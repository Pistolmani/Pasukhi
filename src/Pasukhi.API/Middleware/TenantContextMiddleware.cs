using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Middleware;

/// <summary>
/// Seeds the scoped <see cref="ITenantContext"/> from the authenticated user's
/// <c>BusinessId</c> claim. Runs after auth; if the user is anonymous or has no
/// claim, BusinessId stays at Guid.Empty and DbContext global filters return no rows.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var claim = context.User.FindFirst("BusinessId")?.Value;
        if (Guid.TryParse(claim, out var businessId))
        {
            tenantContext.SetBusinessId(businessId);
        }

        await _next(context);
    }
}
