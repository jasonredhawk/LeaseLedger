using LeaseLedger.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeaseLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLeaseLedgerInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LeaseLedgerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<LeaseLedgerDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
        });

        services.AddScoped<IPaymentGateway, UnconfiguredPaymentGateway>();
        services.AddScoped<LeaseLedgerService>();
        services.AddScoped<ILeaseLedgerClient, SynchronizedLeaseLedgerClient>();
        services.AddScoped<IAccountGateway, AccountGateway>();
        return services;
    }
}
