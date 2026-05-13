namespace Pasukhi.Application.Interfaces;

/// <summary>
/// Mutable tenant context used by both HTTP requests (seeded from JWT) and
/// background-service scopes (seeded from the incoming event). Extends ITenantProvider
/// so existing read-only consumers (DbContext global filters, services) keep working.
/// </summary>
public interface ITenantContext : ITenantProvider
{
    void SetBusinessId(Guid businessId);
}
