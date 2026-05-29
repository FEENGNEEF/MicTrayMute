using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MicTrayMute.Hotkeys;

internal sealed class GlobalHotkey : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4D32;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? Pressed;

    public GlobalHotkey()
    {
        CreateHandle(new CreateParams());
    }

    public void Register()
    {
        if (_registered)
        {
            return;
        }

        if (!RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, (uint)Keys.M))
        {
            throw new InvalidOperationException("Could not register Ctrl+Alt+M. Another application may already use it.");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        DestroyHandle();
        _disposed = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
