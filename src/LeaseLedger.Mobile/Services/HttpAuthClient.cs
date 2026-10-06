using System.Net.Http.Json;
using LeaseLedger.Application;
using Microsoft.AspNetCore.Components;

namespace LeaseLedger.Mobile.Services;

public sealed class HttpAuthClient(HttpClient http, AuthTokenStore tokens, NavigationManager navigation) : IAuthClient
{
    public bool ShowDemoHints
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/auth/login", request, LeaseLedgerJson.Options, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return AuthResultDto.Fail(ApiSettings.UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResultDto.Fail(ApiSettings.UnreachableMessage);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(LeaseLedgerJson.Options, cancellationToken);
                return AuthResultDto.Fail(error?.Error ?? "Sign-in failed.");
            }

            var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(LeaseLedgerJson.Options, cancellationToken)
                ?? throw new LeaseLedgerException("Sign-in failed.");
            tokens.Token = payload.AccessToken;
            return AuthResultDto.Ok(payload.User);
        }
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/auth/register", request, LeaseLedgerJson.Options, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return AuthResultDto.Fail(ApiSettings.UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResultDto.Fail(ApiSettings.UnreachableMessage);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(LeaseLedgerJson.Options, cancellationToken);
                return AuthResultDto.Fail(error?.Error ?? "Registration failed.");
            }

            var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(LeaseLedgerJson.Options, cancellationToken)
                ?? throw new LeaseLedgerException("Registration failed.");
            tokens.Token = payload.AccessToken;
            return AuthResultDto.Ok(payload.User);
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        tokens.Token = null;
        navigation.NavigateTo("/login", forceLoad: true);
        return Task.CompletedTask;
    }
}
