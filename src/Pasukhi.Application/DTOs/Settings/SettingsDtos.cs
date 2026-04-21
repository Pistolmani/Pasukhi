namespace Pasukhi.Application.DTOs.Settings;

public static class SettingKeys
{
    public const string AutoReplyEnabled = "auto_reply_enabled";
    public const string WorkingHoursEnabled = "working_hours_enabled";
    public const string WorkingHoursStart = "working_hours_start";
    public const string WorkingHoursEnd = "working_hours_end";
    public const string Timezone = "timezone";
}

public record BusinessSettingsDto(
    bool AutoReplyEnabled,
    bool WorkingHoursEnabled,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string Timezone);

public record UpdateBusinessSettingsRequest(
    bool AutoReplyEnabled,
    bool WorkingHoursEnabled,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string Timezone);
