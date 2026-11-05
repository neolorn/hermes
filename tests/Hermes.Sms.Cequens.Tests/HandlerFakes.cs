using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Sms.Cequens.Tests;

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

/// <summary>A primary handler whose every request fails with <paramref name="error"/>, as a refused connection or a TLS handshake that does not complete does.</summary>
internal sealed class FailingHandler(HttpRequestError error) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException(error, "The request failed before an answer.");
    }
}