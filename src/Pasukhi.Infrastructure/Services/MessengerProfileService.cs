using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class MessengerProfileService : IMessengerProfileService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly HttpClient _httpClient;
    private readonly string _graphBaseUrl;
    private readonly string _graphApiVersion;
    private readonly ILogger<MessengerProfileService> _logger;

    public MessengerProfileService(
        PasukhiDbContext db,
        ITenantProvider tenantProvider,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MessengerProfileService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _httpClient = httpClient;
        _graphBaseUrl = (configuration["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com").TrimEnd('/');
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v21.0";
        _logger = logger;
    }

    public Task<SyncMessengerProfileResult> SyncAsync(SyncMessengerProfileRequest request, CancellationToken ct = default)
        => throw new NotImplementedException();

    public async Task<string?> GetStoredGreetingTextAsync(CancellationToken ct = default)
    {
        EnsureTenant();
        var setting = await _db.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.MessengerGreetingText, ct);
        return setting?.Value;
    }

    private static object BuildPayload(IList<object> callToActions, string? greetingText)
    {
        if (string.IsNullOrWhiteSpace(greetingText))
        {
            return new
            {
                get_started = new { payload = "GET_STARTED" },
                ice_breakers = new[] { new { call_to_actions = callToActions } }
            };
        }

        return new
        {
            get_started = new { payload = "GET_STARTED" },
            ice_breakers = new[] { new { call_to_actions = callToActions } },
            greeting = new[] { new { locale = "default", text = greetingText } }
        };
    }

    private async Task PersistGreetingTextAsync(Guid businessId, string greetingText, CancellationToken ct)
    {
        var existing = await _db.BusinessSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.MessengerGreetingText, ct);

        if (existing is not null)
        {
            existing.Value = greetingText;
        }
        else
        {
            _db.BusinessSettings.Add(new BusinessSetting
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Key = SettingKeys.MessengerGreetingText,
                Value = greetingText
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        return _tenantProvider.BusinessId;
    }

    private static string ExtractMetaError(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? responseBody;
            }
        }
        catch
        {
            // ignore parse errors
        }

        return responseBody;
    }
}
