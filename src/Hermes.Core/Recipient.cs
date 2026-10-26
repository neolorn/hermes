using System.Text;

namespace Hermes.Core;

/// <summary>
/// Who a send is for. The set of cases is open: a later release may add one, and a seam
/// meeting a case it does not know answers as for a refusal.
/// </summary>
public abstract record Recipient
{
    private Recipient()
    {
    }

    /// <summary>A person the host identifies; it never prints the reference.</summary>
    /// <param name="Reference">The host's reference for the person.</param>
    public sealed record Person(string Reference) : Recipient
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>Someone the host has no reference for.</summary>
    public sealed record Anonymous() : Recipient;
}