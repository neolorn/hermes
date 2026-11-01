using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Conversations.Tests;

/// <summary>A membership seam whose scopes a test fills and changes, counting each question asked of it.</summary>
internal sealed class MembershipFake : IConversationMembership
{
    private readonly ConcurrentDictionary<(ScopeKind Kind, string Reference), HashSet<string>> scopes = new();

    private int asked;

    /// <summary>How many times <see cref="IsMemberAsync"/> was asked.</summary>
    internal int Asked => asked;

    /// <summary>The scope of kind <c>organization</c> with <paramref name="reference"/>.</summary>
    internal static ConversationScope Organization(string reference)
    {
        return new ConversationScope { Kind = ScopeKind.Organization, Reference = reference };
    }

    /// <summary>Makes <paramref name="persons"/> members of <paramref name="scope"/>.</summary>
    internal void Add(ConversationScope scope, params string[] persons)
    {
        HashSet<string> members = scopes.GetOrAdd((scope.Kind, scope.Reference), _ => new HashSet<string>(StringComparer.Ordinal));
        lock (members)
        {
            members.UnionWith(persons);
        }
    }

    /// <summary>Makes <paramref name="person"/> no longer a member of <paramref name="scope"/>.</summary>
    internal void Remove(ConversationScope scope, string person)
    {
        if (scopes.TryGetValue((scope.Kind, scope.Reference), out HashSet<string>? members))
        {
            lock (members)
            {
                members.Remove(person);
            }
        }
    }

    public ValueTask<bool> IsMemberAsync(string person, ConversationScope scope, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref asked);
        if (!scopes.TryGetValue((scope.Kind, scope.Reference), out HashSet<string>? members))
        {
            return ValueTask.FromResult(false);
        }

        lock (members)
        {
            return ValueTask.FromResult(members.Contains(person));
        }
    }

    public async IAsyncEnumerable<string> MembersAsync(ConversationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string[] members = [];
        if (scopes.TryGetValue((scope.Kind, scope.Reference), out HashSet<string>? held))
        {
            lock (held)
            {
                members = [.. held];
            }
        }

        foreach (string member in members)
        {
            await Task.Yield();
            yield return member;
        }
    }

    public async IAsyncEnumerable<ConversationScope> ScopesAsync(string person, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<ConversationScope> found = [];
        foreach (KeyValuePair<(ScopeKind Kind, string Reference), HashSet<string>> scope in scopes)
        {
            lock (scope.Value)
            {
                if (scope.Value.Contains(person))
                {
                    found.Add(new ConversationScope { Kind = scope.Key.Kind, Reference = scope.Key.Reference });
                }
            }
        }

        foreach (ConversationScope scope in found.OrderBy(each => each.Reference, StringComparer.Ordinal))
        {
            await Task.Yield();
            yield return scope;
        }
    }
}