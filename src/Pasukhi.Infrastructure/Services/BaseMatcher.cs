using Microsoft.EntityFrameworkCore;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

/// <summary>
/// Base for matchers called from background consumers. Always uses explicit businessId predicates
/// with IgnoreQueryFilters() rather than the ambient tenant context, because queue consumers may
/// not have an active HTTP request. MatchCount increments are left on tracked entities —
/// SaveChangesAsync is intentionally NOT called here; the consumer owns the save boundary.
/// </summary>
public abstract class BaseMatcher
{
    protected readonly PasukhiDbContext Db;

    protected BaseMatcher(PasukhiDbContext db)
    {
        Db = db;
    }

    protected IQueryable<T> TenantQuery<T>(DbSet<T> dbSet, Guid businessId)
        where T : TenantEntity
        => dbSet.IgnoreQueryFilters().Where(x => x.BusinessId == businessId);
}
