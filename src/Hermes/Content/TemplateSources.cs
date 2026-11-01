using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>
/// Where a template part is read from (<c>03</c> MSG-TPL-006): the host's template source,
/// where it registers one; for a key it answers none, the file under
/// <c>Hermes:Templates:Path</c>, then the embedded resources of each assembly the host passed,
/// matched by a logical name equal to that file's relative path, then, for a page, the
/// library's shipped template.
/// </summary>
/// <param name="host">The host's template source, or none.</param>
/// <param name="folder">The full path of <c>Hermes:Templates:Path</c>, which may name nothing.</param>
/// <param name="assemblies">The assemblies the host passed, in the order it passed them.</param>
internal sealed class TemplateSources(ITemplateSource? host, string folder, IReadOnlyList<Assembly> assemblies)
{
    private static readonly Assembly Library = typeof(TemplateSources).Assembly;

    /// <summary>The part's text, or none where no source holds it.</summary>
    internal async ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (host is not null && await host.ReadAsync(key, cancellationToken).ConfigureAwait(false) is { } hosted)
        {
            return hosted;
        }

        string relative = RelativePath(key);
        string file = Path.Combine(folder, relative);
        if (File.Exists(file))
        {
            return await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        }

        foreach (Assembly assembly in assemblies)
        {
            if (await ResourceAsync(assembly, relative, cancellationToken).ConfigureAwait(false) is { } embedded)
            {
                return embedded;
            }
        }

        return key.Page is null ? null : await ResourceAsync(Library, relative, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The part's path under the folder, with <c>/</c> between its folder and its file:
    /// <c>&lt;kind&gt;/&lt;channel&gt;.&lt;language&gt;.&lt;part&gt;.liquid</c>, for a page
    /// <c>pages/&lt;page&gt;.&lt;language&gt;.&lt;part&gt;.liquid</c>, or for an automatic reply
    /// <c>replies/&lt;name&gt;.&lt;language&gt;.text.liquid</c> (<c>09</c> MSG-CONV-003).
    /// </summary>
    internal static string RelativePath(TemplateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Reply is { } reply)
        {
            return $"replies/{reply}.{key.Language}.{key.Part}.liquid";
        }

        return key.Page is { } page
            ? $"pages/{page}.{key.Language}.{key.Part}.liquid"
            : $"{key.Kind}/{Spellings.Of(key.Channel ?? throw new ArgumentException("A kind's key names its channel.", nameof(key)))}.{key.Language}.{key.Part}.liquid";
    }

    private static async ValueTask<string?> ResourceAsync(Assembly assembly, string name, CancellationToken cancellationToken)
    {
        Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}