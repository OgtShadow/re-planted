using ClientServer.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClientServer.Controllers;

[ApiController]
[Route("api/client-server/controllers/{clientId:int}")]
public sealed class IoTControllerController : ControllerBase
{
    private readonly IControllerStateStore _stateStore;
    private readonly IMainServerTopologyClient _topologyClient;
    private readonly ILogger<IoTControllerController> _logger;

    public IoTControllerController(
        IControllerStateStore stateStore,
        IMainServerTopologyClient topologyClient,
        ILogger<IoTControllerController> logger)
    {
        _stateStore = stateStore;
        _topologyClient = topologyClient;
        _logger = logger;
    }

    [HttpGet("telemetry/current")]
    [ProducesResponseType(typeof(ControllerTelemetryDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerTelemetryDto> GetCurrentTelemetry(int clientId)
    {
        var telemetry = _stateStore.Telemetry;
        if (telemetry is null || telemetry.ClientId != clientId)
        {
            return Ok(new ControllerTelemetryDto(
                $"client-{clientId}",
                0,
                0,
                0,
                0,
                false,
                false,
                DateTime.UtcNow,
                clientId,
                "Idle",
                null,
                null,
                DateTime.UtcNow));
        }

        return Ok(telemetry);
    }

    [HttpGet("topology")]
    [ProducesResponseType(typeof(ControllerTopologyDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerTopologyDto> GetTopology(int clientId)
    {
        var topology = _stateStore.Topology;
        if (topology is null || topology.ClientId != clientId)
        {
            return Ok(new ControllerTopologyDto(clientId, DateTime.UtcNow, []));
        }

        return Ok(topology);
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(ControllerTopologyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ControllerTopologyDto>> Sync(int clientId, CancellationToken cancellationToken)
    {
        var topology = await _topologyClient.GetTopologyAsync(cancellationToken);
        if (topology is null)
        {
            _logger.LogWarning("Nie udało się zsynchronizować topologii dla klienta {ClientId}.", clientId);
            return StatusCode(StatusCodes.Status502BadGateway, new { response = "Nie udało się zsynchronizować topologii z głównym serwerem." });
        }

        _stateStore.UpdateTopology(topology with { SyncedAtUtc = DateTime.UtcNow });
        return Ok(_stateStore.Topology);
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(ControllerStatusDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerStatusDto> GetStatus(int clientId)
    {
        var topology = _stateStore.Topology;
        var phase = _stateStore.PumpStateMachine.Phase.ToString();

        return Ok(new ControllerStatusDto(
            clientId,
            phase,
            _stateStore.PumpStateMachine.IsInSoak(DateTime.UtcNow),
            _stateStore.PumpStateMachine.SoakUntilUtc,
            topology?.SyncedAtUtc ?? DateTime.UtcNow,
            _stateStore.PumpStateMachine.WarningMessage,
            topology?.Plants.Count ?? 0));
    }
}