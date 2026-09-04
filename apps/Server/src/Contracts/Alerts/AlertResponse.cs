namespace RePlanted.Server.Contracts.Alerts;

public sealed class AlertResponse
{
    public int Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? AcknowledgedAtUtc { get; init; }
}
