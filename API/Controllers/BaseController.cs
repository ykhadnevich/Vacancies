using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

// Default policy: "api" (60 req/min/user). Override per-action for stricter (e.g. "auth").
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public abstract class BaseController : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
