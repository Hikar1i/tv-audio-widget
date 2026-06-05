namespace TvAudioWidget.Models;

public sealed record AudioOutputDevice(
    string Id,
    string Name,
    string IconGlyph,
    bool IsDefault,
    bool IsDefaultCommunications);
