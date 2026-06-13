using Microsoft.AspNetCore.Mvc;
using Application.Common.Enums;
using Application.Jobs.Commands.AddManualJobUrl;
using Application.Jobs.Commands.RefreshSavedUrl;
using Application.Jobs.Queries.GetAggregatedJobs;
using Application.Jobs.Queries.GetAggregatedJobsV6;
using Application.Jobs.Queries.GetJobById;
using Application.Jobs.Queries.GetLastSearchSnapshot;
using Application.Jobs.Queries.GetManualVacancies;
using Application.Jobs.Queries.GetRawJobs;
using Application.Jobs.Queries.GetSavedUrls;
using Application.Common.Exceptions;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

[Authorize]
public sealed class JobsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string keywords,
        [FromQuery] string? location = null,
        [FromQuery] WorkFormat? workFormat = null,
        [FromQuery] SeniorityLevel? seniorityLevel = null,
        [FromQuery] decimal? minSalary = null,
        [FromQuery] string? category = null,
        [FromQuery] ReasoningProviderType reasoningProvider = ReasoningProviderType.None,
        [FromQuery] ScoringModelType scoringModel = ScoringModelType.Flash,
        [FromQuery] bool includeCompetitionSignals = false,
        [FromQuery] bool includeRecencyDecay = false,
        CancellationToken ct = default)
    {
        var query = new GetAggregatedJobsQuery
        {
            Keywords                  = keywords,
            Location                  = location,
            WorkFormat                = workFormat,
            SeniorityLevel            = seniorityLevel,
            MinSalary                 = minSalary,
            Category                  = category,
            ReasoningProvider         = reasoningProvider,
            ScoringModel              = scoringModel,
            IncludeCompetitionSignals = includeCompetitionSignals,
            IncludeRecencyDecay       = includeRecencyDecay,
        };
        var result = await Sender.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new GetJobByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }


    [HttpGet("v6")]
    [Authorize]
    public async Task<IActionResult> GetV6(
        [FromQuery] string keywords,
        [FromQuery] string? location = null,
        [FromQuery] Country country = Country.Ukraine,
        [FromQuery] WorkFormat? workFormat = null,
        [FromQuery] SeniorityLevel? seniorityLevel = null,
        [FromQuery] decimal? minSalary = null,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var query = new GetAggregatedJobsV6Query
        {
            Keywords       = keywords ?? string.Empty,
            Location       = location,
            Country        = country,
            WorkFormat     = workFormat,
            SeniorityLevel = seniorityLevel,
            MinSalary      = minSalary,
            Category       = category,
            Limit          = limit,
        };
        try
        {
            var result = await Sender.Send(query, ct);
            return Ok(result);
        }
        catch (CvNotReadyException)
        {
            return StatusCode(425, new
            {
                code    = "cv_not_ready",
                message = "Upload and process your CV in Profile before running analyzed search."
            });
        }
    }


    /// <summary>
    /// Returns the most recent v6 result the system computed for this exact set of
    /// query parameters. Cheap — no Gemini calls. The candidate-side UI calls this
    /// first on app open; only the user's explicit Refresh action triggers <c>GET /v6</c>.
    /// </summary>
    [HttpGet("v6/snapshot")]
    [Authorize]
    public async Task<IActionResult> GetV6Snapshot(
        [FromQuery] string keywords,
        [FromQuery] string? location = null,
        [FromQuery] Country country = Country.Ukraine,
        [FromQuery] WorkFormat? workFormat = null,
        [FromQuery] SeniorityLevel? seniorityLevel = null,
        [FromQuery] decimal? minSalary = null,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var searchParams = new GetAggregatedJobsV6Query
        {
            Keywords       = keywords ?? string.Empty,
            Location       = location,
            Country        = country,
            WorkFormat     = workFormat,
            SeniorityLevel = seniorityLevel,
            MinSalary      = minSalary,
            Category       = category,
            Limit          = limit,
        };
        var snapshot = await Sender.Send(new GetLastSearchSnapshotQuery(searchParams), ct);
        if (snapshot is null) return NoContent();
        return Ok(new
        {
            snapshot.Response,
            snapshot.ExecutedAt,
            snapshot.QueryHash,
        });
    }


    [HttpGet("raw")]
    public async Task<IActionResult> GetRaw(
        [FromQuery] string keywords,
        [FromQuery] string? location = null,
        [FromQuery] Country country = Country.Ukraine,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        var query = new GetRawJobsQuery
        {
            Keywords = keywords ?? string.Empty,
            Location = location,
            Country  = country,
            Limit    = limit,
        };
        var result = await Sender.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("manual")]
    [Authorize]
    public async Task<IActionResult> AddManualUrl(
        [FromBody] AddManualJobUrlRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(
            new AddManualJobUrlCommand(request.Url, request.Alias), ct);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        return Ok(new
        {
            savedUrlId = result.SavedUrlId,
            jobsFound  = result.JobsFound
        });
    }

    [HttpGet("manual")]
    [Authorize]
    public async Task<IActionResult> GetSavedUrls(CancellationToken ct)
    {
        var result = await Sender.Send(new GetSavedUrlsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("manual/{id:guid}/refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshSavedUrl(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new RefreshSavedUrlCommand(id), ct);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        return Ok(new
        {
            parsedCount = result.ParsedCount,
            addedCount  = result.AddedCount
        });
    }

    [HttpGet("manual/vacancies")]
    [Authorize]
    public async Task<IActionResult> GetManualVacancies(CancellationToken ct)
    {
        var result = await Sender.Send(new GetManualVacanciesQuery(), ct);
        return Ok(result);
    }
}

public sealed record AddManualJobUrlRequest(string Url, string? Alias = null);
