using System.IO;
using System.Text.Json;
using TvAudioWidget.Models;

namespace TvAudioWidget.Services.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public JsonSettingsStore()
        : this(Path.Combine(AppContext.BaseDirectory, "config.json"))
    {
    }

    public JsonSettingsStore(string path)
    {
        _path = path;
    }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(_path))
        {
            return new SettingsLoadResult(AppSettings.Default(), null);
        }

        try
        {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.Default();
            settings.Normalize();
            return new SettingsLoadResult(settings, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsLoadResult(AppSettings.Default(), $"配置读取失败，已使用默认设置：{ex.Message}");
        }
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        try
        {
            settings.Normalize();
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_path, json);
            return SettingsSaveResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SettingsSaveResult.Failed($"配置保存失败，请确认程序目录可写：{ex.Message}");
        }
    }
}
