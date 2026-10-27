using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

/// <summary>What a test of the endpoint groups shares: the prefix they are mapped under, the identity seam and the requests.</summary>
internal static class Endpoints
{
    internal const string Prefix = "/notifications";

    /// <summary>Registers the identity seam that answers a request's <c>X-Person</c> header.</summary>
    internal static void Identity(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IRecipientIdentity, RecipientIdentityFake>();
    }

    /// <summary>Sends a request to <paramref name="route"/> under <paramref name="prefix"/>, as <paramref name="person"/> where given, with <paramref name="content"/>, which it disposes.</summary>
    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string route, string? person = null, HttpContent? content = null, string prefix = Prefix)
    {
        using HttpRequestMessage request = new(method, prefix + route) { Content = content };
        if (person is not null)
        {
            request.Headers.Add(RecipientIdentityFake.Header, person);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary><paramref name="json"/> sent as <c>application/json</c>.</summary>
    internal static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}