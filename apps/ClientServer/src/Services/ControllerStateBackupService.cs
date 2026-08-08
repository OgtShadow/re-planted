using System.Text.Json;

namespace ClientServer.Services;

public sealed class ControllerStateBackupSnapshot
{
    public DateTime SavedAtUtc { get; set; }
    public List<ClientServer.Contracts.ControllerTopologyDto> Topologies { get; set; } = new();
    public List<ClientServer.Contracts.ControllerTelemetryDto> Telemetry { get; set; } = new();
}

public sealed class ControllerStateBackupService : BackgroundService
{
    private readonly IControllerStateStore _stateStore;
    private readonly ControllerStateBackupOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<ControllerStateBackupService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ControllerStateBackupService(
        IControllerStateStore stateStore,
        Microsoft.Extensions.Options.IOptions<ControllerStateBackupOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<ControllerStateBackupService> logger)
    {
        _stateStore = stateStore;
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Backup stanu kontrolera jest wyłączony.");
            return;
        }

        await RestoreSnapshotAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.SaveIntervalSeconds, 15, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PersistSnapshotAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się zapisać backupu stanu kontrolera.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RestoreSnapshotAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        if (!File.Exists(filePath))
        {
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<ControllerStateBackupSnapshot>(stream, _jsonOptions, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        _stateStore.RestoreSnapshot(snapshot);
        _logger.LogInformation(
            "Odtworzono backup kontrolera z {FilePath} (klientów: {Clients}, telemetria: {TelemetryCount}).",
            filePath,
            snapshot.Topologies.Count,
            snapshot.Telemetry.Count);
    }

    private async Task PersistSnapshotAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = _stateStore.GetSnapshot();
        snapshot.SavedAtUtc = DateTime.UtcNow;

        var tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, filePath, true);
    }

    private string ResolveFilePath()
    {
        if (Path.IsPathRooted(_options.FilePath))
        {
            return _options.FilePath;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, _options.FilePath));
    }
}
