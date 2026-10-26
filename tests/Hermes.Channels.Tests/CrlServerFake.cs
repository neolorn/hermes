using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Channels.Tests;

/// <summary>
/// A listener in the test process serving revocation lists over HTTP on the loopback
/// address (<c>07</c>): each list at an address of its own, so no list cached by the platform
/// is read for another; an address with no list answers 404.
/// </summary>
internal sealed class CrlServerFake : IAsyncDisposable
{
    private readonly WebApplication application;
    private readonly ConcurrentDictionary<string, byte[]> lists = new(StringComparer.Ordinal);
    private int requests;

    private CrlServerFake(WebApplication application)
    {
        this.application = application;
    }

    /// <summary>The requests received.</summary>
    internal int Requests => Volatile.Read(ref requests);

    private Uri Base { get; set; } = null!;

    internal static async Task<CrlServerFake> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrelCore().ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        WebApplication application = builder.Build();
        CrlServerFake server = new(application);
        application.Run(server.AnswerAsync);
        await application.StartAsync().ConfigureAwait(false);
        IServerAddressesFeature addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The listener names no address.");
        string address = addresses.Addresses.Single();
        server.Base = new Uri(address);
        return server;
    }

    /// <summary>A new address, serving nothing until a list is published at it.</summary>
    internal Uri Reserve()
    {
        return new Uri(Base, "/" + Guid.NewGuid().ToString("N") + ".crl");
    }

    /// <summary>Serves <paramref name="crl"/> at <paramref name="address"/>.</summary>
    internal void Publish(Uri address, byte[] crl)
    {
        lists[address.AbsolutePath] = crl;
    }

    public async ValueTask DisposeAsync()
    {
        await application.DisposeAsync().ConfigureAwait(false);
    }

    private async Task AnswerAsync(HttpContext context)
    {
        Interlocked.Increment(ref requests);
        if (!lists.TryGetValue(context.Request.Path.Value ?? string.Empty, out byte[]? crl))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "application/pkix-crl";
        await context.Response.Body.WriteAsync(crl, context.RequestAborted).ConfigureAwait(false);
    }
}