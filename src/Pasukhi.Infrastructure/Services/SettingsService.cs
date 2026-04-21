using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string DefaultWorkingHoursStart = "09:00";
    private const string DefaultWorkingHoursEnd = "18:00";
    private const string DefaultTimezone = "Asia/Tbilisi";
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public SettingsService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<BusinessSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await _db.BusinessSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return Map(settings);
    }

    public async Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureTenant();
        var settings = await _db.BusinessSettings.ToDictionaryAsync(s => s.Key, ct);

        Upsert(settings, businessId, SettingKeys.AutoReplyEnabled, request.AutoReplyEnabled.ToString());
        Upsert(settings, businessId, SettingKeys.WorkingHoursEnabled, request.WorkingHoursEnabled.ToString());
        Upsert(settings, businessId, SettingKeys.WorkingHoursStart, NormalizeTime(request.WorkingHoursStart, DefaultWorkingHoursStart));
        Upsert(settings, businessId, SettingKeys.WorkingHoursEnd, NormalizeTime(request.WorkingHoursEnd, DefaultWorkingHoursEnd));
        Upsert(settings, businessId, SettingKeys.Timezone, NormalizeText(request.Timezone, DefaultTimezone));

        await _db.SaveChangesAsync(ct);

        return Map(settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value));
    }

    private static BusinessSettingsDto Map(Dictionary<string, string> settings) =>
        new(
            ReadBool(settings, SettingKeys.AutoReplyEnabled, defaultValue: true),
            ReadBool(settings, SettingKeys.WorkingHoursEnabled, defaultValue: false),
            ReadString(settings, SettingKeys.WorkingHoursStart, DefaultWorkingHoursStart),
            ReadString(settings, SettingKeys.WorkingHoursEnd, DefaultWorkingHoursEnd),
            ReadString(settings, SettingKeys.Timezone, DefaultTimezone));

    private static bool ReadBool(Dictionary<string, string> settings, string key, bool defaultValue) =>
        settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    private static string ReadString(Dictionary<string, string> settings, string key, string defaultValue) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;

    private void Upsert(Dictionary<string, BusinessSetting> settings, Guid businessId, string key, string value)
    {
        if (settings.TryGetValue(key, out var setting))
        {
            setting.Value = value;
            return;
        }

        setting = new BusinessSetting
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Key = key,
            Value = value
        };
        _db.BusinessSettings.Add(setting);
        settings[key] = setting;
    }

    private static string NormalizeTime(string value, string fallback) =>
        TimeOnly.TryParse(value, out var time) ? time.ToString("HH:mm") : fallback;

    private static string NormalizeText(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return _tenantProvider.BusinessId;
    }
}
