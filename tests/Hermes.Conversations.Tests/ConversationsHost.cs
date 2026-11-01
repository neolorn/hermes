using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Testing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>A host of the engine and, where a test registers it, the conversations package, on a database of its own.</summary>
internal static class ConversationsHost
{
    /// <summary>Builds a host on a migrated database of its own: <c>en</c> alone, nothing dispatched and no job run.</summary>
    internal static async Task<IHost> BuildAsync(
        PostgresFixture postgres,
        Action<IServiceCollection, IConfiguration> register,
        Action<Dictionary<string, string?>>? configure = null)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Dispatch:Enabled"] = "false",
            ["Hermes:Jobs:Enabled"] = "false",
        };
        configure?.Invoke(configuration);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddHermesTesting(secrets => secrets.SetSecret(
            "connection",
            new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] }));
        register(builder.Services, builder.Configuration);
        return builder.Build();
    }

    /// <summary>Registers the engine with no kind declared, and a membership seam that answers no one a member.</summary>
    internal static void AddEngine(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConversationMembership>(new MembershipFake());
        services.AddHermes(configuration, _ => { });
    }
}