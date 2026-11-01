using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Hermes.Core;
using Hermes.Development;
using Hermes.Endpoints;
using Hermes.Operations;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Tests;

/// <summary>
/// What the release gate compares (<c>04</c> MSG-LIB-006), read as the contract tests read it:
/// the routes with their methods and statuses from the test hosts' endpoint metadata, the
/// development inbox's among them (M-025), each JSON body's members and types from the
/// library's serializer contexts, the engine's and the inbox's, the codes and the
/// configuration keys from the built assemblies, the hubs' routes and methods (M-039), whose payloads are bodies, the spellings of the enumerations, statuses
/// and alert conditions, the metric names and kinds from a test host's meter, the members a
/// host must implement on its interfaces, and the public surface from the packages' public API
/// files; each section a set of lines.
/// </summary>
internal sealed class ReleaseSet
{
    internal const string Routes = "routes";

    internal const string Statuses = "statuses";

    internal const string Bodies = "bodies";

    internal const string Required = "required";

    internal const string Codes = "codes";

    internal const string Keys = "keys";

    internal const string Spelled = "spellings";

    internal const string Metrics = "metrics";

    internal const string Abstract = "abstract";

    /// <summary>The hubs: each hub's route, and each method with the side that sends it and its arguments (M-039).</summary>
    internal const string Hubs = "hubs";

    internal const string Surface = "surface";

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true };

    private readonly SortedDictionary<string, SortedSet<string>> sections = new(StringComparer.Ordinal);

    /// <summary>The sections the baseline file holds; the public surface is the packages' <c>PublicAPI.Shipped.txt</c>.</summary>
    internal static IReadOnlyList<string> FileSections { get; } = [Routes, Statuses, Bodies, Required, Codes, Keys, Spelled, Metrics, Abstract, Hubs];

    /// <summary>The interfaces a host implements: the provider contracts and the seams of <c>04</c> MSG-LIB-002.</summary>
    internal static IReadOnlyList<Type> HostImplemented { get; } =
    [
        typeof(IEmailProvider), typeof(IEmailSession), typeof(ISmsProvider), typeof(IPushProvider),
        typeof(ISecretSource), typeof(IRecipientIdentity), typeof(IConsentSource), typeof(IContentProtector),
        typeof(ITemplateSource), typeof(IDeliveryObserver), typeof(IAlertSink), typeof(IConversationMembership),
        typeof(IAttachmentStore), typeof(IAttachmentScanner),
    ];

    /// <summary>The request bodies an endpoint reads, whose members a caller sends.</summary>
    internal static IReadOnlyList<Type> RequestBodies { get; } = [.. MappedEndpoints.Requests.Values];

    /// <summary>The lines of <paramref name="section"/>, none where it holds none.</summary>
    internal IReadOnlySet<string> this[string section] => sections.TryGetValue(section, out SortedSet<string>? lines) ? lines : new SortedSet<string>(StringComparer.Ordinal);

    /// <summary>A set holding <paramref name="lines"/> in each section named.</summary>
    internal static ReleaseSet Of(params (string Section, string[] Lines)[] lines)
    {
        ReleaseSet set = new();
        foreach ((string section, string[] values) in lines)
        {
            set.Add(section, values);
        }

        return set;
    }

    /// <summary>The set a baseline file holds; a section it lacks, or an empty object, holds nothing.</summary>
    internal static ReleaseSet Read(string json)
    {
        ReleaseSet set = new();
        if (JsonNode.Parse(json) is JsonObject root)
        {
            foreach ((string section, JsonNode? lines) in root)
            {
                set.Add(section, lines is JsonArray array ? array.Select(line => line!.GetValue<string>()) : []);
            }
        }

        return set;
    }

    /// <summary>The public surface the public API files under <paramref name="folder"/> hold, one <c>Project.txt</c> a package.</summary>
    internal static IEnumerable<string> SurfaceOf(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.txt").SelectMany(file => SurfaceLines(Path.GetFileNameWithoutExtension(file), File.ReadAllLines(file)));
    }

    /// <summary>The current set: the built assemblies, the serializer context, a test host's endpoints and meter, and the public API files under <paramref name="root"/>.</summary>
    internal static ReleaseSet Current(string root)
    {
        ReleaseSet set = new();
        AddRoutes(set);
        set.Add(Hubs, MappedEndpoints.Hubs());
        AddBodies(set);
        set.Add(Codes, CodesOf([typeof(Codes).Assembly, typeof(Channel).Assembly, typeof(Hermes.Channels.MessagingChannelsServiceCollectionExtensions).Assembly, typeof(DevelopmentInbox).Assembly]));
        set.Add(Keys, KeysOf().Select(key => "Hermes:" + key));
        set.Add(Spelled, SpellingsOf());
        set.Add(Metrics, MetricsOf());
        set.Add(Abstract, HostImplemented.SelectMany(AbstractMembers));
        foreach (string project in Directory.EnumerateDirectories(Path.Combine(root, "src")).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(project);
            string shipped = Path.Combine(project, "PublicAPI.Shipped.txt");
            string unshipped = Path.Combine(project, "PublicAPI.Unshipped.txt");
            set.Add(Surface, SurfaceLines(name, [.. File.Exists(shipped) ? File.ReadAllLines(shipped) : [], .. File.Exists(unshipped) ? File.ReadAllLines(unshipped) : []]));
        }

        return set;
    }

    /// <summary>The repository's root, the nearest folder above the test run that holds the solution.</summary>
    internal static string Root()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Hermes.sln")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("No folder above the test run holds Hermes.sln.");
    }

    /// <summary>The members of <paramref name="type"/> an implementation must supply, a member with a default implementation not among them.</summary>
    internal static IEnumerable<string> AbstractMembers(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetMethods()
            .Where(method => method.IsAbstract)
            .Select(method => $"{type.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})");
    }

    /// <summary>The set with the public surface <paramref name="surface"/> in place of its own.</summary>
    internal ReleaseSet WithSurface(IEnumerable<string> surface)
    {
        ReleaseSet set = new();
        foreach ((string section, SortedSet<string> lines) in sections.Where(pair => pair.Key != Surface))
        {
            set.Add(section, lines);
        }

        set.Add(Surface, surface);
        return set;
    }

    /// <summary>The baseline file's text: every section but the public surface, each in order.</summary>
    internal string ToJson()
    {
        JsonObject root = [];
        foreach (string section in FileSections)
        {
            root[section] = new JsonArray([.. this[section].Select(line => (JsonNode?)JsonValue.Create(line))]);
        }

        return root.ToJsonString(Written) + "\n";
    }

    private static IEnumerable<string> SurfaceLines(string project, string[] lines)
    {
        HashSet<string> removed = [.. lines.Where(line => line.StartsWith("*REMOVED*", StringComparison.Ordinal)).Select(line => line["*REMOVED*".Length..])];
        return lines
            .Where(line => line.Length > 0 && line != "#nullable enable" && !line.StartsWith("*REMOVED*", StringComparison.Ordinal) && !removed.Contains(line))
            .Select(line => $"{project}: {line}");
    }

    private static void AddRoutes(ReleaseSet set)
    {
        foreach ((string route, IReadOnlyList<IProducesResponseTypeMetadata> responses) in MappedEndpoints.Read())
        {
            set.Add(Routes, [route]);
            set.Add(Statuses, responses.Select(produced => $"{route} {produced.StatusCode}"));
        }
    }

    private static void AddBodies(ReleaseSet set)
    {
        HashSet<Type> seen = [];
        Queue<(Type Type, bool Request)> next = new(BodyMembers.Roots().Select(type => (type, RequestBodies.Contains(type))));
        while (next.TryDequeue(out (Type Type, bool Request) body))
        {
            if (!seen.Add(body.Type))
            {
                continue;
            }

            JsonTypeInfo info = BodyMembers.Info(body.Type);
            foreach (JsonPropertyInfo property in info.Kind == JsonTypeInfoKind.Object ? info.Properties : [])
            {
                set.Add(Bodies, [$"{body.Type.Name}.{property.Name}: {Named(property.PropertyType)}"]);
                if (body.Request && property.IsRequired)
                {
                    set.Add(Required, [$"{body.Type.Name}.{property.Name}"]);
                }

                foreach (Type inner in BodyMembers.Inner(property.PropertyType).Where(BodyMembers.IsBody))
                {
                    next.Enqueue((inner, body.Request));
                }
            }
        }
    }

    /// <summary>A body member's type as the wire carries it.</summary>
    private static string Named(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner)
        {
            return Named(inner) + "?";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type.IsPrimitive || type == typeof(decimal))
        {
            return "number";
        }

        if (type.IsGenericType && type.GetGenericArguments().Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return Named(type.GetGenericArguments()[0]) + "[]";
        }

        if (type.IsGenericType)
        {
            return $"map<{string.Join(", ", type.GetGenericArguments().Select(Named))}>";
        }

        return type.Name;
    }

    private static IEnumerable<string> CodesOf(Assembly[] assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(value => value.StartsWith("messaging.", StringComparison.Ordinal) && value.Split('.').Length >= 3 && value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '.'));
    }

    private static IEnumerable<string> KeysOf()
    {
        return ConfigurationRules.Paths
            .Concat(ConfigurationRules.WriteVersions)
            .Concat(["PublicBaseAddress", "Languages", "DefaultLanguage", "TimeZone"])
            .Concat(KeyPaths(typeof(Hermes.Channels.MessagingChannelsServiceCollectionExtensions).Assembly, "Hermes.Channels.ChannelKeys"))
            .Concat(KeyPaths(typeof(DevelopmentInbox).Assembly, "Hermes.Development.InboxSettings"));
    }

    /// <summary>The paths of the keys the internal <paramref name="holder"/> of <paramref name="assembly"/> lists, a package that grants this project nothing.</summary>
    private static IEnumerable<string> KeyPaths(Assembly assembly, string holder)
    {
        Type type = assembly.GetType(holder, throwOnError: true)!;
        IEnumerable<object> keys = (IEnumerable<object>)type.GetProperty("Keys", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        return keys.Select(key => (string)key.GetType().GetProperty("Path", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(key)!);
    }

    private static IEnumerable<string> SpellingsOf()
    {
        return Spellings.AllChannels.Select(value => "channel " + value)
            .Concat(Spellings.AllStatuses.Select(value => "status " + value))
            .Concat(Spellings.AllClasses.Select(value => "class " + value))
            .Concat(Spellings.AllReasons.Select(value => "reason " + value))
            .Concat(Spellings.AllServices.Select(value => "service " + value))
            .Concat(AlertState.Conditions.Select(value => "alert " + value));
    }

    private static List<string> MetricsOf()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<MessagingMetrics>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IMeterFactory factory = provider.GetRequiredService<IMeterFactory>();
        List<string> published = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Scope == factory)
            {
                published.Add($"{instrument.Name} {instrument.GetType().Name.Split('`')[0]} {instrument.Unit}");
            }
        };
        listener.Start();
        provider.GetRequiredService<MessagingMetrics>();
        return published;
    }

    private void Add(string section, IEnumerable<string> lines)
    {
        if (!sections.TryGetValue(section, out SortedSet<string>? held))
        {
            held = new SortedSet<string>(StringComparer.Ordinal);
            sections[section] = held;
        }

        held.UnionWith(lines);
    }
}