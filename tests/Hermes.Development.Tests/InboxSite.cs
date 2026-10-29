using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>
/// A web host on the loopback address with the inbox alone, its group mapped under
/// <c>/dev/inbox</c>; a request carrying <see cref="RemoteHeader"/> is taken as coming from the
/// address it names.
/// </summary>
internal sealed class InboxSite : IAsyncDisposable
{
    internal const string Prefix = "/dev/inbox";

    /// <summary>The header naming the remote address a test's request is taken as coming from.</summary>
    internal const string RemoteHeader = "X-Test-Remote";

    private InboxSite(WebApplication app)
    {
        App = app;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    internal WebApplication App { get; }

    internal HttpClient Client { get; }

    /// <summary>Starts a site whose environment is <paramref name="environment"/>, its inbox in <paramref name="folder"/>.</summary>
    internal static async Task<InboxSite> StartAsync(string folder, string environment = "Development", bool allowRemote = false, TimeProvider? clock = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:DevelopmentInbox:Folder"] = folder,
            ["Hermes:DevelopmentInbox:AllowRemote"] = allowRemote ? "true" : "false",
        });
        builder.Logging.ClearProviders();
        if (clock is not null)
        {
            builder.Services.AddSingleton(clock);
        }

        builder.AddHermesDevelopmentInbox();
        WebApplication app = builder.Build();
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(RemoteHeader, out Microsoft.Extensions.Primitives.StringValues remote))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
            }

            return next(context);
        });
        app.MapHermesDevelopmentInbox(Prefix);
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new InboxSite(app);
    }

    /// <summary>Hands <paramref name="message"/> to the email provider and answers its record's id.</summary>
    internal async Task<string> EmailAsync(EmailMessage message)
    {
        IEmailSession session = await App.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        return Id(await session.SendAsync(message, TestContext.Current.CancellationToken));
    }

    /// <summary>Hands <paramref name="message"/> to the SMS provider and answers its record's id.</summary>
    internal async Task<string> SmsAsync(SmsMessage message)
    {
        return Id(await App.Services.GetRequiredService<ISmsProvider>().SendAsync(message, TestContext.Current.CancellationToken));
    }

    /// <summary>Hands <paramref name="request"/> to the push provider and answers its record's id.</summary>
    internal async Task<string> PushAsync(PushRequest request)
    {
        return Id(await App.Services.GetRequiredService<IPushProvider>().SendAsync(request, TestContext.Current.CancellationToken));
    }

    /// <summary>Sends <paramref name="method"/> <paramref name="route"/> under the group's prefix, with <paramref name="headers"/>.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, params (string Name, string Value)[] headers)
    {
        using HttpRequestMessage request = new(method, Prefix + route);
        foreach ((string name, string value) in headers)
        {
            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Host = value;
            }
            else
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync(TestContext.Current.CancellationToken);
        await App.DisposeAsync();
    }

    private static string Id(Attempt attempt)
    {
        return Assert.IsType<Attempt.Accepted>(attempt).ProviderReference!;
    }
}