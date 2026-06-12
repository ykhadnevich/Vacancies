using Application.Eval.Commands.ScoreSinglePair;
using Application.Eval.Queries.GetEvalIterationDetails;
using Application.Eval.Queries.GetEvalIterations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;


[Authorize]
[EnableRateLimiting("api")]
public sealed class EvalController : BaseController
{


    [HttpGet("iterations")]
    public async Task<IActionResult> GetIterations(CancellationToken ct)
    {
        var iterations = await Sender.Send(new GetEvalIterationsQuery(), ct);
        return Ok(iterations);
    }


    [HttpGet("iterations/{runId}")]
    public async Task<IActionResult> GetIterationDetails(
        string runId, CancellationToken ct)
    {
        var details = await Sender.Send(new GetEvalIterationDetailsQuery(runId), ct);
        if (details is null) return NotFound(new { runId, message = "Eval run not found." });
        return Ok(details);
    }


    [HttpPost("iterations")]
    public async Task<IActionResult> ScoreSinglePair(
        [FromBody] ScoreSinglePairCommand command,
        CancellationToken ct)
    {
        try
        {
            var result = await Sender.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
