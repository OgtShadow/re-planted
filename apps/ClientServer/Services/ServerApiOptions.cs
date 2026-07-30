namespace ClientServer.Services;

public sealed class ServerApiOptions
{
    public string BaseUrl { get; set; } = "http://app:8080";
    public string CommunicationPath { get; set; } = "/communication-test";
}
