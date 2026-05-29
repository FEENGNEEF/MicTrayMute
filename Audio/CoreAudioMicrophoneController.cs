using System.Runtime.InteropServices;

namespace MicTrayMute.Audio;

internal sealed class CoreAudioMicrophoneController
{
    private readonly string _deviceNameContains;
    private readonly bool _preferDefaultCaptureDevice;
    private readonly bool _fallbackToDefaultCaptureDevice;

    public CoreAudioMicrophoneController(
        string deviceNameContains,
        bool preferDefaultCaptureDevice,
        bool fallbackToDefaultCaptureDevice)
    {
        _deviceNameContains = deviceNameContains;
        _preferDefaultCaptureDevice = preferDefaultCaptureDevice;
        _fallbackToDefaultCaptureDevice = fallbackToDefaultCaptureDevice;
    }

    public void SetMuted(bool muted)
    {
        using var endpoint = GetEndpointVolume();
        var context = Guid.Empty;
        Marshal.ThrowExceptionForHR(endpoint.Volume.SetMute(muted, ref context));
    }

    public bool GetMuted()
    {
        using var endpoint = GetEndpointVolume();
        Marshal.ThrowExceptionForHR(endpoint.Volume.GetMute(out var muted));
        return muted;
    }

    private EndpointVolumeHandle GetEndpointVolume()
    {
        IMMDevice? device = null;
        IMMDeviceEnumerator? enumerator = null;

        try
        {
            enumerator = CreateDeviceEnumerator();

            if (_preferDefaultCaptureDevice)
            {
                device = GetDefaultCaptureDevice(enumerator);
            }
            else
            {
                device = FindCaptureDevice(enumerator);
            }

            if (device is null && _fallbackToDefaultCaptureDevice)
            {
                device = GetDefaultCaptureDevice(enumerator);
            }

            if (device is null)
            {
                throw new InvalidOperationException(
                    $"No active capture device matching '{_deviceNameContains}' was found.");
            }

            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(
                ref iid,
                ClsCtx.All,
                IntPtr.Zero,
                out var endpointObject));

            return new EndpointVolumeHandle((IAudioEndpointVolume)endpointObject, device, enumerator);
        }
        catch
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            if (enumerator is not null)
            {
                Marshal.ReleaseComObject(enumerator);
            }

            throw;
        }
    }

    private IMMDevice? FindCaptureDevice(IMMDeviceEnumerator enumerator)
    {
        IMMDeviceCollection? collection = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(
                EDataFlow.Capture,
                DeviceState.Active,
                out collection));

            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

            for (uint i = 0; i < count; i++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(i, out var device));
                try
                {
                    var friendlyName = GetFriendlyName(device);
                    if (friendlyName.Contains(_deviceNameContains, StringComparison.OrdinalIgnoreCase))
                    {
                        return device;
                    }
                }
                catch
                {
                    Marshal.ReleaseComObject(device);
                    throw;
                }

                Marshal.ReleaseComObject(device);
            }

            return null;
        }
        finally
        {
            if (collection is not null)
            {
                Marshal.ReleaseComObject(collection);
            }
        }
    }

    private static IMMDevice GetDefaultCaptureDevice(IMMDeviceEnumerator enumerator)
    {
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(
            EDataFlow.Capture,
            ERole.Communications,
            out var device));
        return device;
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        IPropertyStore? propertyStore = null;
        try
        {
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(Stgm.Read, out propertyStore));

            var propertyKey = PropertyKeys.DeviceFriendlyName;
            Marshal.ThrowExceptionForHR(propertyStore.GetValue(ref propertyKey, out var propVariant));
            try
            {
                return propVariant.GetString() ?? string.Empty;
            }
            finally
            {
                propVariant.Clear();
            }
        }
        finally
        {
            if (propertyStore is not null)
            {
                Marshal.ReleaseComObject(propertyStore);
            }
        }
    }

    private static IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))
            ?? throw new InvalidOperationException("Could not resolve Windows audio device enumerator COM type.");
        return (IMMDeviceEnumerator)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create Windows audio device enumerator."));
    }

    private sealed class EndpointVolumeHandle : IDisposable
    {
        private readonly IMMDevice _device;
        private readonly IMMDeviceEnumerator _enumerator;
        private bool _disposed;

        public EndpointVolumeHandle(IAudioEndpointVolume volume, IMMDevice device, IMMDeviceEnumerator enumerator)
        {
            Volume = volume;
            _device = device;
            _enumerator = enumerator;
        }

        public IAudioEndpointVolume Volume { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Marshal.ReleaseComObject(Volume);
            Marshal.ReleaseComObject(_device);
            Marshal.ReleaseComObject(_enumerator);
            _disposed = true;
        }
    }
}

internal enum EDataFlow
{
    Render,
    Capture,
    All
}

internal enum ERole
{
    Console,
    Multimedia,
    Communications
}

[Flags]
internal enum DeviceState
{
    Active = 0x00000001
}

[Flags]
internal enum ClsCtx
{
    All = 23
}

internal enum Stgm
{
    Read = 0
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(
        EDataFlow dataFlow,
        DeviceState dwStateMask,
        [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection ppDevices);

    int GetDefaultAudioEndpoint(
        EDataFlow dataFlow,
        ERole role,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppEndpoint);

    int GetDevice(
        [MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-C0BB2BA96A22")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out uint pcDevices);
    int Item(uint nDevice, out IMMDevice ppDevice);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, ClsCtx dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    int OpenPropertyStore(Stgm stgmAccess, out IPropertyStore ppProperties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    int GetState(out DeviceState pdwState);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out uint cProps);
    int GetAt(uint iProp, out PropertyKey pkey);
    int GetValue(ref PropertyKey key, out PropVariant pv);
    int SetValue(ref PropertyKey key, ref PropVariant propvar);
    int Commit();
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(out uint pnChannelCount);
    int SetMasterVolumeLevel(float fLevelDb, ref Guid pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    int GetMasterVolumeLevel(out float pfLevelDb);
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int SetChannelVolumeLevel(uint nChannel, float fLevelDb, ref Guid pguidEventContext);
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDb);
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    int VolumeStepUp(ref Guid pguidEventContext);
    int VolumeStepDown(ref Guid pguidEventContext);
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;

    public PropertyKey(Guid formatId, int propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

internal static class PropertyKeys
{
    public static PropertyKey DeviceFriendlyName =>
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    private ushort _valueType;
    private ushort _reserved1;
    private ushort _reserved2;
    private ushort _reserved3;
    private IntPtr _value;
    private IntPtr _value2;

    public string? GetString()
    {
        const ushort vtLpwstr = 31;
        return _valueType == vtLpwstr ? Marshal.PtrToStringUni(_value) : null;
    }

    public void Clear()
    {
        PropVariantClear(ref this);
    }

    [DllImport("Ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);
}
