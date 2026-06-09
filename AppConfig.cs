using System.Collections.Concurrent;
using System.Text.Json.Serialization;

public record SideAudioConfig(
    [property: JsonPropertyName("input")] string Input,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("name")] string? Name = null
);

public sealed class AppConfig
{
    [JsonPropertyName("bluetoothAddress")]
    public string BluetoothAddress { get; init; } = string.Empty;

    [JsonPropertyName("characteristicUuid")]
    public string? CharacteristicUuid { get; init; }

    [JsonPropertyName("sideToDevice")]
    [JsonConverter(typeof(ConcurrentDictionaryConverter<string, SideAudioConfig>))]
    public ConcurrentDictionary<string, SideAudioConfig> SideToDevice { get; init; } = new();

    [JsonPropertyName("logPath")]
    public string LogPath { get; init; } = string.Empty;

    [JsonPropertyName("startOnBoot")]
    public bool StartOnBoot { get; init; }

    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; init; } = true;
}
