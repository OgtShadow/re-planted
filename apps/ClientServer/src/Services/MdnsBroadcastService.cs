using Makaretu.Dns;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

/// <summary>Advertises the MQTT broker via mDNS so ESP32 nodes can discover it without manual configuration.</summary>
public sealed class MdnsBroadcastService : BackgroundService
{
    private readonly MdnsOptions _options;
    private readonly MqttOptions _mqttOptions;
    private readonly ILogger<MdnsBroadcastService> _logger;
    private ServiceDiscovery? _serviceDiscovery;

    public MdnsBroadcastService(
        IOptions<MdnsOptions> options,
        IOptions<MqttOptions> mqttOptions,
        ILogger<MdnsBroadcastService> logger)
    {
        _options = options.Value;
        _mqttOptions = mqttOptions.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Rozgłaszanie mDNS jest wyłączone w konfiguracji.");
            return Task.CompletedTask;
        }

        try
        {
            var profile = new ServiceProfile(_options.InstanceName, _options.ServiceType, (ushort)_mqttOptions.BrokerPort);
            _serviceDiscovery = new ServiceDiscovery();
            _serviceDiscovery.Advertise(profile);

            _logger.LogInformation(
                "Rozgłoszono usługę mDNS {ServiceType} ({InstanceName}) na porcie {Port}.",
                _options.ServiceType,
                _options.InstanceName,
                _mqttOptions.BrokerPort);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się uruchomić rozgłaszania mDNS. Urządzenia ESP32 będą wymagały ręcznej konfiguracji brokera.");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _serviceDiscovery?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
