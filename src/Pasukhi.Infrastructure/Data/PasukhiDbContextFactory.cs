using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Data;

public class PasukhiDbContextFactory : IDesignTimeDbContextFactory<PasukhiDbContext>
{
    public PasukhiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PasukhiDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=pasukhi_dev;Username=postgres;Password=postgres")
            .Options;

        return new PasukhiDbContext(options, new DesignTimeTenantProvider());
    }

    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid BusinessId => Guid.Empty;
    }
}
