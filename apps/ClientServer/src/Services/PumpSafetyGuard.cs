using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed record PumpSafetyDecision(bool Allowed, int DurationMs, string? RejectionReason);

public interface IPumpSafetyGuard
{
    PumpSafetyDecision ValidateStart(int clientId, int requestedDurationMs, ControllerTelemetryDto? telemetry = null);
}

public sealed class PumpSafetyGuard : IPumpSafetyGuard
{
    private readonly IControllerStateStore _stateStore;
    private readonly IoTControllerOptions _options;

    public PumpSafetyGuard(IControllerStateStore stateStore, IOptions<IoTControllerOptions> options)
    {
        _stateStore = stateStore;
        _options = options.Value;
    }

    public PumpSafetyDecision ValidateStart(int clientId, int requestedDurationMs, ControllerTelemetryDto? telemetry = null)
    {
        if (requestedDurationMs <= 0)
        {
            return new(false, 0, "Czas pracy pompy musi być większy od zera.");
        }

        var currentTelemetry = telemetry ?? _stateStore.GetTelemetry(clientId);
        if (currentTelemetry is null)
        {
            return new(false, 0, "Brak telemetrii. Pompa pozostaje wyłączona w trybie bezpiecznym.");
        }

        var maxAge = TimeSpan.FromSeconds(Math.Clamp(_options.MaxTelemetryAgeSeconds, 1, 3600));
        if (DateTime.UtcNow - currentTelemetry.Timestamp > maxAge)
        {
            return new(false, 0, "Telemetria poziomu wody jest nieaktualna. Pompa pozostaje wyłączona.");
        }

        if (currentTelemetry.WaterLevelCm <= Math.Max(0, _options.LowWaterThresholdCm))
        {
            return new(false, 0, "Poziom wody jest zbyt niski. Uruchomienie pompy zostało zablokowane.");
        }

        var maximumDurationMs = Math.Clamp(_options.MaxPumpRunSeconds, 1, 3600) * 1000;
        return new(true, Math.Min(requestedDurationMs, maximumDurationMs), null);
    }
}