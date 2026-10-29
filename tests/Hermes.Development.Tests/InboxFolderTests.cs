using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>How the inbox writes, bounds and protects its folder (<c>02</c> MSG-DEV-002).</summary>
[Trait("Category", "unit")]
public sealed class InboxFolderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MSG_DEV_002_AC1_AReaderListingTheFolderWhileSeveralWritersWriteNeverReadsAPartialRecordAndFindsEachEmailsEmlBesideIt()
    {
        using ScratchFolder folder = new();
        InboxSettings settings = new(folder.Path, TimeSpan.FromDays(7), 100_000, false);
        Directory.CreateDirectory(folder.Path);
        using CancellationTokenSource writing = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int reads = 0;
        Task reader = Task.Run(
            () =>
            {
                while (!writing.IsCancellationRequested)
                {
                    foreach (RecordFile record in InboxFiles.Records(folder.Path))
                    {
                        MessageBody? body = InboxFiles.Read(record.Path);
                        Assert.True(body is { Channel: Channel.Email }, record.Path);
                        Assert.True(File.Exists(InboxFiles.EmlPath(record.Path)), record.Path);
                        Interlocked.Increment(ref reads);
                    }
                }
            },
            TestContext.Current.CancellationToken);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(
            async () =>
            {
                InboxCapture capture = new(new InboxWriter(settings, TimeProvider.System), "Bakery");
                for (int index = 0; index < 50; index++)
                {
                    await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);
                }
            },
            TestContext.Current.CancellationToken)));
        await writing.CancelAsync();
        await reader;

        Assert.True(reads > 0);
        Assert.Equal(200, Directory.EnumerateFiles(folder.Path, "*.json").Count());
        Assert.Equal(200, Directory.EnumerateFiles(folder.Path, "*.eml").Count());
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.tmp"));
    }

    [Fact]
    public async Task MSG_DEV_002_AC1_EachMessageIsARecordNamedByItsInstantChannelAndIdAndAnEmailsEmlBesideIt()
    {
        using ScratchFolder folder = new();
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromDays(7), 1_000, false), new ManualClock(Start.AddTicks(12_345))), "Bakery");

        Attempt email = await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);
        Attempt sms = await capture.SmsAsync(Captured.Sms(), TestContext.Current.CancellationToken);

        string emailId = ((Attempt.Accepted)email).ProviderReference!;
        string smsId = ((Attempt.Accepted)sms).ProviderReference!;
        Assert.Matches("^[0-9a-f]{32}$", emailId);
        Assert.Equal(
            [$"20261029T090000001Z-email-{emailId}.eml", $"20261029T090000001Z-email-{emailId}.json", $"20261029T090000001Z-sms-{smsId}.json"],
            Directory.EnumerateFiles(folder.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task MSG_DEV_002_AC2_RecordsOlderThanKeepForAreDeletedWithTheirEmlWhenAMessageIsWritten()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(Start);
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromHours(1), 1_000, false), clock), "Bakery");
        await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(59));
        Attempt kept = await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(2));
        Attempt written = await capture.SmsAsync(Captured.Sms(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [((Attempt.Accepted)written).ProviderReference, ((Attempt.Accepted)kept).ProviderReference],
            Captured.In(folder.Path).Select(message => message.Id));
        Assert.Single(Directory.EnumerateFiles(folder.Path, "*.eml"));
    }

    [Fact]
    public async Task MSG_DEV_002_AC2_RecordsBeyondTheNewestMaxMessagesAreDeletedWithTheirEml()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(Start);
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromDays(7), 10, false), clock), "Bakery");
        List<string> ids = [];
        for (int index = 0; index < 12; index++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            ids.Add(((Attempt.Accepted)await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken)).ProviderReference!);
        }

        Assert.Equal(Enumerable.Reverse(ids).Take(10), Captured.In(folder.Path).Select(message => message.Id));
        Assert.Equal(10, Directory.EnumerateFiles(folder.Path, "*.eml").Count());
    }

    [Fact]
    public async Task MSG_DEV_002_AC2_ATmpFileOlderThanAMinuteIsDeletedAndEveryOtherFileIsLeft()
    {
        using ScratchFolder folder = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromDays(7), 1_000, false), new ManualClock(now)), "Bakery");
        Directory.CreateDirectory(folder.Path);
        string old = Write(folder.Path, "20261029T080000000Z-email-00000000000000000000000000000000.json.tmp", now.AddMinutes(-2));
        string fresh = Write(folder.Path, "20261029T080000000Z-email-11111111111111111111111111111111.json.tmp", now.AddSeconds(-10));
        string other = Write(folder.Path, "notes.txt", now.AddDays(-30));

        await capture.SmsAsync(Captured.Sms(), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(other));
        Assert.Single(Captured.In(folder.Path));
    }

    [Fact]
    public void MSG_DEV_002_AC3_AFolderTheLibraryCreatesIsItsOwnersAloneOnLinux()
    {
        using ScratchFolder folder = new();
        string inbox = Path.Combine(folder.Path, "inbox");

        new InboxWriter(new InboxSettings(inbox, TimeSpan.FromDays(7), 1_000, false), TimeProvider.System).Prepare();

        Assert.True(Directory.Exists(inbox));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(inbox));
        }
    }

    [Fact]
    public void InboxSettings_WithNoFolderConfigured_ReadHermesInboxInTheTemporaryFolder()
    {
        InboxSettings settings = InboxSettings.Read(new ConfigurationBuilder().Build());

        Assert.Equal(Path.Combine(Path.GetTempPath(), "hermes-inbox"), settings.Folder);
        Assert.Equal((TimeSpan.FromDays(7), 1_000, false), (settings.KeepFor, settings.MaxMessages, settings.AllowRemote));
    }

    private static string Write(string folder, string name, DateTimeOffset written)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, "{");
        File.SetLastWriteTimeUtc(path, written.UtcDateTime);
        return path;
    }
}