using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using MimeKit;

namespace Hermes.Development;

/// <summary>
/// Writes each captured message to the inbox folder (<c>02</c> MSG-DEV-002): every file under
/// its name followed by <c>.tmp</c>, then renamed, an email's <c>.eml</c> before its record, so
/// a reader never meets half a message; then deletes the records past
/// <c>KeepFor</c> and beyond the newest <c>MaxMessages</c>, with their <c>.eml</c>, and every
/// <c>.tmp</c> file older than a minute.
/// </summary>
internal sealed class InboxWriter(InboxSettings settings, TimeProvider time)
{
    private static readonly TimeSpan TemporaryAge = TimeSpan.FromMinutes(1);

    internal string Folder => settings.Folder;

    /// <summary>Makes the folder where it is missing, on Linux and macOS for its owner alone (mode <c>0700</c>).</summary>
    internal void Prepare()
    {
        if (Directory.Exists(settings.Folder))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(settings.Folder);
        }
        else
        {
            Directory.CreateDirectory(settings.Folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>A new record's id, 128 random bits as 32 lower-case hexadecimal digits, and the instant it is captured, to the millisecond.</summary>
    internal (string Id, DateTimeOffset At) Next()
    {
        DateTimeOffset now = time.GetUtcNow();
        return (Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), new DateTimeOffset(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero));
    }

    /// <summary>Writes <paramref name="body"/>, captured at <paramref name="at"/>, and an email's <paramref name="mime"/> beside it, then cleans the folder.</summary>
    internal async ValueTask WriteAsync(MessageBody body, DateTimeOffset at, MimeMessage? mime, CancellationToken cancellationToken)
    {
        Prepare();
        string stem = Path.Combine(settings.Folder, $"{InboxFiles.Written(at).Stamp}-{Spelling(body.Channel)}-{body.Id}");
        if (mime is not null)
        {
            await WriteAsync(stem + ".eml", (stream, token) => mime.WriteToAsync(stream, token), cancellationToken).ConfigureAwait(false);
        }

        await WriteAsync(stem + ".json", (stream, token) => JsonSerializer.SerializeAsync(stream, body, InboxJson.Default.MessageBody, token), cancellationToken).ConfigureAwait(false);
        Clean(time.GetUtcNow());
    }

    /// <summary>Deletes the records past <c>KeepFor</c> and beyond the newest <c>MaxMessages</c> with their <c>.eml</c>, and the <c>.tmp</c> files older than a minute.</summary>
    internal void Clean(DateTimeOffset now)
    {
        List<RecordFile> records = InboxFiles.Records(settings.Folder);
        DateTimeOffset oldest = now - settings.KeepFor;
        for (int index = 0; index < records.Count; index++)
        {
            if (index >= settings.MaxMessages || records[index].At < oldest)
            {
                InboxFiles.Delete(records[index].Path);
                InboxFiles.Delete(InboxFiles.EmlPath(records[index].Path));
            }
        }

        foreach (string path in InboxFiles.Files(settings.Folder, "*.tmp"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < (now - TemporaryAge).UtcDateTime)
                {
                    InboxFiles.Delete(path);
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // Another process renamed or deleted it first.
            }
        }
    }

    private static async ValueTask WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        string temporary = path + ".tmp";
        try
        {
            FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using (stream.ConfigureAwait(false))
            {
                await write(stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path);
        }
        catch
        {
            InboxFiles.Delete(temporary);
            throw;
        }
    }

    private static string Spelling(Channel channel)
    {
        return channel switch
        {
            Channel.Email => "email",
            Channel.Sms => "sms",
            _ => "push",
        };
    }
}