namespace ClientServer.Services;

public enum PumpControlPhase
{
    Idle,
    Watering,
    Soaking
}

public sealed class PumpControlStateMachine
{
    private readonly object _gate = new();
    private PumpControlPhase _phase = PumpControlPhase.Idle;
    private DateTime? _soakUntilUtc;
    private string? _activePlantName;
    private string? _warningMessage;

    public PumpControlPhase Phase
    {
        get
        {
            lock (_gate)
            {
                return _phase;
            }
        }
    }

    public DateTime? SoakUntilUtc
    {
        get
        {
            lock (_gate)
            {
                return _soakUntilUtc;
            }
        }
    }

    public string? ActivePlantName
    {
        get
        {
            lock (_gate)
            {
                return _activePlantName;
            }
        }
    }

    public string? WarningMessage
    {
        get
        {
            lock (_gate)
            {
                return _warningMessage;
            }
        }
    }

    public bool IsInSoak(DateTime nowUtc)
    {
        lock (_gate)
        {
            return _phase == PumpControlPhase.Soaking && _soakUntilUtc.HasValue && _soakUntilUtc > nowUtc;
        }
    }

    public void BeginWatering(string? activePlantName)
    {
        lock (_gate)
        {
            _phase = PumpControlPhase.Watering;
            _activePlantName = activePlantName;
            _warningMessage = null;
            _soakUntilUtc = null;
        }
    }

    public void BeginSoak(DateTime nowUtc, TimeSpan soakDuration)
    {
        lock (_gate)
        {
            _phase = PumpControlPhase.Soaking;
            _soakUntilUtc = nowUtc.Add(soakDuration);
        }
    }

    public void MarkIdle()
    {
        lock (_gate)
        {
            _phase = PumpControlPhase.Idle;
            _soakUntilUtc = null;
        }
    }

    public void MarkBlocked(string warningMessage)
    {
        lock (_gate)
        {
            _phase = PumpControlPhase.Idle;
            _warningMessage = warningMessage;
            _soakUntilUtc = null;
        }
    }

    public void Refresh(DateTime nowUtc)
    {
        lock (_gate)
        {
            if (_phase == PumpControlPhase.Soaking && _soakUntilUtc.HasValue && nowUtc >= _soakUntilUtc.Value)
            {
                _phase = PumpControlPhase.Idle;
                _soakUntilUtc = null;
                _warningMessage = null;
            }
        }
    }
}