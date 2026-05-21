namespace Pasukhi.Application.DTOs.Channels;

public record SyncMessengerProfileRequest(
    string? GreetingText,
    int MaxIceBreakers);

public record SyncMessengerProfileResult(
    bool Success,
    int IceBreakersCount,
    bool GreetingSet);
