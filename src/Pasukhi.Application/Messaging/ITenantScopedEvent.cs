namespace Pasukhi.Application.Messaging;

/// <summary>
/// Marker for queue events that carry a BusinessId for tenant scoping.
/// Background services read this to seed the scoped TenantContext before processing.
/// </summary>
public interface ITenantScopedEvent
{
    Guid BusinessId { get; }
}
