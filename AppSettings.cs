namespace MicTrayMute;

internal sealed class AppSettings
{
    public string DeviceNameContains { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public bool MuteAllCaptureDevices { get; set; } = true;
    public bool PreferDefaultCaptureDevice { get; set; } = true;
    public bool IsMuted { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool FallbackToDefaultCaptureDevice { get; set; } = true;
}
