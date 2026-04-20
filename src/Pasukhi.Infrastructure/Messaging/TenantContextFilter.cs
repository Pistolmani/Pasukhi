using MassTransit;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Messaging;

/// <summary>
/// MassTransit consume filter that seeds <see cref="ITenantContext"/> from the
/// incoming event's BusinessId before the consumer runs. Without this, DbContext
/// global filters inside consumers would see BusinessId=Guid.Empty and return nothing.
/// </summary>
public class TenantContextFilter<T> : IFilter<ConsumeContext<T>> where T : class, ITenantScopedEvent
{
    private readonly ITenantContext _tenantContext;

    public TenantContextFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-context");

    public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        _tenantContext.SetBusinessId(context.Message.BusinessId);
        return next.Send(context);
    }
}
