namespace TvAudioWidget.Models;

public sealed class AppSettings
{
    public string ThemeId { get; set; } = "sage";
    public double PanelOpacity { get; set; } = 0.88;
    public string? LastScreenDeviceName { get; set; }
    public string WindowMode { get; set; } = "Large";
    public double VolumeStepPercent { get; set; } = 5;

    public static AppSettings Default() => new();

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(ThemeId))
        {
            ThemeId = "sage";
        }

        PanelOpacity = Math.Clamp(PanelOpacity, 0.58, 0.96);
        WindowMode = string.IsNullOrWhiteSpace(WindowMode) ? "Large" : WindowMode;
        VolumeStepPercent = Math.Clamp(VolumeStepPercent, 1, 20);
    }
}
