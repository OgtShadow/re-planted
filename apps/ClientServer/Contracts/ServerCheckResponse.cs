namespace ClientServer.Contracts;

public sealed record ServerCheckResponse(
    bool Reachable,
    int StatusCode,
    string Source,
    string Message,
    string ResponseBody,
    string UtcTime
);
