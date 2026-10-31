using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// An in-app kind's target values (<c>02</c> MSG-INAPP-001): the send's top-level scalar values
/// under the names the kind declares, and the text they are protected as, a JSON object of each
/// value by name as an object of its one case (<c>text</c>, <c>whole</c>, <c>decimal</c>,
/// <c>flag</c> or <c>instant</c>), a decimal number and an instant written as invariant text so
/// that each reads back as it was sent.
/// </summary>
internal static class TargetValues
{
    private const string Text = "text";

    private const string Whole = "whole";

    private const string Decimal = "decimal";

    private const string Flag = "flag";

    private const string Instant = "instant";

    /// <summary>
    /// The values of <paramref name="values"/> the kind's target names, by the name it declares;
    /// throws <see cref="ArgumentException"/> where the send passes none under a name, or a list.
    /// </summary>
    internal static Dictionary<string, SendValue> Of(KindDeclaration kind, IReadOnlyDictionary<string, SendValue> values)
    {
        Dictionary<string, SendValue> chosen = new(StringComparer.Ordinal);
        foreach (string name in kind.InApp!.TargetValues)
        {
            SendValue? value = values.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            if (value is null)
            {
                throw new ArgumentException($"The kind '{kind.Name}' takes the target value '{name}', which the send does not pass.", nameof(values));
            }

            if (value is SendValue.List)
            {
                throw new ArgumentException($"The target value '{name}' of the kind '{kind.Name}' is a list, not a scalar value.", nameof(values));
            }

            chosen[name] = value;
        }

        return chosen;
    }

    internal static string Write(IReadOnlyDictionary<string, SendValue> values)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach ((string name, SendValue value) in values)
            {
                writer.WriteStartObject(name);
                switch (value)
                {
                    case SendValue.PlainText text:
                        writer.WriteString(Text, text.Value);
                        break;
                    case SendValue.WholeNumber whole:
                        writer.WriteNumber(Whole, whole.Value);
                        break;
                    case SendValue.DecimalNumber number:
                        writer.WriteString(Decimal, SendForm.DecimalText(number.Value));
                        break;
                    case SendValue.Flag flag:
                        writer.WriteBoolean(Flag, flag.Value);
                        break;
                    case SendValue.Instant instant:
                        writer.WriteString(Instant, instant.Value.ToString("O", CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new ArgumentException("A target value is a scalar value.", nameof(values));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The values <paramref name="text"/> holds, by name, or none where it does not read.</summary>
    internal static Dictionary<string, SendValue>? Read(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            Dictionary<string, SendValue> values = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (Value(property.Value) is not { } value)
                {
                    return null;
                }

                values[property.Name] = value;
            }

            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SendValue? Value(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() != 1)
        {
            return null;
        }

        JsonProperty only = element.EnumerateObject().First();
        JsonElement value = only.Value;
        return only.Name switch
        {
            Text when value.ValueKind == JsonValueKind.String => new SendValue.PlainText(value.GetString()!),
            Whole when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long whole) => new SendValue.WholeNumber(whole),
            Decimal when value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) => new SendValue.DecimalNumber(number),
            Flag when value.ValueKind is JsonValueKind.True or JsonValueKind.False => new SendValue.Flag(value.GetBoolean()),
            Instant when value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParseExact(value.GetString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset instant) => new SendValue.Instant(instant),
            _ => null,
        };
    }
}