using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Testing.Tests;

/// <summary>The test doubles of <c>Hermes.Testing</c>, each on its own.</summary>
[Trait("Category", "unit")]
public sealed class TestingDoublesTests
{
    private static readonly byte[] P256Order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

    [Fact]
    public async Task FixedSecretSource_New_AnswersVersionOneOfEachSecretInItsFormAndNoConnection()
    {
        FixedSecretSource source = new();

        Dictionary<string, byte[]> held = [];
        foreach (string name in (string[])["contentKeys", "hashKeys", "tokenKeys", "smsReportKey", "pushVapidKeys", "emailSubmission"])
        {
            SecretVersion version = Assert.Single((await source.ReadAsync(name, TestContext.Current.CancellationToken))!.Versions);
            Assert.Equal(1, version.Version);
            held[name] = version.Value.ToArray();
        }

        Assert.Equal(32, held["contentKeys"].Length);
        Assert.True(held["hashKeys"].Length >= 32 && held["tokenKeys"].Length >= 32 && held["smsReportKey"].Length >= 16);
        Assert.True(held["pushVapidKeys"].Length == 32 && held["pushVapidKeys"].Any(value => value != 0) && held["pushVapidKeys"].AsSpan().SequenceCompareTo(P256Order) < 0);
        using JsonDocument account = JsonDocument.Parse(held["emailSubmission"]);
        Assert.False(string.IsNullOrEmpty(account.RootElement.GetProperty("userName").GetString()));
        Assert.False(string.IsNullOrEmpty(account.RootElement.GetProperty("password").GetString()));
        Assert.Equal(5, held.Where(entry => entry.Key.EndsWith("Keys", StringComparison.Ordinal) || entry.Key == "smsReportKey").Select(entry => Convert.ToHexString(entry.Value)).Distinct().Count());
        Assert.Null(await source.ReadAsync("connection", TestContext.Current.CancellationToken));
        Assert.Null(await source.ReadAsync("listenConnection", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FixedSecretSource_SetAndRemove_ChangeWhatItAnswers()
    {
        FixedSecretSource source = new();
        SecretVersions connection = new() { Versions = [new SecretVersion { Version = 1, Value = "Host=localhost"u8.ToArray() }] };

        source.SetSecret("connection", connection);
        SecretVersions? set = await source.ReadAsync("connection", TestContext.Current.CancellationToken);
        source.RemoveSecret("hashKeys");

        Assert.Same(connection, set);
        Assert.Null(await source.ReadAsync("hashKeys", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InMemoryEmailProvider_Send_KeepsTheMessageAndAnswersAsSet()
    {
        InMemoryEmailProvider provider = new();
        EmailMessage message = new() { From = new EmailAddress { Address = "notices@example.com" }, ToAddress = "someone@example.com", Subject = "S", Text = "T", MessageId = "1@example.com", Language = "en" };
        await using IEmailSession session = await provider.OpenAsync(TestContext.Current.CancellationToken);

        Attempt first = await session.SendAsync(message, TestContext.Current.CancellationToken);
        provider.AnswerWith(_ => new Attempt.Permanent(AttemptReason.RecipientRefused, 550));
        Attempt second = await session.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(null), first);
        Assert.Equal(new Attempt.Permanent(AttemptReason.RecipientRefused, 550), second);
        Assert.Equal([message, message], provider.Received);
    }

    [Fact]
    public async Task InMemorySmsProvider_ByDefault_AcceptsReadsNoBalanceAndAnswersAReportWith200()
    {
        InMemorySmsProvider provider = new();
        SmsReportRequest request = new() { Method = "POST", Query = [], Form = [], Headers = [], Body = ReadOnlyMemory<byte>.Empty };

        SmsReportReading reading = await provider.ReadReportAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.NotSupported(), await provider.BalanceAsync(TestContext.Current.CancellationToken));
        Assert.Equal((false, 200, 0), (reading.Reads, reading.Response.Status, reading.Response.Body.Length));
        provider.SetBalance(new BalanceReading.Balance(12.5m));
        Assert.Equal(new BalanceReading.Balance(12.5m), await provider.BalanceAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Hermes", true)]
    [InlineData("Shop 24", true)]
    [InlineData("201000000001", true)]
    [InlineData("", false)]
    [InlineData("A sender name", false)]
    [InlineData("Shop-24", false)]
    public void InMemorySmsProvider_IsValidSender_AppliesTheSenderRule(string sender, bool valid)
    {
        Assert.Equal(valid, new InMemorySmsProvider().IsValidSender(sender));
    }

    [Fact]
    public async Task InMemoryPushProvider_Send_KeepsTheRequestWithItsPayloadAndAnswersAsSet()
    {
        InMemoryPushProvider provider = new();
        PushRequest request = new PushRequest.WebPush("https://push.example.com/1", "key", "auth", 1, "{}"u8.ToArray(), TimeSpan.FromMinutes(1), PushUrgency.Normal, null);
        provider.AnswerWith(_ => new Attempt.Gone(AttemptReason.Gone, 410));

        Attempt answer = await provider.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal((PushPlatform.WebPush, new Attempt.Gone(AttemptReason.Gone, 410)), (provider.Platform, answer));
        Assert.Equal("{}"u8.ToArray(), Assert.IsType<PushRequest.WebPush>(Assert.Single(provider.Received)).Payload.ToArray());
    }

    [Fact]
    public async Task TestDispatcher_RunUntilIdle_RunsRoundsUntilOneTakesNothingAndAnswersTheirSum()
    {
        DispatcherFake dispatcher = new([3, 2, 0, 4]);

        int taken = await new TestDispatcher(dispatcher, TimeProvider.System).RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((5, 3), (taken, dispatcher.Rounds));
    }

    [Fact]
    public async Task TestDispatcher_RunUntilIdleCancelled_Throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TestDispatcher(new DispatcherFake([1]), TimeProvider.System).RunUntilIdleAsync(cancelled.Token).AsTask());
    }

    [Fact]
    public void AddHermesTesting_EachDouble_IsOneSingletonByItsTypeAndItsContractAndALaterSecretSourceReplacesIt()
    {
        ServiceCollection services = new();
        bool configured = false;
        services.AddHermesTesting(_ => configured = true);
        ISecretSource later = new FixedSecretSource();
        services.AddSingleton(later);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.True(configured);
        Assert.Same(provider.GetRequiredService<InMemoryEmailProvider>(), provider.GetRequiredService<IEmailProvider>());
        Assert.Same(provider.GetRequiredService<InMemorySmsProvider>(), provider.GetRequiredService<ISmsProvider>());
        Assert.Same(provider.GetRequiredService<InMemoryPushProvider>(), provider.GetRequiredService<IPushProvider>());
        Assert.Same(later, provider.GetRequiredService<ISecretSource>());
        Assert.NotSame(later, provider.GetRequiredService<FixedSecretSource>());
    }

    /// <summary>A dispatcher whose rounds take the numbers given, in order, honouring its token as the library's does.</summary>
    private sealed class DispatcherFake(int[] rounds) : IDispatcher
    {
        internal int Rounds { get; private set; }

        public ValueTask<int> RunPassAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(rounds[Rounds++]);
        }
    }
}