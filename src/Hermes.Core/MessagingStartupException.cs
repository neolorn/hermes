using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Core;

/// <summary>
/// The exception in which a "(startup)" code of the library's reference is thrown while the
/// host starts: a declaration, a configuration value, a seam, a secret or the schema outside
/// its rule stops the start before any hosted service starts or a request is served.
/// </summary>
public sealed class MessagingStartupException : Exception
{
    /// <summary>Makes the exception for <paramref name="code"/> with its <paramref name="details"/>.</summary>
    /// <param name="code">The startup code, such as <c>messaging.startup.configurationinvalid</c>.</param>
    /// <param name="details">The code's details, such as <c>key</c>; never a secret, a destination or content.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="details"/> is null.</exception>
    public MessagingStartupException(string code, IReadOnlyDictionary<string, string> details)
        : base(Describe(code, details))
    {
        Code = code;
        Details = details.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The startup code.</summary>
    public string Code { get; }

    /// <summary>The code's details, by name.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    private static string Describe(string code, IReadOnlyDictionary<string, string> details)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(details);
        string named = string.Join(", ", details.OrderBy(detail => detail.Key, StringComparer.Ordinal).Select(detail => $"{detail.Key} {detail.Value}"));
        return details.Count == 0 ? $"The host did not start: {code}." : $"The host did not start: {code} ({named}).";
    }
}