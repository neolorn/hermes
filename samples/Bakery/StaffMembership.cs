using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Bakery;

/// <summary>
/// The membership seam of conversations: the bakery's staff are the members of one
/// organization, the shop, whose group conversation they share.
/// </summary>
internal sealed class StaffMembership(Customers customers) : IConversationMembership
{
    /// <summary>The shop, the one scope the staff belong to.</summary>
    internal static ConversationScope Shop { get; } = new() { Kind = ScopeKind.Organization, Reference = "shop" };

    public ValueTask<bool> IsMemberAsync(string person, ConversationScope scope, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(scope == Shop && customers.Find(person) is { Staff: true });
    }

    public async IAsyncEnumerable<string> MembersAsync(ConversationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (scope != Shop)
        {
            yield break;
        }

        foreach (Customer member in customers.Staff())
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return member.Id;
        }
    }

    public async IAsyncEnumerable<ConversationScope> ScopesAsync(string person, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (customers.Find(person) is { Staff: true })
        {
            yield return Shop;
        }
    }
}