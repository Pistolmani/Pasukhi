using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Channels;

public record ChannelConnectionDto(
    Guid Id,
    Guid BusinessId,
    ChannelType ChannelType,
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string VerifyToken,
    bool IsActive,
    DateTime? LastWebhookAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateChannelConnectionRequest(
    ChannelType ChannelType,
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string? VerifyToken,
    bool IsActive);

public record UpdateChannelConnectionRequest(
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string VerifyToken,
    bool IsActive);
