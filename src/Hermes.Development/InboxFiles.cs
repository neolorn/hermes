using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>
/// Reads an inbox folder (<c>02</c> MSG-DEV-002): its <c>.json</c> records, named
/// <c>&lt;at&gt;-&lt;channel&gt;-&lt;id&gt;.json</c>, each email's <c>.eml</c> beside its record;
/// every other file is ignored, and a file another process deleted first is no failure.
/// </summary>
internal static partial class InboxFiles
{
    /// <summary>The instant in a file's name, in UTC.</summary>
    internal const string Stamp = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>The instant in a record, in ISO 8601 UTC with milliseconds.</summary>
    internal const string Instant = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>The records of <paramref name="folder"/>, newest first.</summary>
    internal static List<RecordFile> Records(string folder)
    {
        List<RecordFile> records = [];
        foreach (string path in Files(folder, "*.json"))
        {
            if (Name().Match(Path.GetFileName(path)) is { Success: true } name)
            {
                records.Add(new RecordFile(path, name.Groups["id"].Value, Parse(name.Groups["at"].Value)));
            }
        }

        records.Sort((one, other) => string.CompareOrdinal(Path.GetFileName(other.Path), Path.GetFileName(one.Path)));
        return records;
    }

    /// <summary>The messages of <paramref name="folder"/> that match <paramref name="filter"/>, newest first, at most <paramref name="limit"/>.</summary>
    internal static IReadOnlyList<InboxMessage> List(string folder, InboxFilter filter, int limit)
    {
        return [.. Bodies(folder, filter, limit).Select(found => Message(found.Body, found.File.Path))];
    }

    /// <summary>The bodies of the records of <paramref name="folder"/> that match <paramref name="filter"/>, newest first, at most <paramref name="limit"/>.</summary>
    internal static IEnumerable<(RecordFile File, MessageBody Body)> Bodies(string folder, InboxFilter filter, int limit)
    {
        int found = 0;
        foreach (RecordFile file in Records(folder))
        {
            if (found >= limit)
            {
                yield break;
            }

            if ((filter.After is { } after && file.At <= after) || Read(file.Path) is not { } body || !Matches(body, filter))
            {
                continue;
            }

            found++;
            yield return (file, body);
        }
    }

    /// <summary>The record of <paramref name="id"/> in <paramref name="folder"/>, none where it holds none.</summary>
    internal static (RecordFile File, MessageBody Body)? Find(string folder, string id)
    {
        if (!Id().IsMatch(id))
        {
            return null;
        }

        foreach (RecordFile file in Records(folder).Where(file => string.Equals(file.Id, id, StringComparison.Ordinal)))
        {
            if (Read(file.Path) is { } body)
            {
                return (file, body);
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="body"/> matches <paramref name="filter"/>: an email address in any case, a number exactly, every other member exactly.</summary>
    internal static bool Matches(MessageBody body, InboxFilter filter)
    {
        StringComparison to = body.Channel == Channel.Email ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return (filter.To is null || string.Equals(body.To, filter.To, to))
            && (filter.Person is null || string.Equals(body.Person, filter.Person, StringComparison.Ordinal))
            && (filter.Channel is null || body.Channel == filter.Channel)
            && (filter.App is null || string.Equals(body.App, filter.App, StringComparison.Ordinal))
            && (filter.Kind is null || string.Equals(body.Kind, filter.Kind, StringComparison.Ordinal))
            && (filter.After is null || Parse(body) > filter.After);
    }

    /// <summary>The record at <paramref name="path"/>, none where it is gone or does not read.</summary>
    internal static MessageBody? Read(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize(stream, InboxJson.Default.MessageBody);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The message <paramref name="body"/> holds, its record at <paramref name="path"/>.</summary>
    internal static InboxMessage Message(MessageBody body, string path)
    {
        return new InboxMessage
        {
            Id = body.Id,
            Channel = body.Channel,
            App = body.App,
            At = Parse(body),
            To = body.To,
            Person = body.Person,
            Kind = body.Kind,
            Category = body.Category,
            Language = body.Language,
            Summary = body.Summary,
            Text = body.Text,
            Links = body.Links,
            Email = body.Email is { } email
                ? new InboxEmail
                {
                    From = Address(email.From),
                    ReplyTo = email.ReplyTo is { } replyTo ? Address(replyTo) : null,
                    Subject = email.Subject,
                    Text = email.Text,
                    Html = email.Html,
                    MessageId = email.MessageId,
                    Unsubscribe = email.Unsubscribe is { } unsubscribe ? new Uri(unsubscribe, UriKind.Absolute) : null,
                    Headers = [.. email.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))],
                    EmlPath = EmlPath(path),
                }
                : null,
            Sms = body.Sms is { } sms
                ? new InboxSms { Sender = sms.Sender, Text = sms.Text, Encoding = sms.Encoding, Segments = sms.Segments, DeliveryReference = sms.DeliveryReference }
                : null,
            Push = body.Push is { } push
                ? new InboxPush
                {
                    Service = push.Service,
                    Notification = new InboxNotification
                    {
                        Title = push.Notification.Title,
                        Body = push.Notification.Body,
                        Url = push.Notification.Url,
                        Tag = push.Notification.Tag,
                        Lang = push.Notification.Lang,
                        Dir = push.Notification.Dir,
                        Icon = push.Notification.Icon,
                        Badge = push.Notification.Badge,
                    },
                    Ttl = TimeSpan.FromSeconds(push.Ttl),
                    Urgency = push.Urgency,
                    Topic = push.Topic,
                }
                : null,
        };
    }

    /// <summary>The <c>.eml</c> beside the record at <paramref name="path"/>.</summary>
    internal static string EmlPath(string path)
    {
        return Path.ChangeExtension(path, ".eml");
    }

    /// <summary>Deletes every record of <paramref name="folder"/> with its <c>.eml</c>.</summary>
    internal static void Clear(string folder)
    {
        foreach (RecordFile record in Records(folder))
        {
            Delete(record.Path);
            Delete(EmlPath(record.Path));
        }

        foreach (string path in Files(folder, "*.eml").Where(path => Name().IsMatch(Path.ChangeExtension(Path.GetFileName(path), ".json"))))
        {
            Delete(path);
        }
    }

    /// <summary>Deletes <paramref name="path"/>; a file another process deleted or holds is left.</summary>
    internal static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Another process deleted it first, or holds it; the next write cleans it.
        }
    }

    /// <summary>The files of <paramref name="folder"/> that <paramref name="pattern"/> matches; none where the folder is gone.</summary>
    internal static IReadOnlyList<string> Files(string folder, string pattern)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, pattern)] : [];
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The instant <paramref name="body"/> was captured.</summary>
    internal static DateTimeOffset Parse(MessageBody body)
    {
        return DateTimeOffset.ParseExact(body.At, Instant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    /// <summary>The record's instant <paramref name="at"/> as its file's name and its <c>at</c> write it.</summary>
    internal static (string Stamp, string Instant) Written(DateTimeOffset at)
    {
        DateTime utc = at.UtcDateTime;
        return (utc.ToString(Stamp, CultureInfo.InvariantCulture), utc.ToString(Instant, CultureInfo.InvariantCulture));
    }

    private static DateTimeOffset Parse(string stamp)
    {
        return DateTimeOffset.ParseExact(stamp, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static EmailAddress Address(AddressBody address)
    {
        return new EmailAddress { Address = address.Address, DisplayName = address.DisplayName };
    }

    /// <summary>A record's name.</summary>
    [GeneratedRegex(@"^(?<at>\d{8}T\d{9}Z)-(?<channel>email|sms|push)-(?<id>[0-9a-f]{32})\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex Name();

    /// <summary>A record's id.</summary>
    [GeneratedRegex(@"^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Id();
}

/// <summary>A record of an inbox folder: its path, its id and the instant it was captured.</summary>
internal readonly record struct RecordFile(string Path, string Id, DateTimeOffset At);