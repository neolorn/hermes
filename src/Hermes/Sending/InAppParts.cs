using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Hermes.Sending;

/// <summary>
/// The column that holds an in-app delivery's or entry's protected values: a JSON object of
/// each value's field (<c>title.en</c>, <c>body.ar</c>, <c>target</c>) and its stored envelope
/// in base64, each envelope bound to its own field (<c>01</c> MSG-SEND-018).
/// </summary>
internal static class InAppParts
{
    /// <summary>The field of the target values.</summary>
    internal const string Target = "target";

    /// <summary>The field of <paramref name="part"/> in <paramref name="language"/>: the part's name and the language's declared tag joined by <c>.</c>.</summary>
    internal static string Field(string part, string language)
    {
        return part + "." + language;
    }

    /// <summary>The part and language a field names, or none for the target values' field or a field outside the layout.</summary>
    internal static (string Part, string Language)? Of(string field)
    {
        int dot = field.IndexOf('.', StringComparison.Ordinal);
        return dot <= 0 || dot == field.Length - 1 ? null : (field[..dot], field[(dot + 1)..]);
    }

    internal static string Write(IReadOnlyDictionary<string, byte[]> stored)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach ((string field, byte[] value) in stored)
            {
                writer.WriteBase64String(field, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The stored values the column holds, by field, or none where it does not read.</summary>
    internal static Dictionary<string, byte[]>? Read(string? column)
    {
        if (column is null)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(column);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            Dictionary<string, byte[]> stored = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetBytesFromBase64(out byte[]? value))
                {
                    return null;
                }

                stored[property.Name] = value;
            }

            return stored;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}