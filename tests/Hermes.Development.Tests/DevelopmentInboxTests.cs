using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The helper a .NET test or tool reads the inbox with (<c>02</c> MSG-DEV-006).</summary>
[Trait("Category", "unit")]
public sealed class DevelopmentInboxTests
{
    [Fact]
    public async Task MSG_DEV_006_AC1_ATestThatMakesAnInboxThenSendsAnEmailHoldingALinkWaitsForItAndFindsTheLink()
    {
        using ScratchFolder folder = new();
        InboxCapture capture = Capture(folder.Path);
        await capture.EmailAsync(Captured.Email() with { Text = "An earlier code: https://bakery.example/sign-in?code=1111", Html = null }, TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        DevelopmentInbox inbox = new(folder.Path);

        Task<InboxMessage> waited = inbox.WaitForAsync(new InboxFilter { To = "SomeOne@Example.com" }, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(waited.IsCompleted);
        Attempt sent = await capture.EmailAsync(Captured.Email() with { Text = "Sign in: https://bakery.example/sign-in?code=4521", Html = null }, TestContext.Current.CancellationToken);
        InboxMessage message = await waited;

        Assert.Equal(((Attempt.Accepted)sent).ProviderReference, message.Id);
        Assert.Contains("https://bakery.example/sign-in?code=4521", message.Links);
        Assert.Equal(Path.GetFullPath(folder.Path), inbox.Folder);
    }

    [Fact]
    public async Task MSG_DEV_006_AC1_AWaitAnswersAMessageCapturedAfterTheFiltersAfterAtOnce()
    {
        using ScratchFolder folder = new();
        InboxCapture capture = Capture(folder.Path);
        DateTimeOffset before = DateTimeOffset.UtcNow.AddMinutes(-1);
        Attempt sent = await capture.SmsAsync(Captured.Sms(), TestContext.Current.CancellationToken);
        DevelopmentInbox inbox = new(folder.Path);

        InboxMessage message = await inbox.WaitForAsync(new InboxFilter { To = "+201000000001", After = before }, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(((Attempt.Accepted)sent).ProviderReference, message.Id);
    }

    [Fact]
    public async Task MSG_DEV_006_AC2_AWaitThatNoMessageMatchesThrowsTimeoutExceptionOnceItsTimeoutHasPassed()
    {
        using ScratchFolder folder = new();
        await Capture(folder.Path).EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);
        DevelopmentInbox inbox = new(folder.Path);
        Stopwatch waited = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => inbox.WaitForAsync(new InboxFilter { Channel = Channel.Push }, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken).AsTask());

        Assert.True(waited.Elapsed >= TimeSpan.FromMilliseconds(500), $"The wait ended after {waited.Elapsed}.");
    }

    [Fact]
    public async Task MSG_DEV_006_AC3_AfterClearTheFolderHoldsNoRecordAndNoEml()
    {
        using ScratchFolder folder = new();
        InboxCapture capture = Capture(folder.Path);
        await capture.EmailAsync(Captured.Email(), TestContext.Current.CancellationToken);
        await capture.SmsAsync(Captured.Sms(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "notes.txt"), "kept", TestContext.Current.CancellationToken);
        DevelopmentInbox inbox = new(folder.Path);

        await inbox.ClearAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.json"));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.eml"));
        Assert.True(File.Exists(Path.Combine(folder.Path, "notes.txt")));
        Assert.Empty(await inbox.ListAsync(new InboxFilter(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_WithAFilter_AnswersTheMatchingMessagesNewestFirst()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromDays(7), 1_000, false), clock), "Bakery");
        List<string> ids = [];
        foreach (EmailMessage email in new[] { Captured.Email(), Captured.Email() with { ToAddress = "other@example.com" }, Captured.Email() })
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            ids.Add(((Attempt.Accepted)await capture.EmailAsync(email, TestContext.Current.CancellationToken)).ProviderReference!);
        }

        DevelopmentInbox inbox = new(folder.Path);

        Assert.Equal([ids[2], ids[0]], (await inbox.ListAsync(new InboxFilter { To = "SOMEONE@example.com" }, TestContext.Current.CancellationToken)).Select(message => message.Id));
        Assert.Equal([ids[2]], (await inbox.ListAsync(new InboxFilter { After = new DateTimeOffset(2026, 10, 29, 9, 0, 2, TimeSpan.Zero) }, TestContext.Current.CancellationToken)).Select(message => message.Id));
        Assert.Equal(ids[1], (await inbox.LatestAsync(new InboxFilter { To = "other@example.com" }, TestContext.Current.CancellationToken))!.Id);
        Assert.Null(await inbox.LatestAsync(new InboxFilter { App = "Another" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_OfAFolderThatDoesNotExist_AnswersNone()
    {
        DevelopmentInbox inbox = new(Path.Combine(Path.GetTempPath(), "hermes-development-tests", Guid.NewGuid().ToString("N")));

        Assert.Empty(await inbox.ListAsync(new InboxFilter(), TestContext.Current.CancellationToken));
        Assert.Null(await inbox.LatestAsync(new InboxFilter(), TestContext.Current.CancellationToken));
        await inbox.ClearAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DevelopmentInbox_MadeWithoutAFolder_ReadsTheDefaultFolderAndRecordsTheMillisecondItWasMade()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        DevelopmentInbox inbox = new();

        Assert.Equal(Path.Combine(Path.GetTempPath(), "hermes-inbox"), inbox.Folder);
        Assert.InRange(inbox.MadeAt, before.AddMilliseconds(-1), DateTimeOffset.UtcNow);
        Assert.Equal(0, inbox.MadeAt.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Fact]
    public async Task WaitForAsync_WithItsTokenCancelled_ThrowsOperationCanceledException()
    {
        using ScratchFolder folder = new();
        DevelopmentInbox inbox = new(folder.Path);
        using CancellationTokenSource cancelled = new(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inbox.WaitForAsync(new InboxFilter(), TimeSpan.FromSeconds(10), cancelled.Token).AsTask());
    }

    [Fact]
    public async Task DevelopmentInbox_WithAnArgumentOutsideItsRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DevelopmentInbox(null!));
        Assert.Throws<ArgumentException>(() => new DevelopmentInbox("relative/folder"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new DevelopmentInbox(Path.GetTempPath()).WaitForAsync(new InboxFilter(), TimeSpan.FromSeconds(-1), CancellationToken.None).AsTask());
    }

    private static InboxCapture Capture(string folder)
    {
        return new InboxCapture(new InboxWriter(new InboxSettings(folder, TimeSpan.FromDays(7), 1_000, false), TimeProvider.System), "Bakery");
    }
}