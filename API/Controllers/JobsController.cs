using Microsoft.AspNetCore.Mvc;
using Application.Jobs.Commands.AddManualJobUrl;
using Application.Jobs.Commands.RefreshSavedUrl;
using Application.Jobs.Queries.GetAggregatedJobs;
using Application.Jobs.Queries.GetJobById;
using Application.Jobs.Queries.GetManualVacancies;
using Application.Jobs.Queries.GetSavedUrls;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

public class JobsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string keywords,
        [FromQuery] string? location = null,
        [FromQuery] WorkFormat? workFormat = null,
        [FromQuery] SeniorityLevel? seniorityLevel = null,
        [FromQuery] decimal? minSalary = null,
        [FromQuery] string? category = null,
        [FromQuery] bool runRelevancePipeline = true,
        CancellationToken ct = default)
    {
        var query = new GetAggregatedJobsQuery
        {
            Keywords             = keywords,
            Location             = location,
            WorkFormat           = workFormat,
            SeniorityLevel       = seniorityLevel,
            MinSalary            = minSalary,
            Category             = category,
            RunRelevancePipeline = runRelevancePipeline
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
    public async Task<IActionResult> GetManualVacancies(CancellationToken ct)
    {
        var result = await Sender.Send(new GetManualVacanciesQuery(), ct);
        return Ok(result);
    }
}

public record AddManualJobUrlRequest(string Url, string? Alias = null);
