using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>
/// The Cequens gateway in the test process, reached by replacing the named client's primary
/// handler: it records each request and answers it with the status and body the test sets,
/// the gateway's documented answers (<see cref="GatewayAnswers"/>).
/// </summary>
internal sealed class GatewayFake : IAsyncDisposable
{
    private readonly WebApplication application;

    private GatewayFake(WebApplication application)
    {
        this.application = application;
    }

    /// <summary>The requests received, in order.</summary>
    internal ConcurrentQueue<GatewayRequest> Requests { get; } = new();

    /// <summary>The status each request is answered with.</summary>
    internal int Status { get; set; } = 200;

    /// <summary>The JSON body each request is answered with, none for an empty one.</summary>
    internal string? Body { get; set; } = GatewayAnswers.Sent;

    /// <summary>Whether each answer declares its body's length; otherwise the body is streamed with none.</summary>
    internal bool DeclaresLength { get; set; }

    /// <summary>Whether each request waits until the caller gives up.</summary>
    internal bool Hangs { get; set; }

    internal static async Task<GatewayFake> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        WebApplication application = builder.Build();
        GatewayFake gateway = new(application);
        application.Run(gateway.AnswerAsync);
        await application.StartAsync().ConfigureAwait(false);
        return gateway;
    }

    /// <summary>A handler that reaches the gateway.</summary>
    internal HttpMessageHandler CreateHandler()
    {
        return application.GetTestServer().CreateHandler();
    }

    public async ValueTask DisposeAsync()
    {
        await application.DisposeAsync().ConfigureAwait(false);
    }

    private async Task AnswerAsync(HttpContext context)
    {
        using MemoryStream body = new();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted).ConfigureAwait(false);
        Dictionary<string, string> headers = context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        Requests.Enqueue(new GatewayRequest(context.Request.Method, $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}", headers, Encoding.UTF8.GetString(body.ToArray())));

        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, context.RequestAborted).ConfigureAwait(false);
        }

        context.Response.StatusCode = Status;
        if (Body is not null)
        {
            context.Response.ContentType = "application/json";
            if (DeclaresLength)
            {
                context.Response.ContentLength = Encoding.UTF8.GetByteCount(Body);
            }

            await context.Response.WriteAsync(Body, context.RequestAborted).ConfigureAwait(false);
        }
    }
}

/// <summary>One request the gateway received.</summary>
internal sealed record GatewayRequest(string Method, string Address, IReadOnlyDictionary<string, string> Headers, string Body);