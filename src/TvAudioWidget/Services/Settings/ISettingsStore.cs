using TvAudioWidget.Models;

namespace TvAudioWidget.Services.Settings;

public interface ISettingsStore
{
    SettingsLoadResult Load();
    SettingsSaveResult Save(AppSettings settings);
}

public sealed record SettingsLoadResult(AppSettings Settings, string? ErrorMessage);

public sealed record SettingsSaveResult(bool Success, string? ErrorMessage)
{
    public static SettingsSaveResult Ok() => new(true, null);
    public static SettingsSaveResult Failed(string message) => new(false, message);
}
