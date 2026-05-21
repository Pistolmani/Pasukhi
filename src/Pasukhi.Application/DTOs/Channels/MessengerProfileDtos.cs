namespace Pasukhi.Application.DTOs.Channels;

public record SyncMessengerProfileRequest(
    string? GreetingText,
    int MaxIceBreakers);
