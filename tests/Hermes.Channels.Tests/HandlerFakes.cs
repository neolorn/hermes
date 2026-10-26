using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Channels.Tests;

/// <summary>A host's handler that sends a request again, up to three times, while the answer is 429.</summary>
internal sealed class RetryingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        for (int retry = 0; retry < 3 && response.StatusCode == HttpStatusCode.TooManyRequests; retry++)
        {
            response.Dispose();
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}

/// <summary>A primary handler whose every connection is refused.</summary>
internal sealed class RefusingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException(HttpRequestError.ConnectionError, "The connection was refused.");
    }
}