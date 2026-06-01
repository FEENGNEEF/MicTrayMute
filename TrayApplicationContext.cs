using MicTrayMute.Audio;
using MicTrayMute.Hotkeys;
using System.Diagnostics;
using System.Windows.Forms;

namespace MicTrayMute;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly CoreAudioMicrophoneController _microphone;
    private readonly GlobalHotkey _hotkey = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private bool _disposed;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _settings.IsMuted = true;

        _microphone = new CoreAudioMicrophoneController(
            _settings.DeviceNameContains,
            _settings.MuteAllCaptureDevices,
            _settings.PreferDefaultCaptureDevice,
            _settings.FallbackToDefaultCaptureDevice);

        _statusMenuItem = new ToolStripMenuItem { Enabled = false };
        _toggleMenuItem = new ToolStripMenuItem("Toggle microphone (Ctrl+Alt+M)", null, async (_, _) => await ToggleAsync());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(new ToolStripMenuItem("Mute now", null, async (_, _) => await SetMutedAsync(true, showNotification: true)));
        contextMenu.Items.Add(new ToolStripMenuItem("Open taskbar icon settings", null, (_, _) => OpenTaskbarSettings()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, async (_, _) => await ExitAsync()));

        _notifyIcon = new NotifyIcon
        {
            Text = "Mic Tray Mute",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += async (_, _) => await ToggleAsync();

        _hotkey.Pressed += async (_, _) => await ToggleAsync();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _hotkey.Register();
            await SetMutedAsync(true, showNotification: false);
            ShowNotification("Microphone muted", "The microphone was muted on startup.");
        }
        catch (Exception ex)
        {
            UpdateTrayIcon();
            ShowError("Startup failed", ex);
        }
    }

    private async Task ToggleAsync()
    {
        await SetMutedAsync(!_settings.IsMuted, showNotification: true);
    }

    private async Task SetMutedAsync(bool muted, bool showNotification)
    {
        await _stateLock.WaitAsync();
        try
        {
            _microphone.SetMuted(muted);
            _settings.IsMuted = muted;
            _settingsStore.Save(_settings);

            UpdateTrayIcon();

            if (showNotification)
            {
                ShowNotification(
                    muted ? "Microphone muted" : "Microphone active",
                    muted ? "Input is off." : "Input is on.");
            }
        }
        catch (Exception ex)
        {
            UpdateTrayIcon();
            ShowError("Toggle failed", ex);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void UpdateTrayIcon()
    {
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = TrayIconFactory.Create(_settings.IsMuted);
        oldIcon?.Dispose();

        _notifyIcon.Text = _settings.IsMuted
            ? "Mic Tray Mute - muted"
            : "Mic Tray Mute - active";
        _statusMenuItem.Text = _settings.IsMuted
            ? "Status: muted"
            : "Status: active";
        _toggleMenuItem.Text = _settings.IsMuted
            ? "Unmute (Ctrl+Alt+M)"
            : "Mute (Ctrl+Alt+M)";
    }

    private void ShowNotification(string title, string text)
    {
        if (!_settings.ShowNotifications)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(1500);
    }

    private void ShowError(string title, Exception ex)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = ex.Message;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private static void OpenTaskbarSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:taskbar",
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Open Windows Settings, then go to Personalization > Taskbar > Other system tray icons.",
                "Mic Tray Mute",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task ExitAsync()
    {
        await SetMutedAsync(true, showNotification: false);
        Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _hotkey.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _stateLock.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
