using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Channels;

/// <summary>
/// Replaces a response's content with an empty one unless its status is 2xx, since
/// Lib.Net.Http.WebPush reads an error answer's body whole, with no token and no bound, and
/// the library keeps no provider text (<c>02</c> MSG-CHAN-001).
/// </summary>
internal sealed class EmptyErrorContent : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            response.Content.Dispose();
            response.Content = new ByteArrayContent([]);
        }

        return response;
    }
}