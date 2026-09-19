namespace ClientServer.Services;

public sealed class ServerApiOptions
{
    public const string SectionName = "ServerApi";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string CommunicationPath { get; set; } = "/communication-test";
}
