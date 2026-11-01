using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// A browser on the public side of a <see cref="ChatHost"/>: it keeps the cookie
/// <c>messaging_visitor</c> the host sets, sends it back, and names its remote address and,
/// for a signed-in customer, its person.
/// </summary>
internal sealed class Browser(ChatHost host, string? person = null, string source = "192.0.2.10")
{
    /// <summary>The cookie's value, once the host set one.</summary>
    internal string? Cookie { get; set; }

    /// <summary>The last <c>Set-Cookie</c> the host answered, as it was written.</summary>
    internal string? SetCookie { get; private set; }

    internal string? Person { get; } = person;

    internal string Source { get; set; } = source;

    /// <summary>The <c>Accept-Language</c> the browser sends, none where none.</summary>
    internal string? Language { get; set; }

    /// <summary>Sends a request to <paramref name="route"/> under the public prefix, with <paramref name="json"/> as <c>application/json</c> where given, keeping any cookie it answers.</summary>
    internal Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? json = null)
    {
        return SendAsync(method, route, () => json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"));
    }

    /// <summary>Sends a request to <paramref name="route"/> under the public prefix with the body <paramref name="content"/> makes, keeping any cookie it answers.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Func<HttpContent?> content)
    {
        using HttpClient client = host.Application.GetTestClient();
        using HttpRequestMessage request = new(method, ChatHost.PublicPrefix + route);
        request.Content = content();

        if (Person is not null)
        {
            request.Headers.Add(ChatHost.PersonHeader, Person);
        }

        if (Cookie is not null)
        {
            request.Headers.Add("Cookie", "messaging_visitor=" + Cookie);
        }

        request.Headers.Add(ChatHost.SourceHeader, Source);
        if (Language is not null)
        {
            request.Headers.Add("Accept-Language", Language);
        }

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out System.Collections.Generic.IEnumerable<string>? set) && set.FirstOrDefault(each => each.StartsWith("messaging_visitor=", StringComparison.Ordinal)) is { } kept)
        {
            SetCookie = kept;
            Cookie = kept["messaging_visitor=".Length..kept.IndexOf(';', StringComparison.Ordinal)];
        }

        return response;
    }

    /// <summary>Acknowledges the notice's <paramref name="version"/>.</summary>
    internal async Task<HttpResponseMessage> AcknowledgeAsync(string version = "v1")
    {
        return await SendAsync(HttpMethod.Post, "/support/notice", $$"""{ "version": "{{version}}" }""");
    }

    /// <summary>Starts a conversation of <paramref name="topic"/>, answering its identifier, or failing the test where it is refused.</summary>
    internal async Task<Guid> StartAsync(string topic = "billing", string text = "Hello")
    {
        using HttpResponseMessage started = await SendAsync(HttpMethod.Post, "/support/conversations", $$"""{ "topic": "{{topic}}", "text": "{{text}}" }""");
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return Guid.Parse((await JsonAsync(started)).GetProperty("id").GetString()!);
    }

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    }

}