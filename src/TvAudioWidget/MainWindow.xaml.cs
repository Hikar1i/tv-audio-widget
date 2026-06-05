using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TvAudioWidget.Models;
using TvAudioWidget.Services.Audio;
using TvAudioWidget.Services.Settings;
using TvAudioWidget.Services.Theming;
using TvAudioWidget.Services.Windowing;

namespace TvAudioWidget;

public partial class MainWindow : Window
{
    private readonly IAudioDeviceService _audioService;
    private readonly ISettingsStore _settingsStore;
    private readonly WindowPlacementService _windowPlacementService;
    private readonly ThemeService _themeService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly IReadOnlyList<ThemeDefinition> _themes;
    private AppSettings _settings = AppSettings.Default();
    private bool _uiReady;
    private bool _suppressSettingsSave;
    private bool _suppressVolumeChange;

    public MainWindow()
    {
        InitializeComponent();

        _audioService = new WindowsAudioDeviceService();
        _settingsStore = new JsonSettingsStore();
        _windowPlacementService = new WindowPlacementService();
        _themeService = new ThemeService();
        _themes = _themeService.GetThemes();

        var loadResult = _settingsStore.Load();
        _settings = loadResult.Settings;
        _windowPlacementService.ApplyStartupPlacement(this, _settings.LastScreenDeviceName);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _refreshTimer.Tick += (_, _) => RefreshAudioState(showSuccess: false);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        InitializeSettingsUi();
        ApplyTheme();
        _uiReady = true;
        if (loadResult.ErrorMessage is not null)
        {
            SetStatus(loadResult.ErrorMessage, isError: true);
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshAudioState(showSuccess: false);
        _refreshTimer.Start();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _settings.LastScreenDeviceName = _windowPlacementService.GetCurrentScreenDeviceName(this);
        _ = _settingsStore.Save(_settings);
    }

    private void InitializeSettingsUi()
    {
        _suppressSettingsSave = true;
        ThemeSelector.ItemsSource = _themes;
        ThemeSelector.SelectedValue = _themeService.GetTheme(_settings.ThemeId).Id;
        PanelOpacitySlider.Value = _settings.PanelOpacity;
        VolumeStepSlider.Value = _settings.VolumeStepPercent;
        _suppressSettingsSave = false;

        UpdateSettingsLabels();
    }

    private void ApplyTheme()
    {
        var theme = _themeService.GetTheme(_settings.ThemeId);
        Resources["RootBrush"] = ThemeService.BrushFromHex(theme.BackgroundHex, _settings.PanelOpacity);
        Resources["CardBrush"] = ThemeService.BrushFromHex(theme.BackgroundHex, Math.Min(_settings.PanelOpacity + 0.08, 0.98));
        Resources["ControlBrush"] = ThemeService.BrushFromHex(theme.AccentMutedHex, 0.36);
        Resources["AccentBrush"] = ThemeService.BrushFromHex(theme.AccentHex);
        Resources["AccentMutedBrush"] = ThemeService.BrushFromHex(theme.AccentMutedHex, 0.74);
        Resources["TextBrush"] = ThemeService.BrushFromHex(theme.TextHex);
        Resources["MutedTextBrush"] = ThemeService.BrushFromHex(theme.TextHex, 0.74);
    }

    private void RefreshAudioState(bool showSuccess)
    {
        try
        {
            var devices = _audioService.ListOutputDevices();
            DeviceItems.ItemsSource = devices;

            var defaultDevice = devices.FirstOrDefault(device => device.IsDefault);
            CurrentDeviceText.Text = defaultDevice is null
                ? "未检测到默认输出设备"
                : $"当前默认输出：{defaultDevice.Name}";

            _suppressVolumeChange = true;
            var volume = Math.Round(_audioService.GetMasterVolume() * 100);
            MasterVolumeSlider.Value = volume;
            VolumeText.Text = $"音量 {volume:0}%";
            MuteButton.Content = _audioService.GetMuted() ? "取消静音" : "静音";
            _suppressVolumeChange = false;

            if (showSuccess)
            {
                SetStatus("音频状态已刷新。");
            }
        }
        catch (Exception ex)
        {
            _suppressVolumeChange = false;
            SetStatus($"音频状态读取失败：{ex.Message}", isError: true);
        }
    }

    private void SaveSettings()
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        var result = _settingsStore.Save(_settings);
        if (!result.Success && result.ErrorMessage is not null)
        {
            SetStatus(result.ErrorMessage, isError: true);
        }
        else
        {
            SetStatus("设置已保存。");
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? ThemeService.BrushFromHex("#FFD6D6")
            : (System.Windows.Media.Brush)Resources["MutedTextBrush"];
    }

    private void UpdateSettingsLabels()
    {
        OpacityValueText.Text = $"{Math.Round(_settings.PanelOpacity * 100):0}%";
        VolumeStepValueText.Text = $"{_settings.VolumeStepPercent:0}%";
    }

    private void SetVolume(double percent)
    {
        var clampedPercent = Math.Clamp(percent, 0, 100);
        try
        {
            _audioService.SetMasterVolume(clampedPercent / 100);

            _suppressVolumeChange = true;
            MasterVolumeSlider.Value = clampedPercent;
            VolumeText.Text = $"音量 {clampedPercent:0}%";
            _suppressVolumeChange = false;
            SetStatus("音量已更新。");
        }
        catch (Exception ex)
        {
            _suppressVolumeChange = false;
            SetStatus($"音量设置失败：{ex.Message}", isError: true);
        }
    }

    private void ChangeVolume(double deltaPercent)
    {
        SetVolume(MasterVolumeSlider.Value + deltaPercent);
    }

    private void DeviceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string deviceId })
        {
            return;
        }

        try
        {
            _audioService.SetDefaultOutputDevice(deviceId, includeCommunications: true);
            RefreshAudioState(showSuccess: false);
            SetStatus("默认输出设备已切换。");
        }
        catch (Exception ex)
        {
            SetStatus($"设备切换失败：{ex.Message}", isError: true);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAudioState(showSuccess: true);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void VolumeDownButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeVolume(-_settings.VolumeStepPercent);
    }

    private void VolumeUpButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeVolume(_settings.VolumeStepPercent);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var nextMuted = !_audioService.GetMuted();
            _audioService.SetMuted(nextMuted);
            MuteButton.Content = nextMuted ? "取消静音" : "静音";
            SetStatus(nextMuted ? "已静音。" : "已取消静音。");
        }
        catch (Exception ex)
        {
            SetStatus($"静音切换失败：{ex.Message}", isError: true);
        }
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady)
        {
            return;
        }

        VolumeText.Text = $"音量 {Math.Round(e.NewValue):0}%";
        if (!_suppressVolumeChange)
        {
            SetVolume(e.NewValue);
        }
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSettingsSave || ThemeSelector.SelectedValue is not string themeId)
        {
            return;
        }

        _settings.ThemeId = themeId;
        ApplyTheme();
        SaveSettings();
    }

    private void PanelOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady)
        {
            return;
        }

        _settings.PanelOpacity = Math.Round(e.NewValue, 2);
        UpdateSettingsLabels();
        ApplyTheme();
        SaveSettings();
    }

    private void VolumeStepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady)
        {
            return;
        }

        _settings.VolumeStepPercent = Math.Round(e.NewValue);
        UpdateSettingsLabels();
        SaveSettings();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Down:
                ChangeVolume(-_settings.VolumeStepPercent);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                ChangeVolume(_settings.VolumeStepPercent);
                e.Handled = true;
                break;
        }
    }
}
