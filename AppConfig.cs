using System.Collections.Generic;
using System.Text.Json.Serialization;

public record SideAudioConfig(
    [property: JsonPropertyName("input")] string Input,
    [property: JsonPropertyName("output")] string Output
);

public record AppConfig(
    [property: JsonPropertyName("bluetoothAddress")] string BluetoothAddress,
    [property: JsonPropertyName("characteristicUuid")] string? CharacteristicUuid,
    [property: JsonPropertyName("sideToDevice")] Dictionary<string, SideAudioConfig> SideToDevice,
    [property: JsonPropertyName("logPath")] string LogPath,
    [property: JsonPropertyName("startOnBoot")] bool StartOnBoot = false,
    [property: JsonPropertyName("startMinimized")] bool StartMinimized = true
);
