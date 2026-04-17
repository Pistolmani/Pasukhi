using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.UnitTests;

public static class TestDb
{
    public static PasukhiDbContext Create(Guid? businessId = null)
    {
        var options = new DbContextOptionsBuilder<PasukhiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PasukhiDbContext(options, new StubTenantProvider(businessId ?? Guid.NewGuid()));
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid businessId)
        {
            BusinessId = businessId;
        }

        public Guid BusinessId { get; }
    }
}
