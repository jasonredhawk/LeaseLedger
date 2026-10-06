using System.Net.Http.Headers;

namespace LeaseLedger.Mobile.Services;

public sealed class AuthHeaderHandler(AuthTokenStore tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(tokens.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
