using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IControllerStateStore
{
    ControllerTopologyDto? Topology { get; }
    ControllerTelemetryDto? Telemetry { get; }
    PumpControlStateMachine PumpStateMachine { get; }
    void UpdateTopology(ControllerTopologyDto topology);
    void UpdateTelemetry(ControllerTelemetryDto telemetry);
}

public sealed class ControllerStateStore : IControllerStateStore
{
    private readonly object _gate = new();
    private ControllerTopologyDto? _topology;
    private ControllerTelemetryDto? _telemetry;

    public PumpControlStateMachine PumpStateMachine { get; } = new();

    public ControllerTopologyDto? Topology
    {
        get
        {
            lock (_gate)
            {
                return _topology;
            }
        }
    }

    public ControllerTelemetryDto? Telemetry
    {
        get
        {
            lock (_gate)
            {
                return _telemetry;
            }
        }
    }

    public void UpdateTopology(ControllerTopologyDto topology)
    {
        lock (_gate)
        {
            _topology = topology;
        }
    }

    public void UpdateTelemetry(ControllerTelemetryDto telemetry)
    {
        lock (_gate)
        {
            _telemetry = telemetry;
        }
    }
}