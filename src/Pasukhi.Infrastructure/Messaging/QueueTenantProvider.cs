using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Messaging;

public class QueueTenantProvider : ITenantProvider
{
    public Guid BusinessId { get; set; }
}
