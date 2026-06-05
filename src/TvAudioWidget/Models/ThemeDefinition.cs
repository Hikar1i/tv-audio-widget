namespace TvAudioWidget.Models;

public sealed record ThemeDefinition(
    string Id,
    string Name,
    string BackgroundHex,
    string AccentHex,
    string AccentMutedHex,
    string TextHex);
