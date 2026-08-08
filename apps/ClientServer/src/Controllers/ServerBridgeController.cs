using ClientServer.Contracts;
using ClientServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientServer.Controllers;

[ApiController]
[Route("api/client-server/server-check")]
public sealed class ServerBridgeController : ControllerBase
{
    private readonly IServerProbeService _serverProbeService;

    public ServerBridgeController(IServerProbeService serverProbeService)
    {
        _serverProbeService = serverProbeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ServerCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServerCheckResponse), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ServerCheckResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await _serverProbeService.CheckAsync(cancellationToken);
        return result.Reachable ? Ok(result) : StatusCode(StatusCodes.Status502BadGateway, result);
    }
}
