using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Tenant;

/// <summary>
/// Scoped holder for the current tenant's BusinessId. Mutable so middleware (HTTP)
/// and background-service scopes (queue) can seed it before downstream code runs.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid BusinessId { get; private set; }

    public void SetBusinessId(Guid businessId) => BusinessId = businessId;
}
