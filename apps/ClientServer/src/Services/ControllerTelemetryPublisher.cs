using ClientServer.Contracts;
using ClientServer.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ClientServer.Services;

public interface IControllerTelemetryPublisher
{
    Task PublishAsync(ControllerTelemetryDto telemetry, CancellationToken cancellationToken);
}

public sealed class ControllerTelemetryPublisher : IControllerTelemetryPublisher
{
    private readonly IHubContext<ControllerHub> _hubContext;

    public ControllerTelemetryPublisher(IHubContext<ControllerHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(ControllerTelemetryDto telemetry, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.SendAsync("TelemetryUpdated", telemetry, cancellationToken);
    }
}