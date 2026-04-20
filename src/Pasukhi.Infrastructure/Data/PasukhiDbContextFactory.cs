using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Data;

public class PasukhiDbContextFactory : IDesignTimeDbContextFactory<PasukhiDbContext>
{
    public PasukhiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=127.0.0.1;Port=55433;Database=pasukhi_dev;Username=postgres;Password=postgres;SSL Mode=Disable";

        var options = new DbContextOptionsBuilder<PasukhiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PasukhiDbContext(options, new DesignTimeTenantProvider());
    }

    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid BusinessId => Guid.Empty;
    }
}
