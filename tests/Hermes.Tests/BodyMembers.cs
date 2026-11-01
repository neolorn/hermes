using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

using Hermes.Conversations;
using Hermes.Development;
using Hermes.Endpoints;

namespace Hermes.Tests;

/// <summary>
/// A JSON body's members as dotted paths (<c>categories.channels.on</c>), read either from the
/// library's serializer contexts, the engine's, the development inbox's and the conversations
/// package's, as the release gate
/// reads them (<c>04</c> MSG-LIB-006), or from a body as <c>05</c> writes it
/// (<c>{ "categories": [ { "name", ... } ] }</c>); a member whose value is an object of the
/// library's own is followed into it, another (a map, <c>{ ... }</c>) is one member.
/// </summary>
internal static partial class BodyMembers
{
    /// <summary>The development inbox's serializer context, internal to its package, which grants this project nothing (<c>07</c> MSG-ENG-001).</summary>
    private static readonly JsonSerializerContext Inbox = (JsonSerializerContext)typeof(DevelopmentInbox).Assembly
        .GetType("Hermes.Development.InboxJson", throwOnError: true)!
        .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!
        .GetValue(null)!;

    /// <summary>The conversations package's serializer context, internal to its package, which grants this project nothing (<c>07</c> MSG-ENG-001).</summary>
    private static readonly JsonSerializerContext Conversations = (JsonSerializerContext)typeof(MessagingConversationsServiceCollectionExtensions).Assembly
        .GetType("Hermes.Conversations.Endpoints.ConversationsJson", throwOnError: true)!
        .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!
        .GetValue(null)!;

    /// <summary>The conversations package's body of <paramref name="name"/>, internal to its package.</summary>
    internal static Type ConversationsBody(string name)
    {
        return Conversations.GetType().Assembly.GetType("Hermes.Conversations.Endpoints." + name, throwOnError: true)!;
    }

    /// <summary>The bodies the serializer contexts are declared for.</summary>
    internal static IReadOnlyList<Type> Roots()
    {
        return [.. new[] { typeof(EndpointJson), Inbox.GetType(), Conversations.GetType() }
            .SelectMany(context => context.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!)];
    }

    /// <summary>The serializer context's view of <paramref name="type"/>: the inbox's or the conversations package's for a type of its package, the engine's for any other.</summary>
    internal static JsonTypeInfo Info(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        JsonSerializerContext context = type.Assembly == Inbox.GetType().Assembly ? Inbox
            : type.Assembly == Conversations.GetType().Assembly ? Conversations
            : EndpointJson.Default;
        return context.GetTypeInfo(type) ?? throw new InvalidOperationException($"The serializer context holds no {type.Name}.");
    }

    /// <summary>Whether <paramref name="type"/> is one of the library's bodies, the engine's, the inbox's or the conversations package's, whose members are followed into.</summary>
    internal static bool IsBody(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.Assembly == typeof(EndpointJson).Assembly || type.Assembly == Inbox.GetType().Assembly || type.Assembly == Conversations.GetType().Assembly;
    }

    /// <summary>The types <paramref name="type"/> carries: itself, or a collection's elements.</summary>
    internal static IEnumerable<Type> Inner(Type type)
    {
        Type plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain.IsGenericType ? plain.GetGenericArguments().SelectMany(Inner) : [plain];
    }

    /// <summary>The members of <paramref name="type"/> as the serializer context writes and reads them.</summary>
    internal static IReadOnlyList<string> Of(Type type)
    {
        List<string> members = [];
        Add(type, string.Empty, members);
        return members;
    }

    /// <summary>The members of the body <paramref name="sketch"/> writes, a value naming a shape (<c>&lt;summary&gt;</c>) read through <paramref name="shape"/> where given.</summary>
    internal static IReadOnlyList<string> OfSketch(string sketch, Func<string, IReadOnlyList<string>?>? shape = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        string[] tokens = [.. Token().Matches(sketch).Select(match => match.Value)];
        int next = 0;
        List<string> members = [];
        ReadObject(tokens, ref next, string.Empty, members, shape);
        return members;
    }

    private static void Add(Type type, string prefix, List<string> members)
    {
        JsonTypeInfo info = Info(type);
        foreach (JsonPropertyInfo property in info.Kind == JsonTypeInfoKind.Object ? info.Properties : [])
        {
            string path = prefix + property.Name;
            members.Add(path);
            foreach (Type inner in Inner(property.PropertyType).Where(IsBody))
            {
                Add(inner, path + ".", members);
            }
        }
    }

    /// <summary>Reads an object from <c>{</c> to its <c>}</c>, adding each member under <paramref name="prefix"/>.</summary>
    private static void ReadObject(string[] tokens, ref int next, string prefix, List<string> members, Func<string, IReadOnlyList<string>?>? shape)
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
                ReadValue(tokens, ref next, path, members, shape);
            }
        }

        Expect(tokens, ref next, "}");
    }

    /// <summary>Reads a member's value: an object, followed into; an array, followed into its element; a shape, its members; anything else, skipped.</summary>
    private static void ReadValue(string[] tokens, ref int next, string path, List<string> members, Func<string, IReadOnlyList<string>?>? shape)
    {
        if (next < tokens.Length && tokens[next] == "{")
        {
            ReadObject(tokens, ref next, path + ".", members, shape);
            return;
        }

        if (next < tokens.Length && tokens[next] == "[")
        {
            next++;
            ReadValue(tokens, ref next, path, members, shape);
            Expect(tokens, ref next, "]");
            return;
        }

        while (next < tokens.Length && tokens[next] is not ("," or "}" or "]"))
        {
            if (shape?.Invoke(tokens[next]) is { } inner)
            {
                members.AddRange(inner.Select(member => path + "." + member));
            }

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