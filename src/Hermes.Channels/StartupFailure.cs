using System;
using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Channels;

/// <summary>Makes the exception a failed startup check stops the start with (<c>04</c> MSG-LIB-003).</summary>
internal static class StartupFailure
{
    /// <summary>The failure of <paramref name="code"/>, with <paramref name="details"/>.</summary>
    internal static MessagingStartupException Of(string code, params (string Name, string Value)[] details)
    {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        foreach ((string name, string value) in details)
        {
            named[name] = value;
        }

        return new MessagingStartupException(code, named);
    }
}