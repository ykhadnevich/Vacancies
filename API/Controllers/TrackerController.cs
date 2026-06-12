using Microsoft.AspNetCore.Mvc;
using Application.Tracker.Commands.AddToTracker;
using Application.Tracker.Commands.DeleteApplication;
using Application.Tracker.Commands.UpdateApplicationStatus;
using Application.Tracker.Queries.GetUserApplications;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

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
            JobVacancyId = request.JobVacancyId,
            Title = request.Title,
            Company = request.Company,
            Url = request.Url,
            Salary = request.Salary,
            SeniorityLevel = request.SeniorityLevel ?? SeniorityLevel.NotSpecified
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
    Guid? JobVacancyId,
    string? Title,
    string? Company,
    string? Url,
    string? Salary,
    SeniorityLevel? SeniorityLevel);

public record UpdateApplicationRequest(
    ApplicationStatus? Status,
    string? PipelineStep,
    bool? PipelineStepValue,
    string? Notes);
