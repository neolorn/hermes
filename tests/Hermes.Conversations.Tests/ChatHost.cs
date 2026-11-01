using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// A started web host of the engine and conversations on a migrated database of its own, the
/// staff conversations group mapped under <see cref="Prefix"/>, the membership seam a fake a
/// test fills, and a request's person named by its <c>X-Person</c> header.
/// </summary>
internal sealed class ChatHost : IAsyncDisposable
{
    internal const string Prefix = "/staff";

    internal const string PersonHeader = "X-Person";

    private ChatHost(WebApplication application, string connection, MembershipFake membership)
    {
        Application = application;
        Connection = connection;
        Membership = membership;
    }

    internal WebApplication Application { get; }

    internal string Connection { get; }

    internal MembershipFake Membership { get; }

    /// <summary>Starts a host, with the services <paramref name="services"/> registers before the engine's.</summary>
    internal static async Task<ChatHost> StartAsync(PostgresFixture postgres, Action<IServiceCollection>? services = null)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Dispatch:Enabled"] = "false",
            ["Hermes:Jobs:Enabled"] = "false",
        });
        MembershipFake membership = new();
        builder.Services.AddHermesTesting(secrets => secrets.SetSecret(
            "connection",
            new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] }));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IRecipientIdentity, HeaderIdentity>();
        builder.Services.AddSingleton<IConversationMembership>(membership);
        services?.Invoke(builder.Services);
        builder.Services.AddHermes(builder.Configuration, _ => { });
        builder.Services.AddHermesConversations();
        WebApplication application = builder.Build();
        application.MapHermesStaffConversations(Prefix);
        await application.StartAsync(TestContext.Current.CancellationToken);
        return new ChatHost(application, connection, membership);
    }

    /// <summary>Calls <see cref="IConversations"/> in a scope of its own.</summary>
    internal async Task<T> CallAsync<T>(Func<IConversations, ValueTask<T>> call)
    {
        AsyncServiceScope scope = Application.Services.CreateAsyncScope();
        await using (scope)
        {
            return await call(scope.ServiceProvider.GetRequiredService<IConversations>());
        }
    }

    /// <summary>Calls <see cref="ISubjects"/> in a scope of its own.</summary>
    internal async Task<T> SubjectsAsync<T>(Func<ISubjects, ValueTask<T>> call)
    {
        AsyncServiceScope scope = Application.Services.CreateAsyncScope();
        await using (scope)
        {
            return await call(scope.ServiceProvider.GetRequiredService<ISubjects>());
        }
    }

    /// <summary>Sends a request to <paramref name="route"/> under the prefix, as <paramref name="person"/> where given, with <paramref name="json"/> as <c>application/json</c> where given.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? person, string? json = null)
    {
        using HttpClient client = Application.GetTestClient();
        using HttpRequestMessage request = new(method, Prefix + route);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (person is not null)
        {
            request.Headers.Add(PersonHeader, person);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs the command <paramref name="make"/> makes on the host's database and answers the first column of its first row.</summary>
    internal async Task<object?> ScalarAsync(Func<NpgsqlConnection, NpgsqlCommand> make)
    {
        NpgsqlConnection database = new(Connection);
        await using (database)
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            NpgsqlCommand command = make(database);
            await using (command)
            {
                return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Application.StopAsync(TestContext.Current.CancellationToken);
        await Application.DisposeAsync();
    }

    private sealed class HeaderIdentity(IHttpContextAccessor accessor) : IRecipientIdentity
    {
        public ValueTask<string?> CurrentAsync(System.Threading.CancellationToken cancellationToken)
        {
            string? person = accessor.HttpContext?.Request.Headers[PersonHeader].ToString();
            return ValueTask.FromResult(string.IsNullOrEmpty(person) ? null : person);
        }
    }
}