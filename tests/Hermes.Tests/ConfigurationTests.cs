using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class ConfigurationTests
{
    [Fact]
    public void MSG_OPS_001_AC1_EveryKeyAbsentHasItsDefault()
    {
        using IHost host = TestHost.Build(TestHost.Languages());

        MessagingOptions options = host.Services.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Null(options.PublicBaseAddress);
        Assert.Equal("UTC", options.TimeZone);
        Assert.Equal(100_000, options.Templates.MaxSteps);
        Assert.Equal(20, options.Templates.MaxRecursion);
        Assert.True(options.Messages.Enabled);
        Assert.True(options.Dispatch.Enabled);
        Assert.True(options.Dispatch.Hosted);
        Assert.True(options.Dispatch.Notify);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Dispatch.Interval);
        Assert.Equal(100, options.Dispatch.BatchSize);
        Assert.Equal(4, options.Dispatch.MaxPasses);
        Assert.Equal(TimeSpan.FromSeconds(20), options.Dispatch.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(2), options.Dispatch.ClaimTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Dispatch.LagThreshold);
        Assert.Equal(TimeSpan.FromDays(7), options.Dispatch.MaxHold);
        Assert.False(options.Observer.Enabled);
        Assert.Equal(4, options.Observer.MaxCalls);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Observer.LagThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Retry.Initial);
        Assert.Equal(2d, options.Retry.Factor);
        Assert.Equal(TimeSpan.FromHours(1), options.Retry.Max);
        Assert.Equal(0.2, options.Retry.Jitter);
        Assert.Equal(8, options.Retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromDays(1), options.Idempotency.Window);
        Assert.Equal(new TimeOnly(21, 0), options.QuietHours.Start);
        Assert.Equal(new TimeOnly(9, 0), options.QuietHours.End);
        Assert.Equal(TimeSpan.FromMinutes(30), options.QuietHours.Spread);
        Assert.Equal(TimeSpan.FromDays(30), options.Retention.Content);
        Assert.Equal(TimeSpan.FromDays(365), options.Retention.Record);
        Assert.Equal(TimeSpan.FromDays(31), options.Retention.Places);
        Assert.Null(options.Retention.Suppression.Email);
        Assert.Null(options.Retention.Suppression.Sms);
        Assert.True(options.Jobs.Enabled);
        Assert.Equal(new TimeOnly(3, 0), options.Jobs.RetentionAt);
        Assert.Equal(TimeSpan.FromHours(1), options.Jobs.SubscriptionsInterval);
        Assert.Equal(1_000, options.Jobs.BatchSize);
        Assert.Equal(3d, options.Jobs.StallFactor);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Jobs.ClaimTimeout);
        Assert.Equal(3, options.Database.Retries);
        Assert.Equal(TimeSpan.FromHours(1), options.Alerts.Repeat);
        Assert.Equal(TimeSpan.FromDays(730), options.Unsubscribe.TokenLifetime);
        Assert.Equal(TimeSpan.FromDays(7), options.Secrets.RetireAfter);
        Assert.Null(options.Secrets.ContentKeys.WriteVersion);
        Assert.Equal(2_147_483_648L, options.Secrets.ContentKeys.MaxValues);
        Assert.Equal(10_000, options.Secrets.ContentKeys.ReserveBlock);
        Assert.Null(options.Secrets.HashKeys.WriteVersion);
        Assert.Null(options.Secrets.TokenKeys.WriteVersion);
        Assert.Null(options.Secrets.SmsReportKey.WriteVersion);
        Assert.Null(options.Secrets.PushVapidKeys.WriteVersion);
    }

    [Fact]
    public void MSG_OPS_001_AC1_KeyPresentIsBound()
    {
        Dictionary<string, string?> configuration = TestHost.Languages("en", "ar");
        configuration["Hermes:DefaultLanguage"] = "ar";
        configuration["Hermes:TimeZone"] = "Africa/Cairo";
        configuration["Hermes:PublicBaseAddress"] = "https://messages.example.invalid/notifications";
        configuration["Hermes:Dispatch:Interval"] = "00:00:30";
        configuration["Hermes:Retry:Factor"] = "1.5";
        configuration["Hermes:Retry:Jitter"] = "0.5";
        configuration["Hermes:Jobs:StallFactor"] = "2.5";
        configuration["Hermes:QuietHours:Start"] = "22:30";
        configuration["Hermes:Retention:Suppression:Email"] = "400.00:00:00";
        configuration["Hermes:Secrets:ContentKeys:MaxValues"] = "4294967296";
        configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
        using IHost host = TestHost.Build(configuration);

        MessagingOptions options = host.Services.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Equal(["en", "ar"], options.Languages);
        Assert.Equal("ar", options.DefaultLanguage);
        Assert.Equal("Africa/Cairo", options.TimeZone);
        Assert.Equal(new Uri("https://messages.example.invalid/notifications"), options.PublicBaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Dispatch.Interval);
        Assert.Equal(1.5, options.Retry.Factor);
        Assert.Equal(0.5, options.Retry.Jitter);
        Assert.Equal(2.5, options.Jobs.StallFactor);
        Assert.Equal(new TimeOnly(22, 30), options.QuietHours.Start);
        Assert.Equal(TimeSpan.FromDays(400), options.Retention.Suppression.Email);
        Assert.Equal(4_294_967_296L, options.Secrets.ContentKeys.MaxValues);
        Assert.Equal(2, options.Secrets.HashKeys.WriteVersion);
    }

    [Theory]
    [InlineData("TimeZone", "Mars/Olympus_Mons")]
    [InlineData("TimeZone", "")]
    [InlineData("PublicBaseAddress", "http://messages.example.invalid/notifications")]
    [InlineData("PublicBaseAddress", "/notifications")]
    [InlineData("PublicBaseAddress", "")]
    [InlineData("Templates:MaxSteps", "999")]
    [InlineData("Templates:MaxSteps", "10000001")]
    [InlineData("Templates:MaxSteps", "many")]
    [InlineData("Templates:MaxRecursion", "0")]
    [InlineData("Templates:MaxRecursion", "101")]
    [InlineData("Messages:Enabled", "yes")]
    [InlineData("Dispatch:Enabled", "")]
    [InlineData("Dispatch:Hosted", "1")]
    [InlineData("Dispatch:Notify", "on")]
    [InlineData("Dispatch:Interval", "00:00:02")]
    [InlineData("Dispatch:Interval", "00:05:01")]
    [InlineData("Dispatch:Interval", "five seconds")]
    [InlineData("Dispatch:BatchSize", "0")]
    [InlineData("Dispatch:BatchSize", "1001")]
    [InlineData("Dispatch:MaxPasses", "0")]
    [InlineData("Dispatch:MaxPasses", "33")]
    [InlineData("Dispatch:Timeout", "00:00:00.999")]
    [InlineData("Dispatch:Timeout", "00:02:01")]
    [InlineData("Dispatch:ClaimTimeout", "00:01:09")]
    [InlineData("Dispatch:ClaimTimeout", "01:00:01")]
    [InlineData("Dispatch:LagThreshold", "00:00:05")]
    [InlineData("Dispatch:LagThreshold", "1.00:00:01")]
    [InlineData("Dispatch:MaxHold", "00:59:59")]
    [InlineData("Dispatch:MaxHold", "90.00:00:01")]
    [InlineData("Observer:Enabled", "no")]
    [InlineData("Observer:MaxCalls", "0")]
    [InlineData("Observer:MaxCalls", "65")]
    [InlineData("Observer:LagThreshold", "00:00:05")]
    [InlineData("Observer:LagThreshold", "1.00:00:01")]
    [InlineData("Retry:Initial", "00:00:00.5")]
    [InlineData("Retry:Initial", "01:00:01")]
    [InlineData("Retry:Factor", "0.99")]
    [InlineData("Retry:Factor", "10.01")]
    [InlineData("Retry:Factor", "twice")]
    [InlineData("Retry:Factor", "1,5")]
    [InlineData("Retry:Max", "00:00:29")]
    [InlineData("Retry:Max", "1.00:00:01")]
    [InlineData("Retry:Jitter", "-0.1")]
    [InlineData("Retry:Jitter", "1.1")]
    [InlineData("Retry:Jitter", "a fifth")]
    [InlineData("Retry:MaxAttempts", "0")]
    [InlineData("Retry:MaxAttempts", "51")]
    [InlineData("Idempotency:Window", "00:00:59")]
    [InlineData("Idempotency:Window", "30.00:00:01")]
    [InlineData("QuietHours:Start", "25:00")]
    [InlineData("QuietHours:End", "evening")]
    [InlineData("QuietHours:Spread", "-00:00:01")]
    [InlineData("QuietHours:Spread", "04:00:01")]
    [InlineData("Retention:Content", "23:59:59")]
    [InlineData("Retention:Content", "1095.00:00:01")]
    [InlineData("Retention:Record", "29.23:59:59")]
    [InlineData("Retention:Record", "3650.00:00:01")]
    [InlineData("Retention:Places", "23:59:59")]
    [InlineData("Retention:Places", "365.00:00:01")]
    [InlineData("Retention:Suppression:Email", "29.23:59:59")]
    [InlineData("Retention:Suppression:Email", "3650.00:00:01")]
    [InlineData("Retention:Suppression:Sms", "29.23:59:59")]
    [InlineData("Retention:Suppression:Sms", "3650.00:00:01")]
    [InlineData("Jobs:Enabled", "off")]
    [InlineData("Jobs:RetentionAt", "3 in the morning")]
    [InlineData("Jobs:SubscriptionsInterval", "00:00:59")]
    [InlineData("Jobs:SubscriptionsInterval", "1.00:00:01")]
    [InlineData("Jobs:BatchSize", "99")]
    [InlineData("Jobs:BatchSize", "100001")]
    [InlineData("Jobs:StallFactor", "1.99")]
    [InlineData("Jobs:StallFactor", "100.01")]
    [InlineData("Jobs:StallFactor", "NaN")]
    [InlineData("Jobs:ClaimTimeout", "00:01:59")]
    [InlineData("Jobs:ClaimTimeout", "1.00:00:01")]
    [InlineData("Database:Retries", "-1")]
    [InlineData("Database:Retries", "11")]
    [InlineData("Alerts:Repeat", "00:04:59")]
    [InlineData("Alerts:Repeat", "1.00:00:01")]
    [InlineData("Unsubscribe:TokenLifetime", "179.23:59:59")]
    [InlineData("Unsubscribe:TokenLifetime", "3650.00:00:01")]
    [InlineData("Secrets:RetireAfter", "23:59:59")]
    [InlineData("Secrets:RetireAfter", "365.00:00:01")]
    [InlineData("Secrets:ContentKeys:MaxValues", "0")]
    [InlineData("Secrets:ContentKeys:MaxValues", "4294967297")]
    [InlineData("Secrets:ContentKeys:ReserveBlock", "99")]
    [InlineData("Secrets:ContentKeys:ReserveBlock", "1000001")]
    public async Task MSG_OPS_001_AC1_KeyOutsideItsRuleStopsTheStart(string key, string value)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:" + key] = value;
        using IHost host = TestHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Dispatch:Timeout", "00:00:46", "Dispatch:ClaimTimeout")]
    [InlineData("Dispatch:Interval", "00:05:00", "Dispatch:LagThreshold")]
    [InlineData("Retry:Initial", "01:00:01", "Retry:Initial")]
    [InlineData("Retention:Content", "400.00:00:00", "Retention:Record")]
    [InlineData("Dispatch:ClaimTimeout", "00:15:00", "Jobs:ClaimTimeout")]
    public async Task MSG_LIB_003_AC2_ValueThatPutsAnotherKeyOutsideItsRuleStopsTheStartNamingThatKey(string key, string value, string named)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:" + key] = value;
        using IHost host = TestHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(named, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Dispatch:Interval", "00:00:03")]
    [InlineData("Dispatch:MaxHold", "90.00:00:00")]
    [InlineData("Dispatch:ClaimTimeout", "00:01:10")]
    [InlineData("Dispatch:Timeout", "00:00:01")]
    [InlineData("Retry:Factor", "1")]
    [InlineData("Retry:Factor", "10")]
    [InlineData("Jobs:StallFactor", "2")]
    [InlineData("Jobs:StallFactor", "100")]
    [InlineData("Retry:Jitter", "0")]
    [InlineData("Retry:Jitter", "1")]
    [InlineData("QuietHours:End", "21:00")]
    [InlineData("QuietHours:Spread", "00:00:00")]
    [InlineData("Retention:Content", "1.00:00:00")]
    [InlineData("Retention:Suppression:Sms", "30.00:00:00")]
    [InlineData("Secrets:ContentKeys:MaxValues", "4294967296")]
    [InlineData("Unsubscribe:TokenLifetime", "3650.00:00:00")]
    [InlineData("TimeZone", "UTC")]
    [InlineData("TimeZone", "Asia/Tokyo")]
    [InlineData("PublicBaseAddress", "https://messages.example.invalid/notifications")]
    public void MSG_OPS_001_AC1_KeyAtTheEdgeOfItsRuleIsWithinIt(string key, string value)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:" + key] = value;

        Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), AppContext.BaseDirectory));
    }

    [Fact]
    public void MSG_OPS_001_AC1_DecimalNumberIsReadInTheInvariantCultureWhateverTheProcessCulture()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Retry:Factor"] = "1.5";
        configuration["Hermes:Jobs:StallFactor"] = "2.25";
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using IHost host = TestHost.Build(configuration);
            MessagingOptions options = host.Services.GetRequiredService<IOptions<MessagingOptions>>().Value;

            Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), AppContext.BaseDirectory));
            Assert.Equal((1.5, 2.25), (options.Retry.Factor, options.Jobs.StallFactor));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void MSG_OPS_001_AC1_WriteVersionIsNotCheckedWithTheOtherKeys()
    {
        // A write version is checked only where its key is needed (06 MSG-OPS-008).
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Secrets:TokenKeys:WriteVersion"] = "the second";

        Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), AppContext.BaseDirectory));
    }

    [Theory]
    [InlineData("", "en", "Languages")]
    [InlineData("en", null, "DefaultLanguage")]
    [InlineData("en", "fr", "DefaultLanguage")]
    [InlineData("en,en_US", "en", "Languages")]
    [InlineData("en,qq", "en", "Languages")]
    [InlineData("en,", "en", "Languages")]
    [InlineData("en,x-private", "en", "Languages")]
    public async Task MSG_OPS_001_AC1_LanguagesOutsideTheirRuleStopTheStart(string languages, string? defaultLanguage, string key)
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal) { ["Hermes:DefaultLanguage"] = defaultLanguage };
        string[] tags = string.IsNullOrEmpty(languages) ? [] : languages.Split(',');
        for (int index = 0; index < tags.Length; index++)
        {
            configuration[$"Hermes:Languages:{index}"] = tags[index];
        }

        using IHost host = TestHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_TPL_003_AC2_ADeploymentDeclaringALanguageThatResolvesOnlyToTheInvariantCultureDoesNotStart()
    {
        Dictionary<string, string?> configuration = TestHost.Languages("en", "und");
        using IHost host = TestHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(("messaging.startup.configurationinvalid", "Languages"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public void MSG_OPS_001_AC1_DefaultLanguageMatchesItsLanguageWhateverItsCase()
    {
        Dictionary<string, string?> configuration = TestHost.Languages("en-GB", "ar-EG");
        configuration["Hermes:DefaultLanguage"] = "AR-eg";

        Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), AppContext.BaseDirectory));
    }

    [Fact]
    public void MSG_OPS_001_AC1_WindowsTimeZoneIdentifierIsOutsideTheRule()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:TimeZone"] = "Egypt Standard Time";

        Assert.Equal("TimeZone", ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), AppContext.BaseDirectory));
    }

    [Fact]
    public void InvalidKey_SystemOfferingNoTimeZoneButUtc_NamesTheTimeZone()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();

        Assert.Equal("TimeZone", ConfigurationCheck.InvalidKey(TestHost.Section(configuration), [TimeZoneInfo.Utc], AppContext.BaseDirectory));
    }
}