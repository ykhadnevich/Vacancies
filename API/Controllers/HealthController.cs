using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;


[ApiController]
[Route("health")]
[AllowAnonymous]
[DisableRateLimiting]
public sealed class HealthController : ControllerBase
{
    private readonly IDatabaseHealthService _db;

    public HealthController(IDatabaseHealthService db) => _db = db;


    [HttpGet]
    public IActionResult Live() => Ok(new
    {
        status = "alive",
        time = DateTime.UtcNow
    });


    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var canConnect = await _db.CanConnectAsync(ct);
        return canConnect
            ? Ok(new { status = "ready", time = DateTime.UtcNow })
            : StatusCode(503, new { status = "db-unreachable" });
    }
}
