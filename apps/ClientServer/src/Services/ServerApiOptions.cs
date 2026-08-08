namespace ClientServer.Services;

public sealed class ServerApiOptions
{
    public string BaseUrl { get; set; } = "http://app:{{SERVER_PORT}}";
    public string CommunicationPath { get; set; } = "/communication-test";
}
