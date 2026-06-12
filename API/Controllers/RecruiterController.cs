using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Recruiter.Commands.AddCandidatesToList;
using Application.Recruiter.Commands.AnalyzeListAgainstVacancy;
using Application.Recruiter.Commands.CreateCandidateList;
using Application.Recruiter.Commands.CreateVacancy;
using Application.Recruiter.Commands.CreateVacancyFromUrl;
using Application.Recruiter.Commands.DeleteCandidateList;
using Application.Recruiter.Commands.DeleteRecruiterCandidate;
using Application.Recruiter.Queries.GetCandidateListDetails;
using Application.Recruiter.Queries.GetMyCandidateLists;
using Application.Recruiter.Queries.GetMyVacancies;
using Application.Recruiter.Queries.GetVacancyResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Recruiter cabinet — separate logical surface from the candidate-side endpoints.
/// All endpoints require authentication; recruiter-role + ownership enforcement
/// happens inside the MediatR pipeline (RequireRecruiter / VacancyOwnership /
/// CandidateListOwnership behaviors), so this controller stays thin.
/// </summary>
[Route("api/recruiter")]
[Authorize]
public sealed class RecruiterController : BaseController
{
    private readonly ICvParserService _pdfParser;

    public RecruiterController(ICvParserService pdfParser)
    {
        _pdfParser = pdfParser;
    }

    // ─── Vacancies ──────────────────────────────────────────────────────────

    [HttpPost("vacancy")]
    public async Task<IActionResult> CreateVacancy(
        [FromBody] CreateRecruiterVacancyRequest body,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new CreateRecruiterVacancyCommand(
                body.Title, body.Company, body.RawDescription, body.Location), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("vacancy/from-url")]
    public async Task<IActionResult> CreateVacancyFromUrl(
        [FromBody] CreateVacancyFromUrlRequest body,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new CreateRecruiterVacancyFromUrlCommand(body.Url), ct);
            if (result.VacancyId == Guid.Empty)
                return BadRequest(new { message = result.NormalizationError ?? "Scraper returned no results." });
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("vacancies")]
    public async Task<IActionResult> ListMyVacancies(CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new GetMyVacanciesQuery(), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    // ─── Candidate Lists ────────────────────────────────────────────────────

    [HttpPost("candidate-list")]
    public async Task<IActionResult> CreateCandidateList(
        [FromBody] CreateCandidateListRequest body,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new CreateCandidateListCommand(
                body.Name, body.Description), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("candidate-lists")]
    public async Task<IActionResult> ListMyCandidateLists(CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new GetMyCandidateListsQuery(), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpGet("candidate-list/{listId:guid}")]
    public async Task<IActionResult> GetCandidateListDetails(Guid listId, CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new GetCandidateListDetailsQuery(listId), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    /// <summary>
    /// Adds candidates to a list. Accepts either JSON (raw CV text array) or
    /// multipart/form-data (PDF files; one CV per file). Mixed-mode in one request
    /// is intentional — recruiters often have some pasted and some downloaded.
    /// </summary>
    [HttpPost("candidate-list/{listId:guid}/candidates")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> AddCandidates(
        Guid listId,
        [FromForm] List<IFormFile>? files,
        [FromForm] string? candidateNames,
        [FromQuery] string? jsonInline,
        CancellationToken ct)
    {
        var inputs = new List<NewCandidateInput>();

        // Branch 1: multipart PDFs.
        if (files is { Count: > 0 })
        {
            var names = ParseCommaSeparated(candidateNames);
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file.Length == 0) continue;
                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = $"{file.FileName}: only PDF files are supported." });

                using var stream = file.OpenReadStream();
                string text;
                try { text = await _pdfParser.ExtractTextAsync(stream, ct); }
                catch (Exception ex) { return BadRequest(new { message = $"{file.FileName}: PDF parse failed: {ex.Message}" }); }

                var displayName = i < names.Count ? names[i] : Path.GetFileNameWithoutExtension(file.FileName);
                inputs.Add(new NewCandidateInput(text, displayName));
            }
        }

        // Branch 2: JSON body alongside (or instead of) PDFs.
        if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var body = await Request.ReadFromJsonAsync<AddCandidatesJsonBody>(cancellationToken: ct);
            if (body?.Candidates is { Count: > 0 })
            {
                foreach (var item in body.Candidates)
                {
                    if (string.IsNullOrWhiteSpace(item.CvRawText)) continue;
                    inputs.Add(new NewCandidateInput(item.CvRawText, item.CandidateName));
                }
            }
        }

        if (inputs.Count == 0)
            return BadRequest(new { message = "No candidates supplied (PDF files or JSON body required)." });

        try
        {
            var result = await Sender.Send(
                new AddCandidatesToListCommand(listId, inputs), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpDelete("candidate-list/{listId:guid}")]
    public async Task<IActionResult> DeleteCandidateList(Guid listId, CancellationToken ct)
    {
        try
        {
            await Sender.Send(new DeleteCandidateListCommand(listId), ct);
            return NoContent();
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpDelete("candidate/{candidateId:guid}")]
    public async Task<IActionResult> DeleteCandidate(Guid candidateId, CancellationToken ct)
    {
        try
        {
            await Sender.Send(new DeleteRecruiterCandidateCommand(candidateId), ct);
            return NoContent();
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    // ─── Analyse ────────────────────────────────────────────────────────────

    [HttpPost("vacancy/{vacancyId:guid}/analyze")]
    public async Task<IActionResult> Analyze(
        Guid vacancyId,
        [FromQuery] Guid listId,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(
                new AnalyzeListAgainstVacancyCommand(vacancyId, listId), ct);

            return result.Status switch
            {
                AnalyzeStatus.AlreadyRunning       => StatusCode(409, result),
                AnalyzeStatus.VacancyNotNormalized => StatusCode(422, result),
                _                                  => Ok(result),
            };
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpGet("vacancy/{vacancyId:guid}/results")]
    public async Task<IActionResult> GetResults(
        Guid vacancyId,
        [FromQuery] Guid listId,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(new GetVacancyResultsQuery(vacancyId, listId), ct);
            return Ok(result);
        }
        catch (ForbiddenAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static List<string> ParseCommaSeparated(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public sealed record CreateRecruiterVacancyRequest(
    string Title,
    string Company,
    string RawDescription,
    string? Location);

public sealed record CreateVacancyFromUrlRequest(string Url);

public sealed record CreateCandidateListRequest(string Name, string? Description);

public sealed record AddCandidatesJsonBody(List<AddCandidateJsonItem> Candidates);

public sealed record AddCandidateJsonItem(string CvRawText, string? CandidateName);
