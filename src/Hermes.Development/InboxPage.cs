using System;
using System.IO;

namespace Hermes.Development;

/// <summary>
/// The inbox's page, script and style (<c>02</c> MSG-DEV-004), embedded in the package; the
/// page names its own address, the group's under the host's base path, by <see cref="Root"/>.
/// </summary>
internal static class InboxPage
{
    /// <summary>The token the page holds where its own address goes.</summary>
    internal const string Root = "{{root}}";

    private static readonly Lazy<string> HtmlText = new(() => Read("inbox.html"));

    private static readonly Lazy<string> ScriptText = new(() => Read("inbox.js"));

    private static readonly Lazy<string> StyleText = new(() => Read("inbox.css"));

    internal static string Html => HtmlText.Value;

    internal static string Script => ScriptText.Value;

    internal static string Style => StyleText.Value;

    private static string Read(string name)
    {
        using Stream stream = typeof(InboxPage).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The package holds no {name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}