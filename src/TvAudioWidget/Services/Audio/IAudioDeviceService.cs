using TvAudioWidget.Models;

namespace TvAudioWidget.Services.Audio;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioOutputDevice> ListOutputDevices();
    AudioOutputDevice? GetDefaultOutputDevice();
    void SetDefaultOutputDevice(string deviceId, bool includeCommunications = true);
    double GetMasterVolume();
    void SetMasterVolume(double value);
    bool GetMuted();
    void SetMuted(bool value);
}
