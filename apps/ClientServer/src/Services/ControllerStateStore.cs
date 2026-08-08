using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IControllerStateStore
{
    ControllerTopologyDto? GetTopology(int clientId);
    ControllerTelemetryDto? GetTelemetry(int clientId);
    IReadOnlyList<ControllerTelemetryDto> GetAllTelemetry();
    PumpControlStateMachine GetPumpStateMachine(int clientId);
    void UpdateTopology(int clientId, ControllerTopologyDto topology);
    void UpdateTelemetry(int clientId, ControllerTelemetryDto telemetry);
    ControllerStateBackupSnapshot GetSnapshot();
    void RestoreSnapshot(ControllerStateBackupSnapshot snapshot);
}

public sealed class ControllerStateStore : IControllerStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ControllerTopologyDto> _topologies = new();
    private readonly Dictionary<int, ControllerTelemetryDto> _telemetrySnapshots = new();
    private readonly Dictionary<int, PumpControlStateMachine> _machines = new();

    public ControllerTopologyDto? GetTopology(int clientId)
    {
        lock (_gate)
        {
            return _topologies.TryGetValue(clientId, out var topology) ? topology : null;
        }
    }

    public ControllerTelemetryDto? GetTelemetry(int clientId)
    {
        lock (_gate)
        {
            return _telemetrySnapshots.TryGetValue(clientId, out var telemetry) ? telemetry : null;
        }
    }

    public IReadOnlyList<ControllerTelemetryDto> GetAllTelemetry()
    {
        lock (_gate)
        {
            return _telemetrySnapshots.Values.ToList();
        }
    }

    public PumpControlStateMachine GetPumpStateMachine(int clientId)
    {
        lock (_gate)
        {
            if (_machines.TryGetValue(clientId, out var machine))
            {
                return machine;
            }

            var created = new PumpControlStateMachine();
            _machines[clientId] = created;
            return created;
        }
    }

    public void UpdateTopology(int clientId, ControllerTopologyDto topology)
    {
        lock (_gate)
        {
            _topologies[clientId] = topology;
        }
    }

    public void UpdateTelemetry(int clientId, ControllerTelemetryDto telemetry)
    {
        lock (_gate)
        {
            _telemetrySnapshots[clientId] = telemetry;
        }
    }

    public ControllerStateBackupSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new ControllerStateBackupSnapshot
            {
                SavedAtUtc = DateTime.UtcNow,
                Topologies = _topologies.Values.ToList(),
                Telemetry = _telemetrySnapshots.Values.ToList()
            };
        }
    }

    public void RestoreSnapshot(ControllerStateBackupSnapshot snapshot)
    {
        lock (_gate)
        {
            _topologies.Clear();
            _telemetrySnapshots.Clear();

            foreach (var topology in snapshot.Topologies)
            {
                _topologies[topology.ClientId] = topology;
            }

            foreach (var telemetry in snapshot.Telemetry)
            {
                _telemetrySnapshots[telemetry.ClientId] = telemetry;
            }
        }
    }
}