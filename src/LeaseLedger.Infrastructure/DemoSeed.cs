using LeaseLedger.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeaseLedger.Infrastructure;

public static class DemoSeed
{
    public const string LandlordEmail = "landlord@demo.local";
    public const string TenantEmail = "tenant@demo.local";
    public const string Password = "Demo123!";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<LeaseLedgerDbContext>();

        foreach (var role in new[] { AppRoles.Landlord, AppRoles.Tenant })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = role
                });
            }
        }

        if (!await db.RentGuidelines.AnyAsync(cancellationToken))
        {
            db.RentGuidelines.AddRange(OntarioRentGuidelines.SeedCatalog.Select(row => new RentGuideline
            {
                Year = row.Year,
                Percent = row.Percent
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (await users.FindByEmailAsync(LandlordEmail) is not null)
            return;

        var landlord = await CreateUserAsync(users, LandlordEmail, "Demo Landlord", AppRoles.Landlord);
        var tenant = await CreateUserAsync(users, TenantEmail, "Demo Tenant", AppRoles.Tenant);

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlord.Id,
            Street = "245 James Ave",
            City = "Toronto",
            Province = "ON",
            PostalCode = "M5V 2T6"
        };
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Label = "2",
            FirstOccupiedAfterNovember152018 = false
        };
        var tenancy = new Tenancy
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            TenantUserId = tenant.Id,
            TenantName = "Demo Tenant",
            TenantEmail = TenantEmail,
            RentAmount = 2000m,
            RentDueDay = 1,
            Frequency = RentFrequency.Monthly,
            StartDate = new DateOnly(2024, 6, 1),
            LastRentIncreaseDate = new DateOnly(2025, 6, 1)
        };

        db.Properties.Add(property);
        db.Units.Add(unit);
        db.Tenancies.Add(tenancy);
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TenancyId = tenancy.Id,
                Kind = LedgerEntryKind.Charge,
                ChargeCategory = ChargeCategory.Rent,
                Amount = 2000m,
                EffectiveDate = new DateOnly(2026, 8, 1),
                Description = "Rent"
            },
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TenancyId = tenancy.Id,
                Kind = LedgerEntryKind.Payment,
                PaymentMethod = PaymentMethod.ETransfer,
                Amount = 2000m,
                EffectiveDate = new DateOnly(2026, 8, 2),
                Description = "Payment (e-transfer)",
                ReceiptNumber = "R-20260802-DEMO01"
            },
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TenancyId = tenancy.Id,
                Kind = LedgerEntryKind.Charge,
                ChargeCategory = ChargeCategory.Rent,
                Amount = 2000m,
                EffectiveDate = new DateOnly(2026, 9, 1),
                Description = "Rent"
            });

        var request = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            Title = "Leaking kitchen tap",
            Description = "The kitchen tap drips steadily after it is turned off.",
            Status = MaintenanceStatus.Submitted,
            CreatedAt = new DateTimeOffset(2026, 9, 10, 15, 0, 0, TimeSpan.Zero)
        };
        db.MaintenanceRequests.Add(request);
        db.MaintenanceEvents.Add(new MaintenanceEvent
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            Status = MaintenanceStatus.Submitted,
            Note = "Submitted by the tenant.",
            OccurredAt = request.CreatedAt,
            ActorUserId = tenant.Id
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> users, string email, string displayName, string role)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));

        await users.AddToRoleAsync(user, role);
        return user;
    }
}
