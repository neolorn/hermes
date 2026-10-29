using System;
using System.Collections.Generic;
using System.IO;

using Hermes.Channels;

using Microsoft.Extensions.Configuration;

namespace Hermes.Development;

/// <summary>
/// The inbox's keys of <c>08</c> section 4, <c>Hermes:DevelopmentInbox:...</c>, read and
/// checked only where the inbox acts (<c>02</c> MSG-DEV-001), each start only.
/// </summary>
/// <param name="Folder">The inbox's folder, an absolute path.</param>
/// <param name="KeepFor">How long a record is kept.</param>
/// <param name="MaxMessages">How many records are kept at most.</param>
/// <param name="AllowRemote">Whether the group answers other machines and other host names (<c>02</c> MSG-DEV-005).</param>
internal sealed record InboxSettings(string Folder, TimeSpan KeepFor, int MaxMessages, bool AllowRemote)
{
    internal const string FolderKey = "DevelopmentInbox:Folder";

    internal const string KeepForKey = "DevelopmentInbox:KeepFor";

    internal const string MaxMessagesKey = "DevelopmentInbox:MaxMessages";

    internal const string AllowRemoteKey = "DevelopmentInbox:AllowRemote";

    /// <summary>The keys, each with its rule and default.</summary>
    internal static IReadOnlyList<ConfigurationKey> Keys { get; } =
    [
        ConfigurationKeys.Text(FolderKey, startOnly: true, Path.IsPathFullyQualified),
        ConfigurationKeys.Duration(KeepForKey, startOnly: true, TimeSpan.FromHours(1), TimeSpan.FromDays(90), TimeSpan.FromDays(7)),
        ConfigurationKeys.Whole(MaxMessagesKey, startOnly: true, 10, 100_000, 1_000),
        ConfigurationKeys.Flag(AllowRemoteKey, false),
    ];

    /// <summary>The default folder: <c>hermes-inbox</c> in the operating system's temporary folder (<c>02</c> MSG-DEV-002).</summary>
    internal static string DefaultFolder => Path.Combine(Path.GetTempPath(), "hermes-inbox");

    /// <summary>Reads the keys of <paramref name="configuration"/>'s section <c>Hermes</c>; a value outside its rule stops the start.</summary>
    /// <exception cref="Core.MessagingStartupException">A key's value is outside its rule.</exception>
    internal static InboxSettings Read(IConfiguration configuration)
    {
        IConfigurationSection hermes = configuration.GetSection("Hermes");
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (ConfigurationKey key in Keys)
        {
            if (hermes[key.Path] is not { } text)
            {
                values[key.Path] = key.Default;
                continue;
            }

            object? read = key.Read(text);
            if (read is null || !key.Within(read))
            {
                throw StartupFailure.Of(ChannelCodes.ConfigurationInvalid, ("key", key.Path));
            }

            values[key.Path] = read;
        }

        return new InboxSettings(
            (string?)values[FolderKey] ?? DefaultFolder,
            (TimeSpan)values[KeepForKey]!,
            (int)values[MaxMessagesKey]!,
            (bool)values[AllowRemoteKey]!);
    }
}