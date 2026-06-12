using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

public sealed class UserController : BaseController
{
    private readonly IUserProfileRepository _userRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenService _jwtService;
    private readonly ICvParserService _cvParser;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICvExtractionService _cvExtractor;
    private readonly IAuditEntryRepository _audit;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserProfileRepository userRepo,
        ICurrentUserService currentUser,
        ITokenService jwtService,
        ICvParserService cvParser,
        IEmbeddingService embeddingService,
        ICvExtractionService cvExtractor,
        IAuditEntryRepository audit,
        ILogger<UserController> logger)
    {
        _userRepo = userRepo;
        _currentUser = currentUser;
        _jwtService = jwtService;
        _cvParser = cvParser;
        _embeddingService = embeddingService;
        _cvExtractor = cvExtractor;
        _audit = audit;
        _logger = logger;
    }

    // Direct controller actions bypass AuditingBehavior; this helper mirrors its swallow-on-error semantics.
    private async Task TryWriteAuditAsync(
        string action,
        Guid? userId,
        string? entityType = null,
        Guid? entityId = null,
        object? payload = null,
        string outcome = "Success",
        CancellationToken ct = default)
    {
        try
        {
            var payloadJson = payload is null
                ? null
                : JsonSerializer.Serialize(payload);
            var entry = AuditEntry.Create(
                action:      action,
                userId:      userId,
                entityType:  entityType,
                entityId:    entityId,
                payloadJson: payloadJson,
                ipAddress:   _currentUser.IpAddress,
                userAgent:   _currentUser.UserAgent,
                outcome:     outcome);
            await _audit.AddAsync(entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist audit entry for direct action {Action}", action);
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters" });

        var existing = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Conflict(new { message = "Email already registered" });

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var profile = UserProfile.Create(request.Email, passwordHash, request.DisplayName);
        await _userRepo.AddAsync(profile, ct);

        var token = _jwtService.GenerateToken(profile.Id, profile.Email);

        // Password is never serialized.
        await TryWriteAuditAsync(
            action:     "Register",
            userId:     profile.Id,
            entityType: "UserProfile",
            entityId:   profile.Id,
            payload:    new { email = profile.Email, displayName = request.DisplayName },
            ct:         ct);

        return CreatedAtAction(nameof(Register), new { id = profile.Id, token });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var profile = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (profile is null || !BCrypt.Net.BCrypt.Verify(request.Password, profile.PasswordHash))
        {
            // Brute-force pattern surfaces in (Action='Login', Outcome='Failure'). Password is NEVER recorded.
            await TryWriteAuditAsync(
                action:  "Login",
                userId:  profile?.Id,
                payload: new { email = request.Email },
                outcome: "Failure",
                ct:      ct);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var token = _jwtService.GenerateToken(profile.Id, profile.Email);

        await TryWriteAuditAsync(
            action:  "Login",
            userId:  profile.Id,
            payload: new { email = profile.Email },
            ct:      ct);

        return Ok(new { id = profile.Id, token });
    }

    [HttpPut("preferences")]
    [Authorize]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken ct)
    {
        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();

        profile.UpdatePreferences(
            request.DisplayName, request.Category,
            request.Skills ?? new List<string>(),
            request.ExpectedSalary, request.WorkFormat,
            request.SeniorityLevel, request.PreferredLocation);
        await _userRepo.UpdateAsync(profile, ct);

        await TryWriteAuditAsync(
            action:     "UpdatePreferences",
            userId:     profile.Id,
            entityType: "UserProfile",
            entityId:   profile.Id,
            payload:    new
            {
                displayName       = request.DisplayName,
                category          = request.Category,
                skillCount        = request.Skills?.Count ?? 0,
                expectedSalary    = request.ExpectedSalary,
                workFormat        = request.WorkFormat.ToString(),
                seniorityLevel    = request.SeniorityLevel.ToString(),
                preferredLocation = request.PreferredLocation,
            },
            ct: ct);

        return Ok(new { message = "Preferences updated" });
    }

    [HttpPost("cv")]
    [Authorize]
    public async Task<IActionResult> UploadCv(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is empty" });
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are supported" });

        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();

        using var stream = file.OpenReadStream();
        var rawText = await _cvParser.ExtractTextAsync(stream, ct);
        profile.UpdateCv(file.FileName, rawText);

        var embeddingSkipped = false;
        try
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(rawText, ct);
            profile.SetCvEmbedding(embedding);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException
                                    || ex.InnerException is System.Net.Sockets.SocketException)
        {
            embeddingSkipped = true;
        }

        await _userRepo.UpdateAsync(profile, ct);

        // Raw CV text is PII — never logged. Record only metadata.
        await TryWriteAuditAsync(
            action:     "UploadCv",
            userId:     profile.Id,
            entityType: "UserProfile",
            entityId:   profile.Id,
            payload:    new
            {
                fileName        = file.FileName,
                fileSizeBytes   = file.Length,
                extractedLength = rawText.Length,
                embeddingSkipped,
            },
            ct:         ct);

        return Ok(new
        {
            message = embeddingSkipped
                ? "CV uploaded (embedding skipped — ML API unavailable, will be backfilled later)"
                : "CV uploaded",
            extractedLength = rawText.Length,
            embeddingSkipped
        });
    }

    [HttpPost("cv/normalize")]
    [Authorize]
    public async Task<IActionResult> NormalizeCv(CancellationToken ct)
    {
        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();
        if (string.IsNullOrWhiteSpace(profile.CvRawText))
            return BadRequest(new { message = "Upload a CV first" });

        try
        {
            var result = await _cvExtractor.ExtractAsync(profile.CvRawText!, ct);
            if (string.IsNullOrWhiteSpace(result.Summary)
                || string.IsNullOrWhiteSpace(result.ModelVersion))
                return StatusCode(502, new { message = "CV extraction produced an empty result. Try again." });

            profile.SetCvSummary(result.Summary, result.ModelVersion);
            await _userRepo.UpdateAsync(profile, ct);

            // Summary text itself is PII — record only metadata.
            await TryWriteAuditAsync(
                action:     "NormalizeCv",
                userId:     profile.Id,
                entityType: "UserProfile",
                entityId:   profile.Id,
                payload:    new
                {
                    modelVersion  = result.ModelVersion,
                    summaryLength = result.Summary.Length,
                    inputTokens   = result.InputTokens,
                    outputTokens  = result.OutputTokens,
                },
                ct: ct);

            return Ok(new
            {
                modelVersion  = result.ModelVersion,
                summaryLength = result.Summary.Length,
                inputTokens   = result.InputTokens,
                outputTokens  = result.OutputTokens
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "CV normalization failed", error = ex.Message });
        }
    }

    [HttpGet("cv/status")]
    [Authorize]
    public async Task<IActionResult> GetCvStatus(CancellationToken ct)
    {
        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();

        var hasRaw     = !string.IsNullOrEmpty(profile.CvRawText);
        var hasSummary = !string.IsNullOrEmpty(profile.CvSummary);


        var status = !hasRaw       ? "NoCv"
                   : !hasSummary   ? "PendingNormalization"
                                   : "Ready";

        return Ok(new
        {
            status,
            cvFileName    = profile.CvFileUrl,
            modelVersion  = profile.CvSummaryModelVersion,
            rawTextLength = profile.CvRawText?.Length ?? 0,
            summaryLength = profile.CvSummary?.Length ?? 0
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();

        return Ok(new
        {
            id                = profile.Id,
            email             = profile.Email,
            displayName       = profile.DisplayName,
            role              = profile.Role.ToString(),
            category          = profile.Category,
            skills            = profile.Skills,
            seniorityLevel    = profile.SeniorityLevel,
            expectedSalary    = profile.ExpectedSalary,
            workFormat        = profile.PreferredWorkFormat,
            preferredLocation = profile.PreferredLocation,
            hasCv             = !string.IsNullOrEmpty(profile.CvRawText),
            cvFileName        = profile.CvFileUrl,
            cvTextLength      = profile.CvRawText?.Length ?? 0
        });
    }

    /// <summary>
    /// Switches the caller's role. Recruiter-cabinet endpoints become accessible once
    /// the role is Recruiter or Both. A fresh JWT is issued so the client can drop
    /// any cached profile state — the token shape itself is unchanged for now.
    /// </summary>
    [HttpPost("role")]
    [Authorize]
    public async Task<IActionResult> SetRole(
        [FromBody] SetRoleRequest request,
        CancellationToken ct)
    {
        var profile = await _userRepo.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (profile is null) return NotFound();

        if (!Enum.IsDefined(typeof(UserRole), request.Role))
            return BadRequest(new { message = "Unknown role value." });

        var oldRole = profile.Role;
        profile.SetRole(request.Role);
        await _userRepo.UpdateAsync(profile, ct);

        // Capture old + new role for privilege-change forensic trail.
        await TryWriteAuditAsync(
            action:     "SetRole",
            userId:     profile.Id,
            entityType: "UserProfile",
            entityId:   profile.Id,
            payload:    new
            {
                oldRole = oldRole.ToString(),
                newRole = request.Role.ToString(),
            },
            ct: ct);

        var token = _jwtService.GenerateToken(profile.Id, profile.Email);
        return Ok(new { role = profile.Role.ToString(), token });
    }
}

public record RegisterRequest(string Email, string Password, string? DisplayName = null);
public record LoginRequest(string Email, string Password);
public record UpdatePreferencesRequest(
    string? DisplayName,
    string? Category,
    List<string>? Skills,
    decimal? ExpectedSalary,
    WorkFormat WorkFormat,
    SeniorityLevel SeniorityLevel,
    string? PreferredLocation);
public record SetRoleRequest(UserRole Role);
