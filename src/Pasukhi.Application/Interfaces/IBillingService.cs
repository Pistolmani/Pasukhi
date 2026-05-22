using Pasukhi.Application.DTOs.Billing;

namespace Pasukhi.Application.Interfaces;

public interface IBillingService
{
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(string tier, CancellationToken ct = default);
    Task<PortalSessionDto> CreatePortalSessionAsync(CancellationToken ct = default);
    Task<BillingStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default);
}
