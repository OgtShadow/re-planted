using ClientServer.Contracts;
using ClientServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientServer.Controllers;

/// <summary>Exposes the runtime API of the IoT Controller layer.</summary>
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

    /// <summary>Returns the latest telemetry snapshot for the controller.</summary>
    [HttpGet("telemetry/current")]
    [ProducesResponseType(typeof(ControllerTelemetryDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerTelemetryDto> GetCurrentTelemetry(int clientId)
    {
        var telemetry = _stateStore.GetTelemetry(clientId);
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

    /// <summary>Returns telemetry snapshots for all active clients.</summary>
    [HttpGet("/api/client-server/controllers/telemetry/current")]
    [ProducesResponseType(typeof(IReadOnlyList<ControllerTelemetryDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ControllerTelemetryDto>> GetCurrentTelemetryForAllClients()
    {
        var telemetry = _stateStore.GetAllTelemetry();
        return Ok(telemetry);
    }

    /// <summary>Returns the last synchronized topology for the controller.</summary>
    [HttpGet("topology")]
    [ProducesResponseType(typeof(ControllerTopologyDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerTopologyDto> GetTopology(int clientId)
    {
        var topology = _stateStore.GetTopology(clientId);
        if (topology is null || topology.ClientId != clientId)
        {
            return Ok(new ControllerTopologyDto(clientId, DateTime.UtcNow, []));
        }

        return Ok(topology);
    }

    /// <summary>Forces synchronization with the main server.</summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ControllerTopologyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ControllerTopologyDto>> Sync(int clientId, CancellationToken cancellationToken)
    {
        var topology = await _topologyClient.GetTopologyAsync(clientId, cancellationToken);
        if (topology is null)
        {
            _logger.LogWarning("Nie udało się zsynchronizować topologii dla klienta {ClientId}.", clientId);
            return StatusCode(StatusCodes.Status502BadGateway, new { response = "Nie udało się zsynchronizować topologii z głównym serwerem." });
        }

        _stateStore.UpdateTopology(clientId, topology with { SyncedAtUtc = DateTime.UtcNow });
        return Ok(_stateStore.GetTopology(clientId));
    }

    /// <summary>Returns the operational state of the controller and its soak timer.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ControllerStatusDto), StatusCodes.Status200OK)]
    public ActionResult<ControllerStatusDto> GetStatus(int clientId)
    {
        var topology = _stateStore.GetTopology(clientId);
        var machine = _stateStore.GetPumpStateMachine(clientId);
        var phase = machine.Phase.ToString();

        return Ok(new ControllerStatusDto(
            clientId,
            phase,
            machine.IsInSoak(DateTime.UtcNow),
            machine.SoakUntilUtc,
            topology?.SyncedAtUtc ?? DateTime.UtcNow,
            machine.WarningMessage,
            topology?.Plants.Count ?? 0));
    }
}