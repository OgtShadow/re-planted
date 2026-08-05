using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IJwtTokenProvider _tokenProvider;
    private readonly MainServerApiOptions _options;

    public BearerTokenHandler(IJwtTokenProvider tokenProvider, IOptions<MainServerApiOptions> options)
    {
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenProvider.CreateClientToken(_options.ClientId));
        return base.SendAsync(request, cancellationToken);
    }
}