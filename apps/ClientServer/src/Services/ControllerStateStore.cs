using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IControllerStateStore
{
    ControllerTopologyDto? GetTopology(int clientId);
    ControllerConfigurationSnapshot? GetConfiguration(int clientId);
    ControllerTelemetryDto? GetTelemetry(int clientId);
    IReadOnlyList<ControllerTelemetryDto> GetAllTelemetry();
    PumpControlStateMachine GetPumpStateMachine(int clientId);
    void UpdateTopology(int clientId, ControllerTopologyDto topology);
    void UpdateConfiguration(ControllerConfigurationSnapshot configuration);
    void UpdateTelemetry(int clientId, ControllerTelemetryDto telemetry);
    ControllerStateBackupSnapshot GetSnapshot();
    void RestoreSnapshot(ControllerStateBackupSnapshot snapshot);
}

public sealed class ControllerStateStore : IControllerStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ControllerTopologyDto> _topologies = new();
    private readonly Dictionary<int, ControllerConfigurationSnapshot> _configurations = new();
    private readonly Dictionary<int, ControllerTelemetryDto> _telemetrySnapshots = new();
    private readonly Dictionary<int, PumpControlStateMachine> _machines = new();

    public ControllerTopologyDto? GetTopology(int clientId)
    {
        lock (_gate)
        {
            return _topologies.TryGetValue(clientId, out var topology) ? topology : null;
        }
    }

    public ControllerConfigurationSnapshot? GetConfiguration(int clientId)
    {
        lock (_gate)
        {
            return _configurations.TryGetValue(clientId, out var configuration) ? configuration : null;
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

    public void UpdateConfiguration(ControllerConfigurationSnapshot configuration)
    {
        lock (_gate)
        {
            _configurations[configuration.ClientId] = configuration;
            _topologies[configuration.ClientId] = configuration.Topology;
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
                Telemetry = _telemetrySnapshots.Values.ToList(),
                Configurations = _configurations.Values.ToList()
            };
        }
    }

    public void RestoreSnapshot(ControllerStateBackupSnapshot snapshot)
    {
        lock (_gate)
        {
            _topologies.Clear();
            _configurations.Clear();
            _telemetrySnapshots.Clear();

            foreach (var topology in snapshot.Topologies)
            {
                _topologies[topology.ClientId] = topology;
            }

            foreach (var configuration in snapshot.Configurations)
            {
                _configurations[configuration.ClientId] = configuration;
                _topologies[configuration.ClientId] = configuration.Topology;
            }

            foreach (var telemetry in snapshot.Telemetry)
            {
                _telemetrySnapshots[telemetry.ClientId] = telemetry;
            }
        }
    }
}