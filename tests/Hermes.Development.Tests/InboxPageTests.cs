using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The inbox's page, script and style (<c>02</c> MSG-DEV-004).</summary>
[Trait("Category", "unit")]
public sealed partial class InboxPageTests
{
    [Fact]
    public async Task MSG_DEV_004_AC1_ThePageReferencesOnlyItsOwnScriptAndStyleUnderTheGroupsAddressAndAnEmptyIcon()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        string page = await TextAsync(site, "/");

        Assert.Equal(
            [$"{InboxSite.Prefix}/assets/inbox.css", $"{InboxSite.Prefix}/assets/inbox.js", "data:,"],
            Reference().Matches(page).Select(match => match.Groups["address"].Value).Order(StringComparer.Ordinal));
        Assert.Contains($"data-root=\"{InboxSite.Prefix}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<style", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(InlineScript(), page);
    }

    [Fact]
    public async Task MSG_DEV_004_AC1_TheScriptAndStyleReferenceNoResourceOutsideTheApplication()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        string script = await TextAsync(site, "/assets/inbox.js");
        string style = await TextAsync(site, "/assets/inbox.css");

        Assert.DoesNotMatch(Address(), script);
        Assert.DoesNotMatch(Address(), style);
        Assert.DoesNotMatch(@"\bimport\b|importScripts|new\s+Worker|\beval\s*\(|new\s+Function\b", script);
        Assert.DoesNotMatch(@"@import|url\(", style);
        Assert.Matches(@"fetch\(root \+ ""/messages", script);
    }

    [Fact]
    public async Task MSG_DEV_004_AC2_TheScriptWritesNoValueAsMarkup()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        string script = await TextAsync(site, "/assets/inbox.js");

        Assert.DoesNotMatch(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write|srcdoc|DOMParser|createContextualFragment", script);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
        Assert.Contains("\"allow-popups allow-popups-to-escape-sandbox\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-scripts", script, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-same-origin", script, StringComparison.Ordinal);
        Assert.Contains("\"noopener noreferrer\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_DEV_004_AC3_TheScriptAsksForNewMessagesEverySecondAfterTheNewestItHolds()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        string script = await TextAsync(site, "/assets/inbox.js");

        Assert.Contains("const every = 1000;", script, StringComparison.Ordinal);
        Assert.Contains("\"/messages?after=\" + encodeURIComponent(newest)", script, StringComparison.Ordinal);
    }

    private static async Task<string> TextAsync(InboxSite site, string route)
    {
        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, route);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        return await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An address an element of the page loads or links to.</summary>
    [GeneratedRegex(@"\b(?:src|href)=""(?<address>[^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    /// <summary>A script element with content of its own.</summary>
    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    /// <summary>An absolute or scheme-relative address.</summary>
    [GeneratedRegex(@"[a-z][a-z0-9+.-]*://|[""'(]//", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Address();
}