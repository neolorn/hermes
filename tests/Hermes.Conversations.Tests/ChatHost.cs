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
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
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

    internal const string PublicPrefix = "/public";

    /// <summary>The header a test names a request's remote address in.</summary>
    internal const string SourceHeader = "X-Source";

    private ChatHost(WebApplication application, string connection, MembershipFake membership)
    {
        Application = application;
        Connection = connection;
        Membership = membership;
    }

    internal WebApplication Application { get; }

    internal string Connection { get; }

    internal MembershipFake Membership { get; }

    /// <summary>
    /// Starts a host, with the services <paramref name="services"/> registers before the
    /// engine's, the configuration <paramref name="configure"/> adds and the kinds
    /// <paramref name="declare"/> declares; on the database of <paramref name="shared"/> where
    /// given, as a second process of the same deployment, and its membership seam.
    /// </summary>
    internal static async Task<ChatHost> StartAsync(
        PostgresFixture postgres,
        Action<IServiceCollection>? services = null,
        ChatHost? shared = null,
        Action<Dictionary<string, string?>>? configure = null,
        Action<DeclarationBuilder>? declare = null,
        bool staff = true)
    {
        string connection = shared?.Connection ?? await postgres.NewDatabaseAsync();
        if (shared is null)
        {
            await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        }

        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Logging.ClearProviders();
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Dispatch:Enabled"] = "false",
            ["Hermes:Jobs:Enabled"] = "false",
        };
        configure?.Invoke(configuration);
        builder.Configuration.AddInMemoryCollection(configuration);
        MembershipFake membership = shared?.Membership ?? new();
        builder.Services.AddSingleton<ITemplateSource>(new AwayTemplates());
        builder.Services.AddHermesTesting(secrets => secrets.SetSecret(
            "connection",
            new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] }));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IRecipientIdentity, HeaderIdentity>();
        builder.Services.AddSingleton<IConversationMembership>(membership);
        services?.Invoke(builder.Services);
        builder.Services.AddHermes(builder.Configuration, declare ?? (_ => { }));
        builder.Services.AddHermesConversations();
        WebApplication application = builder.Build();
        application.Use((context, next) =>
        {
            if (context.Request.Headers[SourceHeader].ToString() is { Length: > 0 } source)
            {
                context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(source);
            }

            return next(context);
        });
        if (staff)
        {
            application.MapHermesStaffConversations(Prefix);
        }

        application.MapHermesPublicConversations(PublicPrefix);
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

    /// <summary>Connects to the staff hub as <paramref name="person"/>, over long polling through the test server, waiting until the hub recorded the connection where <paramref name="recorded"/>.</summary>
    internal async Task<HubConnection> ConnectAsync(string person, bool recorded = true)
    {
        TestServer server = Application.GetTestServer();
        HubConnection hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, Prefix + "/live"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers[PersonHeader] = person;
            })
            .Build();
        await hub.StartAsync(TestContext.Current.CancellationToken);

        // The hub records a connection once its handshake is answered, so a test waits for its row.
        for (int wait = 0; recorded && wait < 500; wait++)
        {
            object? held = await ScalarAsync(database =>
            {
                NpgsqlCommand command = new("SELECT count(*) FROM messaging.connections WHERE person = @person", database);
                command.Parameters.AddWithValue("person", person);
                return command;
            });
            if (held is long and > 0)
            {
                break;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return hub;
    }

    /// <summary>Connects <paramref name="browser"/> to the public hub, with its cookie or its person, over long polling through the test server.</summary>
    internal async Task<HubConnection> ConnectPublicAsync(Browser browser)
    {
        TestServer server = Application.GetTestServer();
        HubConnection hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, PublicPrefix + "/live"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (browser.Cookie is { } cookie)
                {
                    options.Headers["Cookie"] = "messaging_visitor=" + cookie;
                }

                if (browser.Person is { } person)
                {
                    options.Headers[PersonHeader] = person;
                }
            })
            .Build();
        await hub.StartAsync(TestContext.Current.CancellationToken);

        // The hub holds a connection once it has read its caller, so a test waits for it.
        Live.PublicConnections held = Application.Services.GetRequiredService<Live.PublicConnections>();
        for (int wait = 0; wait < 500 && hub.ConnectionId is { } id && held.KeysOf(id) is null; wait++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return hub;
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

    /// <summary>The templates of the away kind a test declares: a title and a body naming the conversation's type.</summary>
    private sealed class AwayTemplates : ITemplateSource
    {
        public ValueTask<string?> ReadAsync(TemplateKey key, System.Threading.CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<string?>(key.Kind is null ? null
                : key.Kind == SupportTests.ReplyKind ? key.Part == "subject" ? "About {{ topic }}" : "{{ text }}"
                : key.Part == "title" ? "A message" : "In a {{ conversationType }} conversation");
        }
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