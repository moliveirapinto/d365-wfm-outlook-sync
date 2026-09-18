using Azure.Core;

namespace WfmOutlookSync.Auth;

/// <summary>
/// HttpClient delegating handler that attaches a bearer token acquired from a
/// <see cref="TokenCredential"/> for a fixed resource scope, refreshing it as needed.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes;
    private AccessToken _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BearerTokenHandler(TokenCredential credential, string scope)
    {
        _credential = credential;
        _scopes = new[] { scope };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _cached.Token;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                _cached = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), ct);
            }

            return _cached.Token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
