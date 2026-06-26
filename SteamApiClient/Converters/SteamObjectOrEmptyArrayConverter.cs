/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamApiClient.Converters;

/// <summary>
/// Fixes Steam's broken API design where empty objects/missing data are returned as empty arrays ([])
/// </summary>
public class SteamObjectOrEmptyArrayConverter<T> : JsonConverter<T?> where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Broken Array
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        // number to string
        if (typeof(T) == typeof(string) && reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32().ToString() as T;
        }

        // default
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}