using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// Answers who belongs to the host's organizations and groups, a seam required where
/// conversations are registered (<c>09</c> MSG-CONV-006; <c>04</c> MSG-LIB-005). The library
/// caches none of its answers: every read and write of a group conversation asks
/// <see cref="IsMemberAsync"/>.
/// </summary>
public interface IConversationMembership
{
    /// <summary>Whether <paramref name="person"/> is a member of <paramref name="scope"/>.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the person is a member.</returns>
    ValueTask<bool> IsMemberAsync(string person, ConversationScope scope, CancellationToken cancellationToken);

    /// <summary>The members of <paramref name="scope"/>.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each member's person reference.</returns>
    IAsyncEnumerable<string> MembersAsync(ConversationScope scope, CancellationToken cancellationToken);

    /// <summary>The scopes <paramref name="person"/> is a member of.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each scope.</returns>
    IAsyncEnumerable<ConversationScope> ScopesAsync(string person, CancellationToken cancellationToken);
}