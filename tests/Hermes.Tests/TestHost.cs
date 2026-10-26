using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Tests;

/// <summary>A host that registers the library with the configuration and declarations a test gives.</summary>
internal static class TestHost
{
    internal const string FromAddress = "notices@example.com";

    internal const string Sender = "Hermes";

    internal const string Subject = "mailto:operations@example.com";

    internal const string SubmissionHost = "smtp.example.test";

    /// <summary>
    /// The configuration of <paramref name="languages"/>, the first being the default; <c>en</c>
    /// where none is given; with the keys each channel requires where a kind uses it.
    /// </summary>
    internal static Dictionary<string, string?> Languages(params string[] languages)
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = FromAddress,
            ["Hermes:Email:Host"] = SubmissionHost,
            ["Hermes:Sms:Sender"] = Sender,
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:Push:Subject"] = Subject,
        };
        string[] tags = languages.Length == 0 ? ["en"] : languages;
        for (int index = 0; index < tags.Length; index++)
        {
            configuration[$"Hermes:Languages:{index}"] = tags[index];
        }

        configuration["Hermes:DefaultLanguage"] = tags[0];
        return configuration;
    }

    internal static IConfigurationSection Section(Dictionary<string, string?> configuration)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(configuration).Build().GetSection("Hermes");
    }

    /// <summary>
    /// A host with <paramref name="configuration"/>, the kinds and categories <paramref name="declare"/>
    /// declares, the services <paramref name="services"/> registers before <c>AddHermes</c> and
    /// <paramref name="after"/> after it, logging to <paramref name="logs"/> where given, its content root
    /// <paramref name="contentRoot"/> where given.
    /// </summary>
    internal static IHost Build(
        Dictionary<string, string?> configuration,
        Action<DeclarationBuilder>? declare = null,
        Action<IServiceCollection>? services = null,
        Action<IServiceCollection>? after = null,
        LogCapture? logs = null,
        string? contentRoot = null)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development, ContentRootPath = contentRoot });
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        services?.Invoke(builder.Services);
        builder.Services.AddHermes(builder.Configuration, declare ?? (_ => { }));
        after?.Invoke(builder.Services);
        return builder.Build();
    }
}