using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ClientServer.Contracts;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace ClientServer.Services;

public sealed class MqttBridgeService : BackgroundService, IMqttBridgeService, IAsyncDisposable
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttBridgeService> _logger;
    private readonly IMqttClient _mqttClient;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TelemetryPayload> _latestTelemetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    public MqttBridgeService(IOptions<MqttOptions> options, ILogger<MqttBridgeService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _mqttClient = new MqttFactory().CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MQTT jest wyłączony w konfiguracji.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await ConnectAndSubscribeAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd pętli MQTT. Próba ponownego połączenia za chwilę.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.ReconnectDelaySeconds)), stoppingToken);
            }
        }

        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
        }
    }

    public Task<bool> PublishPumpCommandAsync(string deviceId, int durationMs, CancellationToken cancellationToken = default)
    {
        return PublishActuatorCommandAsync(deviceId, "pump", true, durationMs, cancellationToken);
    }

    public async Task<bool> PublishActuatorCommandAsync(string deviceId, string command, bool state, int durationMs, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Pominięto publikację komendy MQTT, ponieważ MQTT jest wyłączony.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _logger.LogWarning("Pominięto publikację komendy MQTT, ponieważ deviceId jest pusty.");
            return false;
        }

        if (durationMs <= 0)
        {
            _logger.LogWarning("Pominięto publikację komendy MQTT, ponieważ durationMs={DurationMs} jest nieprawidłowe.", durationMs);
            return false;
        }

        if (!_mqttClient.IsConnected)
        {
            _logger.LogWarning("Pominięto publikację komendy MQTT dla {DeviceId}, brak aktywnego połączenia z brokerem.", deviceId);
            return false;
        }

        var payload = new CommandPayload(deviceId, string.IsNullOrWhiteSpace(command) ? "pump" : command, state, durationMs, DateTime.UtcNow);
        var topic = _options.CommandsTopicTemplate.Replace("{deviceId}", deviceId, StringComparison.OrdinalIgnoreCase);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payloadJson)
                .WithQualityOfServiceLevel(MapQos(_options.QosLevel))
                .WithRetainFlag(false)
                .Build();

            var result = await _mqttClient.PublishAsync(message, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Broker MQTT odrzucił komendę {Command} dla {DeviceId}.", payload.Command, deviceId);
                return false;
            }

            _logger.LogInformation("Opublikowano komendę MQTT {Command} dla {DeviceId} na {DurationMs} ms.", payload.Command, deviceId, durationMs);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się opublikować komendy MQTT dla urządzenia {DeviceId}.", deviceId);
            return false;
        }
        finally
        {
            _publishGate.Release();
        }
    }

    public bool TryGetLatestTelemetry(string deviceId, out TelemetryPayload? telemetry)
    {
        telemetry = null;

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return _latestTelemetry.TryGetValue(deviceId, out telemetry);
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        var clientId = string.IsNullOrWhiteSpace(_options.ClientId)
            ? $"re-planted-client-server-{Environment.MachineName}"
            : _options.ClientId;

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(Math.Max(5, _options.KeepAliveSeconds)))
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        var clientOptions = builder.Build();
        await _mqttClient.ConnectAsync(clientOptions, cancellationToken);

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topicFilter =>
            {
                topicFilter.WithTopic(_options.TelemetryTopicFilter);
                topicFilter.WithQualityOfServiceLevel(MapQos(_options.QosLevel));
            })
            .Build();

        await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
        _logger.LogInformation("Zasubskrybowano temat telemetrii MQTT: {TopicFilter}.", _options.TelemetryTopicFilter);
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs _)
    {
        _logger.LogInformation("Połączono z brokerem MQTT {Host}:{Port}.", _options.BrokerHost, _options.BrokerPort);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Rozłączono z brokerem MQTT. Powód: {Reason}.", args.ReasonString ?? args.Reason.ToString());
        return Task.CompletedTask;
    }

    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        if (args.ApplicationMessage.Topic is null)
        {
            return Task.CompletedTask;
        }

        var topicSegments = args.ApplicationMessage.Topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (topicSegments.Length != 4 ||
            !string.Equals(topicSegments[0], "replanted", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(topicSegments[1], "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var sourceType = topicSegments[2];
        var deviceId = topicSegments[3];

        var payloadBytes = args.ApplicationMessage.PayloadSegment;
        if (payloadBytes.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(payloadBytes.AsSpan());
            var parsedPayload = JsonSerializer.Deserialize<TelemetryPayload>(payloadJson, _jsonOptions);
            if (parsedPayload is null)
            {
                return Task.CompletedTask;
            }

            var normalizedPayload = new TelemetryPayload(
                string.IsNullOrWhiteSpace(parsedPayload.DeviceId) ? deviceId : parsedPayload.DeviceId,
                sourceType,
                ClampToRange(parsedPayload.SoilMoisture),
                ClampToRange(parsedPayload.LightLevel),
                ClampToRange(parsedPayload.Temperature),
                ClampToRange(parsedPayload.Humidity),
                ClampToRange(parsedPayload.WaterLevel),
                parsedPayload.WaterLevelOk,
                parsedPayload.PumpState,
                parsedPayload.LampState,
                parsedPayload.TimestampUtc == default ? DateTime.UtcNow : parsedPayload.TimestampUtc);

            _latestTelemetry[deviceId] = normalizedPayload;

            _logger.LogInformation(
                "Odebrano telemetrię MQTT: urządzenie={DeviceId}, źródło={SourceType}, wilgotność={SoilMoisture}, poziomWody={WaterLevel}, pompa={PumpState}.",
                deviceId,
                sourceType,
                normalizedPayload.SoilMoisture,
                normalizedPayload.WaterLevel,
                normalizedPayload.PumpState);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się przetworzyć telemetrii MQTT z tematu {Topic}.", args.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _publishGate.Dispose();
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
        }

        _mqttClient.Dispose();
    }

    private static int? ClampToRange(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 0, 1000);
    }

    private static MqttQualityOfServiceLevel MapQos(int qosLevel)
    {
        return qosLevel switch
        {
            <= 0 => MqttQualityOfServiceLevel.AtMostOnce,
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            _ => MqttQualityOfServiceLevel.ExactlyOnce
        };
    }
}
