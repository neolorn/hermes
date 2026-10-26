using System;
using System.Collections.Generic;

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

    /// <summary>The keys each channel requires, every channel used, the shipped push provider among them.</summary>
    internal static Dictionary<string, string?> Used()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = FromAddress,
            ["Hermes:Sms:Sender"] = Sender,
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:Push:Subject"] = Subject,
        };
    }

    /// <summary>A secret source holding one valid version of <c>pushVapidKeys</c>.</summary>
    internal static SecretSourceFake Secrets()
    {
        return new SecretSourceFake().WithVersions("pushVapidKeys", (1, PushHarness.Scalar()));
    }

    /// <summary>
    /// A host over <paramref name="configuration"/>, the services <paramref name="before"/>
    /// registers added before <c>AddHermesChannels</c> and those <paramref name="after"/>
    /// registers after it.
    /// </summary>
    internal static IHost Build(IConfiguration configuration, Action<IServiceCollection>? before = null, Action<IServiceCollection>? after = null)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
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