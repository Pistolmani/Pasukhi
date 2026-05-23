namespace Pasukhi.Infrastructure.Services;

public static class KeyValueSettingsReader
{
    public static bool ReadBool(Dictionary<string, string> settings, string key, bool defaultValue) =>
        settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    public static string ReadString(Dictionary<string, string> settings, string key, string defaultValue) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
}
