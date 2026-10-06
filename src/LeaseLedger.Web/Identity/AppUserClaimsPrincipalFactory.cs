using System.Security.Claims;
using LeaseLedger.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LeaseLedger.Web.Identity;

public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrEmpty(user.Email) && !identity.HasClaim(claim => claim.Type == ClaimTypes.Email))
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        if (!identity.HasClaim(claim => claim.Type == ClaimTypes.GivenName))
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        return identity;
    }
}
