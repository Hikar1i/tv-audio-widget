using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using TvAudioWidget.Models;

namespace TvAudioWidget.Services.Audio;

public sealed class WindowsAudioDeviceService : IAudioDeviceService
{
    public IReadOnlyList<AudioOutputDevice> ListOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultDeviceId = TryGetDefaultDeviceId(enumerator, Role.Multimedia);
        var defaultCommunicationsId = TryGetDefaultDeviceId(enumerator, Role.Communications);
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        return devices
            .Select(device => CreateDeviceModel(device, defaultDeviceId, defaultCommunicationsId))
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public AudioOutputDevice? GetDefaultOutputDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultDeviceId = TryGetDefaultDeviceId(enumerator, Role.Multimedia);
        if (defaultDeviceId is null)
        {
            return null;
        }

        using var device = enumerator.GetDevice(defaultDeviceId);
        var defaultCommunicationsId = TryGetDefaultDeviceId(enumerator, Role.Communications);
        return CreateDeviceModel(device, defaultDeviceId, defaultCommunicationsId);
    }

    public void SetDefaultOutputDevice(string deviceId, bool includeCommunications = true)
    {
        object? policyObject = null;
        try
        {
            policyObject = new PolicyConfigClient();
            var policyConfig = (IPolicyConfig)policyObject;

            ThrowIfFailed(policyConfig.SetDefaultEndpoint(deviceId, EndpointRole.Console));
            ThrowIfFailed(policyConfig.SetDefaultEndpoint(deviceId, EndpointRole.Multimedia));
            if (includeCommunications)
            {
                ThrowIfFailed(policyConfig.SetDefaultEndpoint(deviceId, EndpointRole.Communications));
            }
        }
        finally
        {
            if (policyObject is not null && Marshal.IsComObject(policyObject))
            {
                Marshal.ReleaseComObject(policyObject);
            }
        }
    }

    public double GetMasterVolume()
    {
        using var device = GetDefaultEndpoint();
        return device.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public void SetMasterVolume(double value)
    {
        using var device = GetDefaultEndpoint();
        device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(value, 0, 1);
    }

    public bool GetMuted()
    {
        using var device = GetDefaultEndpoint();
        return device.AudioEndpointVolume.Mute;
    }

    public void SetMuted(bool value)
    {
        using var device = GetDefaultEndpoint();
        device.AudioEndpointVolume.Mute = value;
    }

    private static MMDevice GetDefaultEndpoint()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    private static string? TryGetDefaultDeviceId(MMDeviceEnumerator enumerator, Role role)
    {
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
            return device.ID;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static AudioOutputDevice CreateDeviceModel(
        MMDevice device,
        string? defaultDeviceId,
        string? defaultCommunicationsId)
    {
        return new AudioOutputDevice(
            device.ID,
            device.FriendlyName,
            InferIconGlyph(device.FriendlyName),
            string.Equals(device.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase),
            string.Equals(device.ID, defaultCommunicationsId, StringComparison.OrdinalIgnoreCase));
    }

    private static string InferIconGlyph(string name)
    {
        if (ContainsAny(name, "headphone", "headset", "耳机", "耳麦"))
        {
            return "\uE7F6";
        }

        if (ContainsAny(name, "hdmi", "display", "monitor", "tv", "television", "nvidia", "amd", "intel", "电视", "显示器"))
        {
            return "\uE7F4";
        }

        return "\uE767";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.CurrentCultureIgnoreCase));
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }
}
