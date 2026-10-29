using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

using Hermes;
using Hermes.Core;
using Hermes.Testing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Bakery.Tests;

/// <summary>
/// The bakery's site in the test process, on a database whose migrations
/// <see cref="MessagingTestDatabase"/> applied, sending through the in-memory providers of
/// <c>AddHermesTesting</c>, which are used in place of the development inbox's; the inbox's
/// folder is one of the test's own.
/// </summary>
internal sealed class BakeryFactory(string connection) : WebApplicationFactory<Program>
{
    /// <summary>The development inbox's folder, which the in-memory providers leave empty.</summary>
    internal string InboxFolder { get; } = Path.Combine(Path.GetTempPath(), "bakery-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Starts the site on a new database of <paramref name="postgres"/>.</summary>
    internal static async Task<BakeryFactory> StartAsync(PostgresFixture postgres)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        BakeryFactory factory = new(connection);
        _ = factory.Server;
        return factory;
    }

    /// <summary>A client signed in as <paramref name="person"/>, its cookie kept.</summary>
    internal async Task<HttpClient> SignInAsync(string person)
    {
        HttpClient client = CreateClient();
        using HttpResponseMessage signedIn = await client.PostAsJsonAsync("/account/sign-in", new { person }, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, signedIn.StatusCode);
        return client;
    }

    /// <summary>Runs dispatch passes until one takes nothing, as the hosted dispatcher would, and answers how many deliveries they took.</summary>
    internal async Task<int> DispatchAsync()
    {
        return await new TestDispatcher(Services.GetRequiredService<IDispatcher>(), TimeProvider.System).RunUntilIdleAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(InboxFolder))
        {
            Directory.Delete(InboxFolder, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Bakery", connection);
        builder.UseSetting("Hermes:DevelopmentInbox:Folder", InboxFolder);
        builder.ConfigureServices(services => services.AddHermesTesting(secrets => secrets.SetSecret(
            "connection",
            new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] })));
    }
}