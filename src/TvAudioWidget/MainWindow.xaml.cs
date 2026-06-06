using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TvAudioWidget.Models;
using TvAudioWidget.Services.Audio;
using TvAudioWidget.Services.Settings;
using TvAudioWidget.Services.Theming;
using TvAudioWidget.Services.Windowing;

namespace TvAudioWidget;

public partial class MainWindow : Window
{
    private const int WmDisplayChange = 0x007E;
    private const int WmExitSizeMove = 0x0232;
    private const double DeviceCardOuterMargin = 24;
    private const double DeviceCardTargetOuterWidth = 390;
    private const double DeviceCardMinWidth = 260;
    private const double DeviceCardMaxWidth = 420;

    private readonly IAudioDeviceService _audioService;
    private readonly ISettingsStore _settingsStore;
    private readonly WindowPlacementService _windowPlacementService;
    private readonly ThemeService _themeService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _screenChangeTimer;
    private readonly IReadOnlyList<ThemeDefinition> _themes;
    private AppSettings _settings = AppSettings.Default();
    private HwndSource? _windowSource;
    private string? _currentScreenDeviceName;
    private bool _uiReady;
    private bool _suppressSettingsSave;
    private bool _suppressVolumeChange;

    public MainWindow()
    {
        InitializeComponent();
        Resources["DeviceCardWidth"] = 360d;

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

        _screenChangeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _screenChangeTimer.Tick += (_, _) =>
        {
            _screenChangeTimer.Stop();
            CheckForScreenChange();
        };

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        LocationChanged += (_, _) => QueueScreenChangeCheck();
        StateChanged += MainWindow_StateChanged;

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
        _currentScreenDeviceName = _windowPlacementService.GetCurrentScreenDeviceName(this);
        _windowPlacementService.ApplyCurrentScreenMaximizedBounds(this);
        UpdateMaximizeRestoreButton();
        UpdateDeviceCardWidth();
        RefreshAudioState(showSuccess: false);
        _refreshTimer.Start();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowMessageHook);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
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
        if (sender is not System.Windows.Controls.Button { Tag: string deviceId })
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

        Dispatcher.BeginInvoke((Action)UpdateDeviceCardWidth, DispatcherPriority.Loaded);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
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

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            e.Handled = true;
            return;
        }

        BeginWindowDrag(e);
    }

    private void DeviceScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDeviceCardWidth();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            _windowPlacementService.ApplyCurrentScreenMaximizedBounds(this);
            CheckForScreenChange();
            Dispatcher.BeginInvoke((Action)UpdateDeviceCardWidth, DispatcherPriority.Loaded);
        }

        UpdateMaximizeRestoreButton();
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmExitSizeMove || msg == WmDisplayChange)
        {
            CheckForScreenChange();
            Dispatcher.BeginInvoke((Action)UpdateDeviceCardWidth, DispatcherPriority.Loaded);
        }

        return IntPtr.Zero;
    }

    private void BeginWindowDrag(MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindowForDrag(e);
        }

        try
        {
            DragMove();
            CheckForScreenChange();
        }
        catch (InvalidOperationException)
        {
            // DragMove can fail if the mouse capture is lost before WPF starts the drag.
        }
    }

    private void RestoreWindowForDrag(MouseButtonEventArgs e)
    {
        var headerPosition = e.GetPosition(this);
        var screenPosition = PointToScreen(headerPosition);
        var horizontalRatio = headerPosition.X / Math.Max(ActualWidth, 1);

        WindowState = WindowState.Normal;
        Left = screenPosition.X - Width * horizontalRatio;
        Top = screenPosition.Y - 28;
    }

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            return;
        }

        _windowPlacementService.ApplyCurrentScreenMaximizedBounds(this);
        WindowState = WindowState.Maximized;
    }

    private void UpdateMaximizeRestoreButton()
    {
        if (MaximizeRestoreButton is null)
        {
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            MaximizeRestoreButton.Content = "\uE923";
            MaximizeRestoreButton.ToolTip = "还原";
        }
        else
        {
            MaximizeRestoreButton.Content = "\uE922";
            MaximizeRestoreButton.ToolTip = "最大化";
        }
    }

    private void QueueScreenChangeCheck()
    {
        if (!IsLoaded || WindowState == WindowState.Minimized)
        {
            return;
        }

        _screenChangeTimer.Stop();
        _screenChangeTimer.Start();
    }

    private void CheckForScreenChange()
    {
        if (!IsLoaded || WindowState == WindowState.Minimized)
        {
            return;
        }

        var screenDeviceName = _windowPlacementService.GetCurrentScreenDeviceName(this);
        if (_currentScreenDeviceName is null)
        {
            _currentScreenDeviceName = screenDeviceName;
            _windowPlacementService.ApplyCurrentScreenMaximizedBounds(this);
            return;
        }

        if (string.Equals(_currentScreenDeviceName, screenDeviceName, StringComparison.OrdinalIgnoreCase))
        {
            _windowPlacementService.ApplyCurrentScreenMaximizedBounds(this);
            return;
        }

        _currentScreenDeviceName = screenDeviceName;
        _windowPlacementService.FitToCurrentScreen(this);
        UpdateDeviceCardWidth();
    }

    private void UpdateDeviceCardWidth()
    {
        var availableWidth = DeviceScrollViewer.ActualWidth;
        if (availableWidth <= 0)
        {
            return;
        }

        var columns = Math.Clamp((int)Math.Floor(availableWidth / DeviceCardTargetOuterWidth), 1, 8);
        var cardWidth = Math.Clamp(
            availableWidth / columns - DeviceCardOuterMargin,
            DeviceCardMinWidth,
            DeviceCardMaxWidth);

        Resources["DeviceCardWidth"] = cardWidth;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = source switch
            {
                FrameworkElement element => element.Parent,
                FrameworkContentElement contentElement => contentElement.Parent,
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(source),
                _ => null
            };
        }

        return null;
    }
}
