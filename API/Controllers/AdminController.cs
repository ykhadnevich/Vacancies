using System.Globalization;
using System.Text;
using Application.Common.Interfaces;
using Application.Jobs.Commands.PrewarmVacancies;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;


[Authorize]
public sealed class AdminController : BaseController
{
    private readonly IGeminiCostLogRepository _costLog;
    private readonly IAuditEntryRepository _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _config;

    public AdminController(
        IGeminiCostLogRepository costLog,
        IAuditEntryRepository audit,
        ICurrentUserService currentUser,
        IConfiguration config)
    {
        _costLog = costLog;
        _audit = audit;
        _currentUser = currentUser;
        _config = config;
    }


    [HttpGet("cost")]
    public async Task<IActionResult> GetCost(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddHours(-24)).ToUniversalTime();

        if (fromUtc >= toUtc)
            return BadRequest(new { error = "from must be earlier than to" });

        var rows = await _costLog.QueryAsync(fromUtc, toUtc, ct);

        var sb = new StringBuilder();
        sb.AppendLine(
            "timestamp_utc,user_id,request_id,request_kind,stage,calls," +
            "duration_ms,input_tokens,output_tokens,cost_usd,keywords");
        foreach (var r in rows)
        {
            sb.Append(r.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.UserId?.ToString("D") ?? "").Append(',')
              .Append(r.RequestId.ToString("D")).Append(',')
              .Append(CsvEscape(r.RequestKind)).Append(',')
              .Append(CsvEscape(r.Stage)).Append(',')
              .Append(r.Calls).Append(',')
              .Append(r.DurationMs.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.InputTokens).Append(',')
              .Append(r.OutputTokens).Append(',')
              .Append(r.CostUsd.ToString("0.######", CultureInfo.InvariantCulture)).Append(',')
              .Append(CsvEscape(r.Keywords ?? ""))
              .Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var filename = $"gemini-cost-{fromUtc:yyyyMMddHHmm}-{toUtc:yyyyMMddHHmm}.csv";
        return File(bytes, "text/csv", filename);
    }


    [HttpGet("cost/summary")]
    public async Task<IActionResult> GetCostSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddDays(-30)).ToUniversalTime();

        var rows = await _costLog.QueryAsync(fromUtc, toUtc, ct);

        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                requests = g.Select(r => r.RequestId).Distinct().Count(),
                calls = g.Sum(r => r.Calls),
                inputTokens = g.Sum(r => r.InputTokens),
                outputTokens = g.Sum(r => r.OutputTokens),
                costUsd = Math.Round(g.Sum(r => r.CostUsd), 6),
            })
            .ToList();

        var totalCost = Math.Round(rows.Sum(r => r.CostUsd), 6);
        return Ok(new
        {
            from = fromUtc,
            to = toUtc,
            totalRows = rows.Count,
            totalCostUsd = totalCost,
            byDay,
        });
    }

    // Admin-only: includes IP + UA (PII).
    [HttpGet("audit/user/{userId:guid}")]
    public async Task<IActionResult> GetAuditByUser(
        [FromRoute] Guid userId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();
        if (limit < 1 || limit > 500)
            return BadRequest(new { error = "limit must be between 1 and 500" });

        var rows = await _audit.QueryByUserAsync(userId, limit, ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpGet("audit/entity/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> GetAuditByEntity(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();
        if (string.IsNullOrWhiteSpace(entityType))
            return BadRequest(new { error = "entityType cannot be empty" });

        var rows = await _audit.QueryByEntityAsync(entityType, entityId, ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost("cache/prewarm-vacancies")]
    public async Task<IActionResult> PrewarmVacancies(
        [FromBody] PrewarmVacanciesRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        if (request is null || string.IsNullOrWhiteSpace(request.Keywords))
            return BadRequest(new { error = "keywords required" });

        var cmd = new PrewarmVacanciesCommand(
            Keywords:        request.Keywords.Trim(),
            Location:        request.Location,
            Country:         request.Country ?? Country.Ukraine,
            MaxNewVacancies: Math.Clamp(request.MaxNewVacancies ?? 100, 1, 500));

        var result = await Sender.Send(cmd, ct);
        return Ok(result);
    }

    private static object ToDto(Domain.Entities.AuditEntry e) => new
    {
        id          = e.Id,
        userId      = e.UserId,
        action      = e.Action,
        entityType  = e.EntityType,
        entityId    = e.EntityId,
        outcome     = e.Outcome,
        timestamp   = e.Timestamp,
        ipAddress   = e.IpAddress,
        userAgent   = e.UserAgent,
        payloadJson = e.PayloadJson,
    };

    private bool IsAdmin()
    {
        if (_currentUser.UserId is not Guid uid) return false;
        var allowed = _config.GetSection("Admin:UserIds").Get<string[]>() ?? Array.Empty<string>();
        return allowed.Any(id =>
            Guid.TryParse(id, out var parsed) && parsed == uid);
    }

    private static string CsvEscape(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (raw.Contains(',') || raw.Contains('"') || raw.Contains('\n'))
            return "\"" + raw.Replace("\"", "\"\"") + "\"";
        return raw;
    }
}


public sealed record PrewarmVacanciesRequest(
    string Keywords,
    Country? Country = null,
    string? Location = null,
    int? MaxNewVacancies = null);
