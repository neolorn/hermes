using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Tests;

/// <summary>A push service in place of the network: it records each request's address and content encoding and answers 201.</summary>
internal sealed class PushServiceHandler : HttpMessageHandler
{
    private readonly Lock gate = new();

    private readonly List<(Uri? Address, string? Encoding)> requests = [];

    internal IReadOnlyList<(Uri? Address, string? Encoding)> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            requests.Add((request.RequestUri, request.Content?.Headers.ContentEncoding.FirstOrDefault()));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { RequestMessage = request });
    }
}