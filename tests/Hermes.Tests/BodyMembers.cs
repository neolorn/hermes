using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

using Hermes.Endpoints;

namespace Hermes.Tests;

/// <summary>
/// A JSON body's members as dotted paths (<c>categories.channels.on</c>), read either from the
/// library's serializer context, as the release gate reads it (<c>04</c> MSG-LIB-006), or from a
/// body as <c>05</c> writes it (<c>{ "categories": [ { "name", ... } ] }</c>); a member whose
/// value is an object of the library's own is followed into it, another (a map, <c>{ ... }</c>)
/// is one member.
/// </summary>
internal static partial class BodyMembers
{
    /// <summary>The bodies the serializer context is declared for.</summary>
    internal static IReadOnlyList<Type> Roots()
    {
        return [.. typeof(EndpointJson).GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!)];
    }

    /// <summary>The members of <paramref name="type"/> as the serializer context writes and reads them.</summary>
    internal static IReadOnlyList<string> Of(Type type)
    {
        List<string> members = [];
        Add(type, string.Empty, members);
        return members;
    }

    /// <summary>The members of the body <paramref name="sketch"/> writes.</summary>
    internal static IReadOnlyList<string> OfSketch(string sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        string[] tokens = [.. Token().Matches(sketch).Select(match => match.Value)];
        int next = 0;
        List<string> members = [];
        ReadObject(tokens, ref next, string.Empty, members);
        return members;
    }

    private static void Add(Type type, string prefix, List<string> members)
    {
        JsonTypeInfo info = EndpointJson.Default.GetTypeInfo(type) ?? throw new InvalidOperationException($"The serializer context holds no {type.Name}.");
        foreach (JsonPropertyInfo property in info.Kind == JsonTypeInfoKind.Object ? info.Properties : [])
        {
            string path = prefix + property.Name;
            members.Add(path);
            foreach (Type inner in Inner(property.PropertyType).Where(inner => inner.Assembly == typeof(EndpointJson).Assembly))
            {
                Add(inner, path + ".", members);
            }
        }
    }

    /// <summary>The types <paramref name="type"/> carries: itself, or a collection's elements.</summary>
    private static IEnumerable<Type> Inner(Type type)
    {
        Type plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain.IsGenericType ? plain.GetGenericArguments().SelectMany(Inner) : [plain];
    }

    /// <summary>Reads an object from <c>{</c> to its <c>}</c>, adding each member under <paramref name="prefix"/>.</summary>
    private static void ReadObject(string[] tokens, ref int next, string prefix, List<string> members)
    {
        Expect(tokens, ref next, "{");
        while (next < tokens.Length && tokens[next] != "}")
        {
            string token = tokens[next++];
            if (token is "," or "...")
            {
                continue;
            }

            if (!token.StartsWith('"'))
            {
                throw new FormatException($"A member's name was expected, not {token}.");
            }

            string path = prefix + token.Trim('"');
            members.Add(path);
            if (next < tokens.Length && tokens[next] == ":")
            {
                next++;
                ReadValue(tokens, ref next, path, members);
            }
        }

        Expect(tokens, ref next, "}");
    }

    /// <summary>Reads a member's value: an object, followed into; an array, followed into its element; anything else, skipped.</summary>
    private static void ReadValue(string[] tokens, ref int next, string path, List<string> members)
    {
        if (next < tokens.Length && tokens[next] == "{")
        {
            ReadObject(tokens, ref next, path + ".", members);
            return;
        }

        if (next < tokens.Length && tokens[next] == "[")
        {
            next++;
            ReadValue(tokens, ref next, path, members);
            Expect(tokens, ref next, "]");
            return;
        }

        while (next < tokens.Length && tokens[next] is not ("," or "}" or "]"))
        {
            next++;
        }
    }

    private static void Expect(string[] tokens, ref int next, string token)
    {
        if (next >= tokens.Length || tokens[next] != token)
        {
            throw new FormatException($"{token} was expected at token {next}.");
        }

        next++;
    }

    /// <summary>A quoted name or value, an ellipsis, a brace, bracket, comma or colon, or any other word.</summary>
    [GeneratedRegex(@"""[^""]*""|\.\.\.|[{}\[\],:]|[^\s{}\[\],:""]+", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}