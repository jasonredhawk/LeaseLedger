using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeaseLedger.Infrastructure;

public sealed class LeaseLedgerDbContextFactory : IDesignTimeDbContextFactory<LeaseLedgerDbContext>
{
    public LeaseLedgerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LeaseLedgerDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=LeaseLedger;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;
        return new LeaseLedgerDbContext(options);
    }
}
