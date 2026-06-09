using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ConcurrentDictionaryConverter<TKey, TValue> : JsonConverter<ConcurrentDictionary<TKey, TValue>>
    where TKey : notnull
{
    public override ConcurrentDictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);
        return dictionary == null ? new ConcurrentDictionary<TKey, TValue>() : new ConcurrentDictionary<TKey, TValue>(dictionary);
    }

    public override void Write(Utf8JsonWriter writer, ConcurrentDictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), options);
    }
}
