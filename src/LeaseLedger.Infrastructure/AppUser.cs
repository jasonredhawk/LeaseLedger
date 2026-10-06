using Microsoft.AspNetCore.Identity;

namespace LeaseLedger.Infrastructure;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
}
