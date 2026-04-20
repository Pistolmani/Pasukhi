namespace Pasukhi.Application.Interfaces;

public interface IWhatsAppChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        string phoneNumberId,
        CancellationToken ct = default);
}
