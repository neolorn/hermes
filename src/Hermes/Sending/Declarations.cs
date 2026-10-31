using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>What the host declared in <c>AddHermes</c>, in the order it declared it (<c>01</c> MSG-SEND-001, MSG-SEND-002).</summary>
/// <param name="Kinds">The kinds.</param>
/// <param name="Categories">The categories.</param>
/// <param name="Retired">The names of the kinds retired.</param>
/// <param name="Assemblies">The assemblies whose embedded resources hold templates (<c>03</c> MSG-TPL-006).</param>
internal sealed record Declarations(IReadOnlyList<KindDeclaration> Kinds, IReadOnlyList<CategoryDeclaration> Categories, IReadOnlyList<string> Retired, IReadOnlyList<Assembly> Assemblies)
{
    /// <summary>
    /// Whether an optional category has a kind with email, which needs
    /// <c>Hermes:PublicBaseAddress</c> (<c>08</c> section 4).
    /// </summary>
    /// <summary>The channels the kinds of <paramref name="category"/> use, in the order of <c>08</c> section 2.</summary>
    internal IReadOnlyList<Channel> ChannelsOf(string category)
    {
        List<KindDeclaration> kinds = [.. Kinds.Where(kind => string.Equals(kind.Category, category, StringComparison.Ordinal))];
        List<Channel> channels = [];
        if (kinds.Any(kind => kind.Email is not null))
        {
            channels.Add(Channel.Email);
        }

        if (kinds.Any(kind => kind.Sms is not null))
        {
            channels.Add(Channel.Sms);
        }

        if (kinds.Any(kind => kind.Push is not null))
        {
            channels.Add(Channel.Push);
        }

        if (kinds.Any(kind => kind.InApp is not null))
        {
            channels.Add(Channel.InApp);
        }

        return channels;
    }

    internal bool NeedsPublicBaseAddress => Kinds.Any(kind => kind.Email is not null
        && Categories.Any(category => string.Equals(category.Name, kind.Category, StringComparison.Ordinal) && !category.Essential));
}