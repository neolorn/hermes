using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Hermes.Content;

/// <summary>
/// The push payload of <c>02</c> MSG-PUSH-002: the UTF-8 JSON object of <c>title</c>,
/// <c>body</c>, <c>url</c>, <c>tag</c>, <c>lang</c>, <c>dir</c>, <c>icon</c> and <c>badge</c>, a
/// member whose optional part the template lacks left out; every character but those JSON
/// must escape written as itself, so its size is the text's own.
/// </summary>
internal static class PushPayload
{
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    /// <summary>The payload of the rendered <paramref name="parts"/>, by part name, in <paramref name="language"/> and its <paramref name="direction"/>.</summary>
    internal static byte[] Of(IReadOnlyDictionary<string, string> parts, string language, string direction)
    {
        ArgumentNullException.ThrowIfNull(parts);
        using MemoryStream payload = new();
        using (Utf8JsonWriter writer = new(payload, Options))
        {
            writer.WriteStartObject();
            Member(writer, parts, TemplateParts.Title);
            Member(writer, parts, TemplateParts.Body);
            Member(writer, parts, TemplateParts.Url);
            Member(writer, parts, TemplateParts.Tag);
            writer.WriteString("lang", language);
            writer.WriteString("dir", direction);
            Member(writer, parts, TemplateParts.Icon);
            Member(writer, parts, TemplateParts.Badge);
            writer.WriteEndObject();
        }

        return payload.ToArray();
    }

    private static void Member(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> parts, string part)
    {
        if (parts.TryGetValue(part, out string? text))
        {
            writer.WriteString(part, text);
        }
    }
}