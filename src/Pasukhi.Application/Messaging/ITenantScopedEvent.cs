namespace Pasukhi.Application.Messaging;

/// <summary>
/// Marker for MassTransit events that carry a BusinessId for tenant scoping.
/// The TenantContextFilter reads this before dispatching to the consumer.
/// </summary>
public interface ITenantScopedEvent
{
    Guid BusinessId { get; }
}
