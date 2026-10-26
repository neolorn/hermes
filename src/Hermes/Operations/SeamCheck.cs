using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Operations;

/// <summary>Checks the seams a host supplies when it starts (<c>04</c> MSG-LIB-005).</summary>
internal static class SeamCheck
{
    /// <summary>The library protector's <c>Id</c>, which no host protector takes (<c>08</c> section 8).</summary>
    internal const string LibraryProtectorId = "messaging";

    private static readonly SearchValues<char> IdCharacters = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789");

    /// <summary>
    /// The <c>details.key</c> of the first required seam not registered, in the order of
    /// MSG-LIB-005's table, or none.
    /// </summary>
    internal static string? Missing(IServiceProviderIsService services, Declarations declarations, MessagingOptions options)
    {
        if (!services.IsService(typeof(ISecretSource)))
        {
            return "secretSource";
        }

        if (declarations.Categories.Any(category => category.ConsentPurpose is not null) && !services.IsService(typeof(IConsentSource)))
        {
            return "consentSource";
        }

        if (options.Observer.Enabled && options.Dispatch.Enabled && !services.IsService(typeof(IDeliveryObserver)))
        {
            return "deliveryObserver";
        }

        return null;
    }

    /// <summary>
    /// The <c>Id</c> of the first host protector, in the order registered, whose <c>Id</c> is
    /// outside the rule or taken by one before it, or none.
    /// </summary>
    internal static string? InvalidProtector(IEnumerable<IContentProtector> protectors)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IContentProtector protector in protectors)
        {
            string id = protector.Id;
            if (!IsProtectorId(id) || !seen.Add(id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="id"/> is 1 to 32 lower-case ASCII letters and digits, and not the library's own.</summary>
    internal static bool IsProtectorId(string? id)
    {
        return id is { Length: >= 1 and <= 32 }
            && !id.AsSpan().ContainsAnyExcept(IdCharacters)
            && !string.Equals(id, LibraryProtectorId, StringComparison.Ordinal);
    }
}