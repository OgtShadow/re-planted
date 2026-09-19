using System.Collections.Concurrent;
using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IDeviceRegistrationStore
{
    RegisteredDeviceDto RegisterOrUpdate(string deviceId, IReadOnlyList<string> capabilities);
    IReadOnlyList<RegisteredDeviceDto> GetAll();
    bool TryGet(string deviceId, out RegisteredDeviceDto? device);
}

public sealed class DeviceRegistrationStore : IDeviceRegistrationStore
{
    private readonly ConcurrentDictionary<string, RegisteredDeviceDto> _devices = new(StringComparer.OrdinalIgnoreCase);

    public RegisteredDeviceDto RegisterOrUpdate(string deviceId, IReadOnlyList<string> capabilities)
    {
        var nowUtc = DateTime.UtcNow;
        return _devices.AddOrUpdate(
            deviceId,
            _ => new RegisteredDeviceDto(deviceId, capabilities, nowUtc, nowUtc),
            (_, existing) => existing with { Capabilities = capabilities, LastSeenUtc = nowUtc });
    }

    public IReadOnlyList<RegisteredDeviceDto> GetAll()
    {
        return _devices.Values.OrderBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool TryGet(string deviceId, out RegisteredDeviceDto? device)
    {
        return _devices.TryGetValue(deviceId, out device);
    }
}
