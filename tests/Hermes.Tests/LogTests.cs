using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class LogTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-7f3c2a";

    private const string Destination = "someone.distinct@example.test";

    private static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MSG_OPS_008_NoLogEntryOfAHostsLifeHoldsASecretADestinationOrAPerson()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Start);
        using LogCapture logs = new(everyCategory: true);
        using IHost host = Host(Secrets.Held(connection), clock, logs);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            ContentProtection protection = scope.ServiceProvider.GetRequiredService<ContentProtection>();
            string context = ContentContext.Of(ContentContext.Deliveries, Guid.NewGuid(), "destination");
            ContentProtecting made = await protection.ProtectAsync(Person, Encoding.UTF8.GetBytes(Destination), context, TestContext.Current.CancellationToken);
            await protection.OpenAsync(Person, made.Stored, context, TestContext.Current.CancellationToken);
        }

        IConfigurationRoot configuration = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();
        configuration["Hermes:QuietHours:Spread"] = "05:00:00";
        configuration.Reload();
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForAsync(logs, entry => entry.Message.StartsWith("Alert raised", StringComparison.Ordinal));
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(logs.Entries, entry => entry.Message.StartsWith("Alert raised", StringComparison.Ordinal));
        AssertHoldsNoneOf(logs.Entries.Select(entry => entry.Message), connection);
    }

    [Fact]
    public async Task MSG_OPS_008_AStartStoppedOnASecretWritesNothingOfIt()
    {
        string connection = await MigratedAsync();
        using LogCapture logs = new(everyCategory: true);
        using IHost host = Host(Secrets.Held(connection).With("hashKeys", Secrets.Bytes(31, 2)), new ManualClock(Start), logs);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        AssertHoldsNoneOf([.. logs.Entries.Select(entry => entry.Message), failure.ToString()], connection, Secrets.Bytes(31, 2));
    }

    private void AssertHoldsNoneOf(IEnumerable<string> written, string connection, params byte[][] more)
    {
        byte[][] keys = [Secrets.Bytes(32, 1), Secrets.Bytes(32, 2), Secrets.Bytes(32, 3), Convert.FromHexString(Secrets.VapidScalar), .. more];
        string[] forbidden =
        [
            connection,
            postgres.Password,
            Destination,
            Person,
            .. keys.SelectMany(key => new[] { Convert.ToBase64String(key), Convert.ToHexString(key), Convert.ToHexStringLower(key) }),
        ];
        foreach (string text in written)
        {
            Assert.DoesNotContain(forbidden, secret => text.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IHost Host(SecretSourceFake secrets, TimeProvider clock, LogCapture logs)
    {
        return TestHost.Build(
            TestHost.Languages(),
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Sms();
            },
            services => services.AddSingleton<ISecretSource>(secrets).AddSingleton(clock),
            after: services => services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace)),
            logs: logs);
    }

    private async Task<string> MigratedAsync()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    private static async Task WaitForAsync(LogCapture logs, Func<LogCapture.LogEntry, bool> written)
    {
        for (int attempt = 0; attempt < 200 && !logs.Entries.Any(written); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }
    }
}