using Microsoft.AspNetCore.Mvc;
using Application.Tracker.Commands.AddToTracker;
using Application.Tracker.Commands.DeleteApplication;
using Application.Tracker.Commands.UpdateApplicationStatus;
using Application.Tracker.Queries.GetUserApplications;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

[Authorize]
public sealed class TrackerController : BaseController
{
    private readonly ICurrentUserService _currentUser;

    public TrackerController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetApplications(
        [FromQuery] ApplicationStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await Sender.Send(
            new GetUserApplicationsQuery { FilterByStatus = status }, ct);

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddToTracker(
        [FromBody] AddToTrackerRequest request,
        CancellationToken ct)
    {
        var command = new AddToTrackerCommand
        {
            JobVacancyId       = request.JobVacancyId,
            Title              = request.Title,
            Company            = request.Company,
            Location           = request.Location,
            Url                = request.Url,
            Salary             = request.Salary,
            SeniorityLevel     = request.SeniorityLevel ?? SeniorityLevel.NotSpecified,
            Score              = request.Score,
            Verdict            = request.Verdict,
            MatchedSkills      = request.MatchedSkills,
            MissingMustHaves   = request.MissingMustHaves,
            TriggeredAntiFlags = request.TriggeredAntiFlags,
            ReasonShort        = request.ReasonShort,
            StrengthsEn        = request.StrengthsEn,
            StrengthsUk        = request.StrengthsUk,
            GapsEn             = request.GapsEn,
            GapsUk             = request.GapsUk,
            RecommendationEn   = request.RecommendationEn,
            RecommendationUk   = request.RecommendationUk,
            SubScores          = request.SubScores,
            PipelineVersion    = request.PipelineVersion,
        };

        var result = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetApplications), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateApplication(
        Guid id,
        [FromBody] UpdateApplicationRequest request,
        CancellationToken ct)
    {
        var command = new UpdateApplicationStatusCommand
        {
            ApplicationId = id,
            NewStatus = request.Status,
            PipelineStep = request.PipelineStep,
            PipelineStepValue = request.PipelineStepValue,
            Notes = request.Notes,
            UpdateNotes = request.Notes is not null
        };

        var success = await Sender.Send(command, ct);

        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException();

        var success = await Sender.Send(new DeleteApplicationCommand(id, userId), ct);
        return success ? NoContent() : NotFound();
    }
}

public record AddToTrackerRequest(
    Guid?                       JobVacancyId,
    string?                     Title,
    string?                     Company,
    string?                     Location,
    string?                     Url,
    string?                     Salary,
    SeniorityLevel?             SeniorityLevel,
    double?                     Score,
    string?                     Verdict,
    List<string>?               MatchedSkills,
    List<string>?               MissingMustHaves,
    List<string>?               TriggeredAntiFlags,
    string?                     ReasonShort,
    string?                     StrengthsEn,
    string?                     StrengthsUk,
    string?                     GapsEn,
    string?                     GapsUk,
    string?                     RecommendationEn,
    string?                     RecommendationUk,
    Dictionary<string, double>? SubScores,
    string?                     PipelineVersion);

public record UpdateApplicationRequest(
    ApplicationStatus? Status,
    string? PipelineStep,
    bool? PipelineStepValue,
    string? Notes);
