using LeaseLedger.Application;
using Microsoft.AspNetCore.Components;

namespace LeaseLedger.Web.Services;

public sealed class WebAuthClient(NavigationManager navigation, IWebHostEnvironment environment) : IAuthClient
{
    public bool ShowDemoHints => environment.IsDevelopment();

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        navigation.NavigateTo("/account/logout", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The website signs in through the sign-in form.");

    public Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The website registers through the registration form.");
}
