using Microsoft.AspNetCore.Http;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Tenant;

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid BusinessId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirst("BusinessId")?.Value,
            out var id) ? id : Guid.Empty;
}
