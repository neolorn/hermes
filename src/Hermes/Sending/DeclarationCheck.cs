using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Hermes.Channels;
using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// The startup checks of the kinds and categories the host declared (<c>01</c> MSG-SEND-001,
/// MSG-SEND-002): the first declaration outside its rule, with the member it fails on.
/// </summary>
internal static partial class DeclarationCheck
{
    private const int NameLength = 64;

    private const int LabelLength = 128;

    private static readonly TimeSpan LongestTtl = TimeSpan.FromDays(28);

    /// <summary>Answers the first declaration outside its rule and the member it fails on, or none.</summary>
    /// <param name="declarations">What the host declared.</param>
    /// <param name="languages">The languages of <c>Hermes:Languages</c>.</param>
    /// <param name="places">The value of <c>Hermes:Retention:Places</c>.</param>
    internal static (string Declaration, string Field)? Invalid(Declarations declarations, IReadOnlyList<string> languages, TimeSpan places)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(languages);

        HashSet<string> categories = new(StringComparer.Ordinal);
        foreach (CategoryDeclaration category in declarations.Categories)
        {
            if (!IsName(category.Name) || !categories.Add(category.Name))
            {
                return (category.Name, "name");
            }
        }

        HashSet<string> kinds = new(StringComparer.Ordinal);
        HashSet<string> retired = [.. declarations.Retired];
        foreach (KindDeclaration kind in declarations.Kinds)
        {
            if (!IsName(kind.Name) || !kinds.Add(kind.Name) || retired.Contains(kind.Name))
            {
                return (kind.Name, "name");
            }

            string? field = InvalidMember(kind, categories);
            if (field is not null)
            {
                return (kind.Name, field);
            }
        }

        HashSet<string> retiredOnce = new(StringComparer.Ordinal);
        foreach (string name in declarations.Retired)
        {
            if (!IsName(name) || !retiredOnce.Add(name))
            {
                return (name, "name");
            }
        }

        foreach (CategoryDeclaration category in declarations.Categories)
        {
            IReadOnlyList<KindDeclaration> members = [.. declarations.Kinds.Where(kind => string.Equals(kind.Category, category.Name, StringComparison.Ordinal))];
            string? field = InvalidMember(category, members, languages, places);
            if (field is not null)
            {
                return (category.Name, field);
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="name"/> is 1 to 64 lower-case ASCII letters and digits separated by single <c>.</c>, <c>-</c> or <c>_</c>.</summary>
    internal static bool IsName(string name)
    {
        return name.Length <= NameLength && Name().IsMatch(name);
    }

    private static string? InvalidMember(KindDeclaration kind, HashSet<string> categories)
    {
        if (!categories.Contains(kind.Category))
        {
            return "category";
        }

        if (kind.Email is null && kind.Sms is null && kind.Push is null)
        {
            return "channel";
        }

        if (kind.ExpiresAfter is { } expiresAfter && expiresAfter <= TimeSpan.Zero)
        {
            return "ExpiresAfter";
        }

        if (kind.Sms is { MaxSegments: < 1 or > 10 })
        {
            return "maxSegments";
        }

        if (kind.Push is { Ttl: { } ttl } && (ttl < TimeSpan.FromSeconds(1) || ttl > LongestTtl))
        {
            return "ttl";
        }

        return null;
    }

    private static string? InvalidMember(CategoryDeclaration category, IReadOnlyList<KindDeclaration> kinds, IReadOnlyList<string> languages, TimeSpan places)
    {
        if (category.Essential && category.ConsentPurpose is not null)
        {
            return "RequiresConsent";
        }

        if (category.Essential && category.QuietHours is true)
        {
            return "QuietHours";
        }

        if (category.ConsentPurpose is { } purpose && !IsName(purpose))
        {
            return "RequiresConsent";
        }

        if (!AreLimits(category.Limits, kinds, places))
        {
            return "Limit";
        }

        if (!AreLimits(category.DeploymentLimits, kinds, places))
        {
            return "DeploymentLimit";
        }

        bool labelled = !category.Essential && kinds.Any(kind => kind.Email is not null);
        if (labelled && !languages.All(category.Labels.ContainsKey))
        {
            return "Label";
        }

        if (!category.Labels.Values.All(label => TextLength.AtMost(label, LabelLength)))
        {
            return "Label";
        }

        return null;
    }

    private static bool AreLimits(IReadOnlyList<LimitDeclaration> limits, IReadOnlyList<KindDeclaration> kinds, TimeSpan places)
    {
        HashSet<(Channel, TimeSpan)> windows = [];
        return limits.All(limit => limit.Count >= 1
            && limit.Window >= TimeSpan.FromMinutes(1)
            && limit.Window <= places
            && kinds.Any(kind => Uses(kind, limit.Channel))
            && windows.Add((limit.Channel, limit.Window)));
    }

    private static bool Uses(KindDeclaration kind, Channel channel)
    {
        return channel switch
        {
            Channel.Email => kind.Email is not null,
            Channel.Sms => kind.Sms is not null,
            Channel.Push => kind.Push is not null,
            _ => false,
        };
    }

    [GeneratedRegex(@"^[a-z0-9]+([._-][a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Name();
}