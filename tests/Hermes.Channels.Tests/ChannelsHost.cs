using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Channels.Tests;

/// <summary>A host that registers the providers alone, over configuration a test gives it.</summary>
internal static class ChannelsHost
{
    internal const string FromAddress = "notices@example.com";

    internal const string Sender = "Hermes";

    internal const string Subject = "mailto:operations@example.com";

    internal const string SubmissionHost = "smtp.example.test";

    internal const string User = "notices";

    /// <summary>The submission account's password, made at run time.</summary>
    internal static readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>The keys each channel requires, every channel used, the shipped email and push providers among them.</summary>
    internal static Dictionary<string, string?> Used()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = FromAddress,
            ["Hermes:Email:Host"] = SubmissionHost,
            ["Hermes:Sms:Sender"] = Sender,
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:Push:Subject"] = Subject,
        };
    }

    /// <summary>A secret source holding one valid version of <c>emailSubmission</c> and of <c>pushVapidKeys</c>.</summary>
    internal static SecretSourceFake Secrets()
    {
        return new SecretSourceFake()
            .WithVersions("emailSubmission", (1, Account(User, Password)))
            .WithVersions("pushVapidKeys", (1, PushHarness.Scalar()));
    }

    /// <summary>The secret <c>emailSubmission</c> of <paramref name="user"/> and <paramref name="password"/>.</summary>
    internal static byte[] Account(string user, string password)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["userName"] = user, ["password"] = password });
    }

    /// <summary>
    /// A host over <paramref name="configuration"/>, the services <paramref name="before"/>
    /// registers added before <c>AddHermesChannels</c> and those <paramref name="after"/>
    /// registers after it.
    /// </summary>
    internal static IHost Build(IConfiguration configuration, Action<IServiceCollection>? before = null, Action<IServiceCollection>? after = null)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        // The library ships no SMS provider, so a host using SMS registers its own; a test's own replaces this one.
        builder.Services.AddSingleton<Core.ISmsProvider>(new AnySenderSmsProvider());
        before?.Invoke(builder.Services);
        builder.Services.AddHermesChannels(configuration);
        after?.Invoke(builder.Services);
        return builder.Build();
    }

    /// <summary>A host over <paramref name="values"/>, holding <see cref="Secrets"/> unless <paramref name="before"/> is given.</summary>
    internal static IHost Build(Dictionary<string, string?> values, Action<IServiceCollection>? before = null, Action<IServiceCollection>? after = null)
    {
        return Build(Root(values), before ?? (services => services.AddSingleton<Core.ISecretSource>(Secrets())), after);
    }

    internal static IConfigurationRoot Root(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}