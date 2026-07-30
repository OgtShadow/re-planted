using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IServerProbeService
{
    Task<ServerCheckResponse> CheckAsync(CancellationToken cancellationToken);
}
