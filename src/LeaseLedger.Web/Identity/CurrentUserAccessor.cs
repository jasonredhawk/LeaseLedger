using System.Security.Claims;
using LeaseLedger.Application;
using Microsoft.AspNetCore.Components.Authorization;

namespace LeaseLedger.Web.Identity;

public sealed class CurrentUserAccessor(IHttpContextAccessor http, AuthenticationStateProvider authentication) : ICurrentUserAccessor
{
    public async Task<CurrentUser> GetAsync(CancellationToken cancellationToken = default)
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated == true)
            return From(principal);

        var state = await authentication.GetAuthenticationStateAsync();
        return From(state.User);
    }

    private static CurrentUser From(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return new CurrentUser(null, null, null, null);

        Guid? id = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        return new CurrentUser(
            id,
            principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity.Name,
            principal.FindFirstValue(ClaimTypes.Role),
            principal.FindFirstValue(ClaimTypes.GivenName) ?? principal.Identity.Name);
    }
}
