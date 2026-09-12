namespace RePlanted.Server.Contracts.Devices;

public sealed class ManualCommandRequest
{
    public string Command { get; set; } = "pump";
    public bool State { get; set; } = true;
    public int DurationMs { get; set; }
}
