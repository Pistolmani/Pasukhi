using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class SettingsServiceTests
{
    [Fact]
    public async Task GetAsync_returns_defaults_when_settings_are_missing()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var service = new SettingsService(db, new StubTenantProvider(businessId));

        var result = await service.GetAsync();

        Assert.True(result.AutoReplyEnabled);
        Assert.False(result.WorkingHoursEnabled);
        Assert.Equal("09:00", result.WorkingHoursStart);
        Assert.Equal("18:00", result.WorkingHoursEnd);
        Assert.Equal("Asia/Tbilisi", result.Timezone);
    }

    [Fact]
    public async Task UpdateAsync_upserts_known_settings_and_returns_saved_values()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var service = new SettingsService(db, new StubTenantProvider(businessId));

        var result = await service.UpdateAsync(new UpdateBusinessSettingsRequest(
            AutoReplyEnabled: false,
            WorkingHoursEnabled: true,
            WorkingHoursStart: "10:15",
            WorkingHoursEnd: "19:45",
            Timezone: "Europe/Berlin"));

        Assert.False(result.AutoReplyEnabled);
        Assert.True(result.WorkingHoursEnabled);
        Assert.Equal("10:15", result.WorkingHoursStart);
        Assert.Equal("19:45", result.WorkingHoursEnd);
        Assert.Equal("Europe/Berlin", result.Timezone);
        Assert.Equal(5, await db.BusinessSettings.CountAsync());

        var updated = await service.UpdateAsync(new UpdateBusinessSettingsRequest(
            AutoReplyEnabled: true,
            WorkingHoursEnabled: false,
            WorkingHoursStart: "bad-time",
            WorkingHoursEnd: "",
            Timezone: "  Asia/Tbilisi  "));

        Assert.True(updated.AutoReplyEnabled);
        Assert.False(updated.WorkingHoursEnabled);
        Assert.Equal("09:00", updated.WorkingHoursStart);
        Assert.Equal("18:00", updated.WorkingHoursEnd);
        Assert.Equal("Asia/Tbilisi", updated.Timezone);
        Assert.Equal(5, await db.BusinessSettings.CountAsync());
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid businessId)
        {
            BusinessId = businessId;
        }

        public Guid BusinessId { get; }
    }
}
