namespace Pasukhi.Application.Interfaces;

public interface IMessengerChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default);
}
