using ClientServer.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClientServer.Controllers;

[ApiController]
[Route("api/client-server/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse(
            Service: "ClientServer",
            Status: "ok",
            UtcTime: DateTime.UtcNow.ToString("O")
        ));
    }
}
