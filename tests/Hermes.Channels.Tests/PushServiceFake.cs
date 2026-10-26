using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Hermes.Channels.Tests;

/// <summary>
/// A push service in the test process, reached by replacing the named client's primary
/// handler: it records each request and answers it with the status the test sets.
/// </summary>
internal sealed class PushServiceFake : IAsyncDisposable
{
    private readonly WebApplication application;

    private PushServiceFake(WebApplication application)
    {
        this.application = application;
    }

    /// <summary>The requests received, in order.</summary>
    internal ConcurrentQueue<PushServiceRequest> Requests { get; } = new();

    /// <summary>The status each request is answered with.</summary>
    internal int Status { get; set; } = 201;

    /// <summary>The <c>Retry-After</c> header each request is answered with, or none.</summary>
    internal string? RetryAfter { get; set; }

    /// <summary>The body each request is answered with.</summary>
    internal string Body { get; set; } = string.Empty;

    /// <summary>Whether each request waits until the caller gives up.</summary>
    internal bool Hangs { get; set; }

    internal static async Task<PushServiceFake> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        WebApplication application = builder.Build();
        PushServiceFake service = new(application);
        application.Run(service.AnswerAsync);
        await application.StartAsync().ConfigureAwait(false);
        return service;
    }

    /// <summary>A handler that reaches the service.</summary>
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
        Requests.Enqueue(new PushServiceRequest(context.Request.Method, $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}", headers, body.ToArray()));

        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, context.RequestAborted).ConfigureAwait(false);
        }

        context.Response.StatusCode = Status;
        if (RetryAfter is not null)
        {
            context.Response.Headers.RetryAfter = RetryAfter;
        }

        await context.Response.WriteAsync(Body, context.RequestAborted).ConfigureAwait(false);
    }
}

/// <summary>One request a push service received.</summary>
internal sealed record PushServiceRequest(string Method, string Address, IReadOnlyDictionary<string, string> Headers, byte[] Body);