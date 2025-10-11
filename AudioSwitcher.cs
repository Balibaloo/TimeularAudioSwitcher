using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), ClassInterface(ClassInterfaceType.None)]
public class MMDeviceEnumeratorComObject { }

public enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
    EDataFlow_enum_count = 3
}

[Flags]
public enum DeviceState : uint
{
    Active = 0x00000001,
    Disabled = 0x00000002,
    NotPresent = 0x00000004,
    Unplugged = 0x00000008,
    All = Active | Disabled | NotPresent | Unplugged
}

public enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator {
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask, out IMMDeviceCollection ppDevices);
    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-C0F27F4D1B4D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection {
    [PreserveSig]
    int GetCount(out uint pcDevices);
    [PreserveSig]
    int Item(uint nDevice, out IMMDevice ppDevice);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice {
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig]
    int OpenPropertyStore(int stgmAccess, out IPropertyStore propertyStore);
    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig]
    int GetState(out int pdwState);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore {
    [PreserveSig]
    int GetCount(out uint cProps);
    [PreserveSig]
    int GetAt(uint iProp, out PROPERTYKEY pkey);
    [PreserveSig]
    int GetValue(ref PROPERTYKEY key, out PropVariant pv);
    [PreserveSig]
    int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
    [PreserveSig]
    int Commit();
}

[StructLayout(LayoutKind.Sequential)]
public struct PROPERTYKEY {
    public Guid fmtid;
    public uint pid;
}

[StructLayout(LayoutKind.Explicit)]
public struct PropVariant {
    [FieldOffset(0)] public short vt;
    [FieldOffset(8)] public IntPtr pointerValue;

    public string GetString() {
        if (vt == 31 && pointerValue != IntPtr.Zero) // VT_LPWSTR
            return Marshal.PtrToStringUni(pointerValue);
        return null;
    }
}

// Complete interface definitions for PolicyConfig
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPolicyConfig {
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefault);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmft);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, out PropVariant pv);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, ref PropVariant pv);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
}

[ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPolicyConfigVista {
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefault);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmft);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, out PropVariant pv);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, ref PropVariant pv);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
}

public class AudioSwitcher {
    // PROPERTYKEY for device friendly name
    static PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14
    };

    private readonly object _cacheLock = new();
    private Dictionary<string, string> _renderByName = new(StringComparer.InvariantCultureIgnoreCase);
    private Dictionary<string, string> _captureByName = new(StringComparer.InvariantCultureIgnoreCase);
    private DateTime _cacheBuiltAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private object? _policyObj;
    private IPolicyConfig? _policyConfig;
    private IPolicyConfigVista? _policyConfigVista;

    private static bool NameMatches(string deviceName, string target) {
        if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(target)) return false;
        if (deviceName.Equals(target, StringComparison.InvariantCultureIgnoreCase)) return true;
        return deviceName.IndexOf(target, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }

    private void EnsurePolicyConfig() {
        if (_policyObj != null || _policyConfig != null || _policyConfigVista != null) return;
        try {
            var clsid = new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"); // PolicyConfigClient
            var type = Type.GetTypeFromCLSID(clsid);
            if (type == null) { Logger.Log("PolicyConfigClient class not found."); return; }
            _policyObj = Activator.CreateInstance(type);
            _policyConfig = _policyObj as IPolicyConfig;
            _policyConfigVista = _policyObj as IPolicyConfigVista;
        } catch (Exception ex) {
            Logger.Log("Failed to create PolicyConfig COM instance: " + ex.Message);
        }
    }

    private void BuildCache() {
        var render = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        var capture = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        try {
            using var mm = new MMDeviceEnumerator();
            // Render devices
            var outs = mm.EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
            for (int i = 0; i < outs.Count; i++) {
                try {
                    var d = outs[i];
                    var name = d.FriendlyName;
                    var id = d.ID;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                        render[name] = id;
                } catch { /* skip broken endpoint */ }
            }
            // Capture devices
            var ins = mm.EnumerateAudioEndPoints(DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active);
            for (int i = 0; i < ins.Count; i++) {
                try {
                    var d = ins[i];
                    var name = d.FriendlyName;
                    var id = d.ID;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                        capture[name] = id;
                } catch { /* skip broken endpoint */ }
            }
        } catch (Exception ex) {
            Logger.Log("BuildCache failed: " + ex.Message);
        }
        lock (_cacheLock) {
            _renderByName = render;
            _captureByName = capture;
            _cacheBuiltAt = DateTime.UtcNow;
        }
    }

    private void EnsureCache() {
        bool need;
        lock (_cacheLock) {
            need = (DateTime.UtcNow - _cacheBuiltAt) > CacheTtl || _renderByName.Count == 0 || _captureByName.Count == 0;
        }
        if (need) BuildCache();
    }

    private string? ResolveId(string friendlyName, DataFlow flow) {
        if (string.IsNullOrWhiteSpace(friendlyName)) return null;
        EnsureCache();
        Dictionary<string, string> dict;
        lock (_cacheLock) { dict = flow == DataFlow.Render ? _renderByName : _captureByName; }
        // exact or case-insensitive
        if (dict.TryGetValue(friendlyName, out var id)) return id;
        // substring fallback
        var kv = dict.FirstOrDefault(k => NameMatches(k.Key, friendlyName));
        if (!string.IsNullOrEmpty(kv.Key)) return kv.Value;
        // refresh once more in case of hot-plug
        BuildCache();
        lock (_cacheLock) { dict = flow == DataFlow.Render ? _renderByName : _captureByName; }
        if (dict.TryGetValue(friendlyName, out id)) return id;
        kv = dict.FirstOrDefault(k => NameMatches(k.Key, friendlyName));
        return string.IsNullOrEmpty(kv.Key) ? null : kv.Value;
    }

    private bool TrySetDefaultEndpoint(string id) {
        try {
            EnsurePolicyConfig();
            if (_policyConfig != null) {
                int hr = _policyConfig.SetDefaultEndpoint(id, ERole.eConsole);
                if (hr != 0) return false;
                _policyConfig.SetDefaultEndpoint(id, ERole.eMultimedia);
                _policyConfig.SetDefaultEndpoint(id, ERole.eCommunications);
                return true;
            }
            if (_policyConfigVista != null) {
                int hr = _policyConfigVista.SetDefaultEndpoint(id, ERole.eConsole);
                if (hr != 0) return false;
                _policyConfigVista.SetDefaultEndpoint(id, ERole.eMultimedia);
                _policyConfigVista.SetDefaultEndpoint(id, ERole.eCommunications);
                return true;
            }
            Logger.Log("No PolicyConfig interface available.");
        }
        catch (Exception ex) {
            Logger.Log("TrySetDefaultEndpoint failed: " + ex.Message);
        }
        return false;
    }

    private static bool IsAlreadyDefault(string deviceId, DataFlow flow) {
        try {
            using var mm = new MMDeviceEnumerator();
            var df = flow == DataFlow.Render ? DataFlow.Render : DataFlow.Capture;
            var def = mm.GetDefaultAudioEndpoint(df, Role.Multimedia);
            return def != null && string.Equals(def.ID, deviceId, StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }

    public bool SetDefaultPlaybackDevice(string friendlyName) {
        if (string.IsNullOrWhiteSpace(friendlyName)) return true; // nothing to do
        try {
            var id = ResolveId(friendlyName, DataFlow.Render);
            if (id == null) { Logger.Log($"Playback device not found: '{friendlyName}'"); return false; }
            if (IsAlreadyDefault(id, DataFlow.Render)) return true;
            return TrySetDefaultEndpoint(id);
        } catch (Exception ex) {
            Logger.Log("SetDefaultPlaybackDevice failed: " + ex.Message);
            return false;
        }
    }

    public bool SetDefaultInputDevice(string friendlyName) {
        if (string.IsNullOrWhiteSpace(friendlyName)) return true; // nothing to do
        try {
            var id = ResolveId(friendlyName, DataFlow.Capture);
            if (id == null) { Logger.Log($"Input device not found: '{friendlyName}'"); return false; }
            if (IsAlreadyDefault(id, DataFlow.Capture)) return true;
            return TrySetDefaultEndpoint(id);
        } catch (Exception ex) {
            Logger.Log("SetDefaultInputDevice failed: " + ex.Message);
            return false;
        }
    }

    public void LogAllDeviceNames() {
        try {
            using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.All);
            Logger.Log($"Found {devices.Count} playback devices (all states):");
            for (int i = 0; i < devices.Count; i++) {
                try {
                    var dev = devices[i];
                    var id = dev.ID;
                    var name = dev.FriendlyName;
                    var state = dev.State;
                    Logger.Log($"Device: id={id}, name='{name}', state={state}");
                } catch (Exception devEx) {
                    Logger.Log($"Device index {i}: failed to read properties: {devEx.Message}");
                }
            }
        }
        catch (Exception exNA) {
            Logger.Log($"NAudio enumeration failed: {exNA.Message}");
        }
    }
}
