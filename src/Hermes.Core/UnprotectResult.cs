using System;
using System.Text;

namespace Hermes.Core;

/// <summary>What opening a protected value answers.</summary>
public abstract record UnprotectResult
{
    private UnprotectResult()
    {
    }

    /// <summary>The value opened; it never prints the value.</summary>
    /// <param name="Plaintext">The value.</param>
    public sealed record Opened(ReadOnlyMemory<byte> Plaintext) : UnprotectResult
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>The person's key was erased, so the value never opens again.</summary>
    public sealed record Erased() : UnprotectResult;

    /// <summary>The key the value was made under is not available now.</summary>
    public sealed record KeyUnavailable() : UnprotectResult;

    /// <summary>The value does not open under its binding.</summary>
    public sealed record Rejected() : UnprotectResult;
}