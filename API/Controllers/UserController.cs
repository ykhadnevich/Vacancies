using Microsoft.AspNetCore.Mvc;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using API.Services;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

public class UserController : BaseController
{
    private readonly IUserProfileRepository _userRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtTokenService _jwtService;
    private readonly ICvParserService _cvParser;

    public UserController(
        IUserProfileRepository userRepo,
        ICurrentUserService currentUser,
        JwtTokenService jwtService,
        ICvParserService cvParser)
    {
        _userRepo = userRepo;
        _currentUser = currentUser;
        _jwtService = jwtService;
        _cvParser = cvParser;
    }

    [HttpPost("register")]
    [AllowAnonymous]
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

        return CreatedAtAction(nameof(Register), new
        {
            id    = profile.Id,
            token = token
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var profile = await _userRepo.GetByEmailAsync(request.Email, ct);

        if (profile is null || !BCrypt.Net.BCrypt.Verify(request.Password, profile.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        var token = _jwtService.GenerateToken(profile.Id, profile.Email);

        return Ok(new { id = profile.Id, token = token });
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
            request.DisplayName,
            request.Category,
            request.Skills,
            request.ExpectedSalary,
            request.WorkFormat,
            request.SeniorityLevel,
            request.PreferredLocation);

        await _userRepo.UpdateAsync(profile, ct);
        return NoContent();
    }

    [HttpPost("cv")]
    [Authorize]
    public async Task<IActionResult> UploadCv(
        IFormFile file,
        CancellationToken ct)
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
        await _userRepo.UpdateAsync(profile, ct);

        return Ok(new { message = "CV uploaded", extractedLength = rawText.Length });
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
}

public record RegisterRequest(string Email, string Password, string? DisplayName = null);
public record LoginRequest(string Email, string Password);
public record UpdatePreferencesRequest(
    string? DisplayName,
    string? Category,
    List<string> Skills,
    decimal? ExpectedSalary,
    WorkFormat WorkFormat,
    SeniorityLevel SeniorityLevel,
    string? PreferredLocation);
