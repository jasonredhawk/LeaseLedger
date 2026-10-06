using LeaseLedger.Application;
using LeaseLedger.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed class AccountGateway(UserManager<AppUser> users, LeaseLedgerDbContext db) : IAccountGateway
{
    public async Task<AccountOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var role = request.Role?.Trim();
        if (role is not (AppRoles.Landlord or AppRoles.Tenant))
            return AccountOutcome.Fail("Choose a landlord or tenant account.");

        var displayName = request.DisplayName.Trim();
        var email = NormalizeEmail(request.Email);
        if (displayName.Length == 0)
            return AccountOutcome.Fail("Enter the name to show on the account.");
        if (email.Length == 0 || !email.Contains('@'))
            return AccountOutcome.Fail("Enter a valid email address.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return AccountOutcome.Fail(string.Join(" ", created.Errors.Select(error => error.Description)));

        var roleResult = await users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            return AccountOutcome.Fail(string.Join(" ", roleResult.Errors.Select(error => error.Description)));

        if (role == AppRoles.Tenant)
            await LinkTenantInvitesAsync(user.Id, cancellationToken);

        return AccountOutcome.Ok(user.Id, email, displayName, role);
    }

    public async Task<AccountOutcome> ValidatePasswordAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            return AccountOutcome.Fail("Email or password is incorrect.");

        var roles = await users.GetRolesAsync(user);
        var role = roles.FirstOrDefault();
        if (role is null)
            return AccountOutcome.Fail("This account has no role.");

        if (role == AppRoles.Tenant)
            await LinkTenantInvitesAsync(user.Id, cancellationToken);

        return AccountOutcome.Ok(user.Id, user.Email ?? email, user.DisplayName, role);
    }

    public async Task LinkTenantInvitesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user?.Email is null || !await users.IsInRoleAsync(user, AppRoles.Tenant))
            return;

        var email = NormalizeEmail(user.Email);
        var pending = await db.Tenancies
            .Where(tenancy => tenancy.TenantUserId == null && tenancy.TenantEmail == email)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        foreach (var tenancy in pending)
            tenancy.TenantUserId = userId;

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
