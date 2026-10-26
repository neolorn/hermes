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
    /// <summary>The configuration of <paramref name="languages"/>, the first being the default; <c>en</c> where none is given.</summary>
    internal static Dictionary<string, string?> Languages(params string[] languages)
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal);
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

    internal static IHost Build(Dictionary<string, string?> configuration, Action<DeclarationBuilder>? declare = null, Action<IServiceCollection>? services = null)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        services?.Invoke(builder.Services);
        builder.Services.AddHermes(builder.Configuration, declare ?? (_ => { }));
        return builder.Build();
    }
}