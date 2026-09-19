using ClientServer.Contracts;
using ClientServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientServer.Controllers;

/// <summary>Exposes the in-memory registry of ESP32 nodes registered via the MQTT discovery handshake.</summary>
[ApiController]
[Route("api/client-server/devices")]
public sealed class DeviceRegistrationController : ControllerBase
{
    private readonly IDeviceRegistrationStore _registrationStore;

    public DeviceRegistrationController(IDeviceRegistrationStore registrationStore)
    {
        _registrationStore = registrationStore;
    }

    /// <summary>Returns every device currently registered with the IoT Controller.</summary>
    [HttpGet("registered")]
    [ProducesResponseType(typeof(IReadOnlyList<RegisteredDeviceDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RegisteredDeviceDto>> GetRegisteredDevices()
    {
        return Ok(_registrationStore.GetAll());
    }

    /// <summary>Returns a single registered device by its deviceId.</summary>
    [HttpGet("registered/{deviceId}")]
    [ProducesResponseType(typeof(RegisteredDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RegisteredDeviceDto> GetRegisteredDevice(string deviceId)
    {
        if (_registrationStore.TryGet(deviceId, out var device) && device is not null)
        {
            return Ok(device);
        }

        return NotFound(new { response = "Urządzenie o podanym identyfikatorze nie jest zarejestrowane." });
    }
}
