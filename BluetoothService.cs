using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Microsoft.Win32;

public enum BleStatus
{
    Idle,
    Connecting,
    Connected,
    ServicesDiscovered,
    Subscribed,
    Listening,
    Cancelled,
    Error,
    Disconnected
}

public class BluetoothService {
    private readonly AppConfig _cfg;
    private readonly AudioSwitcher _audio;
    private readonly CancellationTokenSource _cts = new();

    private BleStatus _status = BleStatus.Idle;
    public BleStatus Status => _status;

    public event Action<BleStatus, string>? StatusChanged;
    public event Action<int>? SideChanged;

    // Runtime BLE references and reconnection helpers
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _sideChar;
    private GattCharacteristic? _ctrlChar;
    private GattSession? _gattSession;
    private TaskCompletionSource<object?>? _connectionLostTcs;
    private CancellationTokenSource? _reconnectDelayCts;

    public BluetoothService(AppConfig cfg, AudioSwitcher audio) {
        _cfg = cfg;
        _audio = audio;
        try { SystemEvents.PowerModeChanged += OnPowerModeChanged; } catch { }
    }

    private void SetStatus(BleStatus status, string message)
    {
        _status = status;
        try { StatusChanged?.Invoke(status, message); } catch { }
        try { Logger.Log($"BLE status: {status} - {message}"); } catch { }
    }

    public void Stop()
    {
        try { _cts.Cancel(); } catch { }
    }

    public async Task RunAsync() {
        var backoffSeconds = new int[] { 1, 2, 5, 10, 20, 30 };
        int backoffIndex = 0;

        try {
            while (!_cts.IsCancellationRequested)
            {
                SetStatus(BleStatus.Connecting, "Connecting to device...");
                bool connected = false;
                try
                {
                    connected = await ConnectAndSubscribeAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    SetStatus(BleStatus.Error, ex.Message);
                    Logger.Log($"ConnectAndSubscribe error: {ex}");
                    connected = false;
                }

                if (!connected)
                {
                    // Backoff then retry
                    int delay = backoffSeconds[Math.Min(backoffIndex, backoffSeconds.Length - 1)];
                    backoffIndex = Math.Min(backoffIndex + 1, backoffSeconds.Length - 1);
                    Logger.Log($"Reconnect in {delay}s...");
                    _reconnectDelayCts = new CancellationTokenSource();
                    try { await Task.Delay(TimeSpan.FromSeconds(delay), CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _reconnectDelayCts.Token).Token); } catch { }
                    continue;
                }

                // Connected and subscribed; reset backoff
                backoffIndex = 0;
                SetStatus(BleStatus.Listening, "Listening for side changes...");

                _connectionLostTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var reg = _cts.Token.Register(() => _connectionLostTcs.TrySetCanceled());

                try { await _connectionLostTcs.Task; }
                catch (OperationCanceledException) { throw; }
                catch { /* swallow */ }

                // Clean up and retry
                CleanupConnection();
                if (!_cts.IsCancellationRequested)
                {
                    SetStatus(BleStatus.Disconnected, "Connection lost, retrying...");
                }
            }
        }
        catch (OperationCanceledException) {
            SetStatus(BleStatus.Cancelled, "Bluetooth service cancelled");
            Logger.Log("Bluetooth service cancelled.");
        }
        catch (Exception ex) {
            SetStatus(BleStatus.Error, ex.Message);
            Logger.Log($"Fatal error in BluetoothService: {ex}");
        }
        finally
        {
            CleanupConnection();
            SetStatus(BleStatus.Disconnected, "Service stopped");
        }
    }

    private async Task<bool> ConnectAndSubscribeAsync(CancellationToken token)
    {
        // --- Connect ---
        ulong address = Convert.ToUInt64(_cfg.BluetoothAddress, 16);
        Logger.Log($"Connecting to device @ {address:X}");
        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(token);
        if (_device == null)
        {
            SetStatus(BleStatus.Error, "Failed to create BluetoothLEDevice");
            Logger.Log("Failed to create BluetoothLEDevice.");
            return false;
        }
        _device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
        SetStatus(BleStatus.Connected, $"Connected: {_device.Name}");
        Logger.Log($"Connected to device: {_device.Name}, Id: {_device.DeviceId}");

        // --- Discover all services ---
        var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(token);
        if (servicesResult.Status != GattCommunicationStatus.Success)
        {
            SetStatus(BleStatus.Error, $"GetGattServices failed: {servicesResult.Status}");
            Logger.Log($"GetGattServicesAsync failed: {servicesResult.Status}");
            return false;
        }
        SetStatus(BleStatus.ServicesDiscovered, $"Services discovered: {servicesResult.Services.Count}");

        // --- Parse characteristic UUID from config ---
        string uuidRaw = _cfg.CharacteristicUuid?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(uuidRaw) || !Guid.TryParse(uuidRaw, out var targetCharUuid))
        {
            SetStatus(BleStatus.Error, "Invalid characteristic UUID in config");
            Logger.Log($"Invalid or missing characteristicUuid in config: '{_cfg.CharacteristicUuid}' (after trim: '{uuidRaw}')");
            return false;
        }
        Logger.Log($"Configured characteristic UUID: {targetCharUuid}");

        _sideChar = null;
        _ctrlChar = null;
        foreach (var svc in servicesResult.Services)
        {
            var charsResult = await svc.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(token);
            if (charsResult.Status != GattCommunicationStatus.Success)
                continue;

            foreach (var ch in charsResult.Characteristics)
            {
                Logger.Log($"Found characteristic: {ch.Uuid} properties: {ch.CharacteristicProperties}");

                if (ch.Uuid == targetCharUuid)
                    _sideChar = ch;

                if (ch.Uuid == Guid.Parse("c7e70001-c847-11e6-8175-8c89a55d403c"))
                    _ctrlChar = ch;
            }
        }

        // --- Optional handshake ---
        if (_ctrlChar != null)
        {
            try
            {
                Logger.Log($"Sending handshake to control characteristic {_ctrlChar.Uuid}");
                var handshake = new byte[] { 0x01 };
                await _ctrlChar.WriteValueAsync(handshake.AsBuffer()).AsTask(token);
                Logger.Log("Sent handshake 0x01 to control characteristic.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send handshake: {ex.Message}");
            }
        }
        else
        {
            Logger.Log("No control characteristic found, continuing without handshake.");
        }

        if (_sideChar == null)
        {
            SetStatus(BleStatus.Error, $"Target characteristic {targetCharUuid} not found");
            Logger.Log($"Could not find target characteristic {targetCharUuid}");
            return false;
        }

        Logger.Log($"Found target characteristic: {_sideChar.Uuid} with properties {_sideChar.CharacteristicProperties}");

        // Ensure we maintain connection if possible via GATT session
        try
        {
            _gattSession = _sideChar.Service?.Session;
            if (_gattSession != null)
            {
                _gattSession.MaintainConnection = true;
                _gattSession.SessionStatusChanged += GattSession_SessionStatusChanged;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"GattSession setup failed or unsupported: {ex.Message}");
        }

        // --- Subscribe using Indicate ---
        _sideChar.ValueChanged += Characteristic_ValueChanged;
        var status = await _sideChar.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Indicate).AsTask(token);

        if (status != GattCommunicationStatus.Success)
        {
            SetStatus(BleStatus.Error, $"Enable indications failed: {status}");
            Logger.Log($"Failed to enable indications: {status}");
            return false;
        }

        SetStatus(BleStatus.Subscribed, "Indications enabled");
        Logger.Log("Indications enabled for side changes.");
        return true;
    }

    private void GattSession_SessionStatusChanged(GattSession sender, GattSessionStatusChangedEventArgs args)
    {
        try
        {
            Logger.Log($"GATT session status changed: {args.Status}, ErrorStatus: {args.Error}");
            if (args.Status == GattSessionStatus.Closed)
            {
                SetStatus(BleStatus.Disconnected, "GATT session closed");
                _connectionLostTcs?.TrySetResult(null);
            }
        }
        catch { }
    }

    private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        try
        {
            Logger.Log($"BLE device connection status: {sender.ConnectionStatus}");
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                SetStatus(BleStatus.Disconnected, "Device disconnected");
                _connectionLostTcs?.TrySetResult(null);
            }
        }
        catch { }
    }

    private void CleanupConnection()
    {
        try
        {
            if (_sideChar != null)
            {
                try { _sideChar.ValueChanged -= Characteristic_ValueChanged; } catch { }
            }
            if (_gattSession != null)
            {
                try { _gattSession.SessionStatusChanged -= GattSession_SessionStatusChanged; } catch { }
                try { _gattSession.Dispose(); } catch { }
            }
            if (_device != null)
            {
                try { _device.ConnectionStatusChanged -= Device_ConnectionStatusChanged; } catch { }
                try { _device.Dispose(); } catch { }
            }
        }
        catch { }
        finally
        {
            _sideChar = null;
            _ctrlChar = null;
            _gattSession = null;
            _device = null;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        try
        {
            if (e.Mode == PowerModes.Resume)
            {
                Logger.Log("System resume detected; nudging BLE reconnect");
                _reconnectDelayCts?.Cancel();
                _connectionLostTcs?.TrySetResult(null);
            }
        }
        catch { }
    }

    private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
        try {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var bytes = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(bytes);

            Logger.Log($"Notification from {sender.Uuid}: {BitConverter.ToString(bytes)}");

            if (bytes.Length == 0) {
                Logger.Log("Empty payload.");
                return;
            }

            // First byte = side ID (1..8)
            var sideId = bytes[0];
            Logger.Log($"Parsed side id = {sideId}");

            // Notify listeners about active side change
            try { SideChanged?.Invoke(sideId); } catch { }

            var key = sideId.ToString();
            if (_cfg.SideToDevice.TryGetValue(key, out var audioConfig)) {
                // Only switch devices when non-blank values are configured
                if (!string.IsNullOrWhiteSpace(audioConfig.Output))
                {
                    Logger.Log($"Setting output to '{audioConfig.Output}'");
                    _audio.SetDefaultPlaybackDevice(audioConfig.Output);
                }
                else
                {
                    Logger.Log("Output is blank; no change.");
                }
                if (!string.IsNullOrWhiteSpace(audioConfig.Input))
                {
                    Logger.Log($"Setting input to '{audioConfig.Input}'");
                    _audio.SetDefaultInputDevice(audioConfig.Input);
                }
                else
                {
                    Logger.Log("Input is blank; no change.");
                }
            }
            else {
                Logger.Log($"No mapping for side id {key} in config.");
            }
        }
        catch (Exception ex) {
            Logger.Log("Error in ValueChanged handler: " + ex);
        }
    }

    public async Task ListCharacteristicUuidsAsync()
    {
        ulong address = Convert.ToUInt64(_cfg.BluetoothAddress, 16);
        Logger.Log($"Connecting to device @ {address:X}");
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        if (device == null)
        {
            Logger.Log("Failed to create BluetoothLEDevice.");
            return;
        }
        Logger.Log($"Connected to device: {device.Name}, Id: {device.DeviceId}");

        var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (servicesResult.Status != GattCommunicationStatus.Success)
        {
            Logger.Log($"GetGattServicesAsync failed: {servicesResult.Status}");
            return;
        }

        foreach (var svc in servicesResult.Services)
        {
            var charsResult = await svc.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (charsResult.Status != GattCommunicationStatus.Success)
                continue;

            Logger.Log($"Service: {svc.Uuid}");
            foreach (var ch in charsResult.Characteristics)
            {
                Logger.Log($"  Characteristic: {ch.Uuid} Properties: {ch.CharacteristicProperties}");
            }
        }
    }
}
