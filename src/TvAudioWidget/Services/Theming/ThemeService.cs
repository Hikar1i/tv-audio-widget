using System.Windows.Media;
using TvAudioWidget.Models;

namespace TvAudioWidget.Services.Theming;

public sealed class ThemeService
{
    private readonly IReadOnlyList<ThemeDefinition> _themes =
    [
        new("sage", "鼠尾草绿", "#1D2522", "#9DB8A4", "#64796C", "#F4F7F2"),
        new("mist", "雾蓝", "#1F2730", "#9EB4C8", "#627489", "#F5F7FA"),
        new("rose", "灰玫瑰", "#2B2225", "#C8A0A9", "#86646D", "#FFF6F8"),
        new("clay", "陶土", "#29241F", "#C3A27E", "#7E6952", "#FFF8EE"),
        new("stone", "石墨", "#202124", "#B8B8AE", "#73736B", "#F7F7F1")
    ];

    public IReadOnlyList<ThemeDefinition> GetThemes() => _themes;

    public ThemeDefinition GetTheme(string? id)
    {
        return _themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? _themes[0];
    }

    public static SolidColorBrush BrushFromHex(string hex, double opacity = 1)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
