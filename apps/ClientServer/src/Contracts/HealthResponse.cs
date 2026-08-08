namespace ClientServer.Contracts;

public sealed record HealthResponse(string Service, string Status, string UtcTime);
