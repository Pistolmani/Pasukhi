using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Interfaces;

public interface IMessengerProfileService
{
    Task<SyncMessengerProfileResult> SyncAsync(SyncMessengerProfileRequest request, CancellationToken ct = default);
    Task<string?> GetStoredGreetingTextAsync(CancellationToken ct = default);
}
