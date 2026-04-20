namespace Pasukhi.Application.Interfaces;

public interface IInstagramChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default);
}
