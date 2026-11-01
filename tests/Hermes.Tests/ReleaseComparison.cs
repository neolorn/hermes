using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Tests;

/// <summary>
/// The release a change from one set to another needs (<c>04</c> MSG-LIB-006): a line removed
/// from any section, which is also how a change shows, is major, as is a line added where an
/// addition binds a caller or a host (a request member a caller must now send, a member a host
/// must implement); any other addition is minor.
/// </summary>
internal static class ReleaseComparison
{
    /// <summary>The sections where an addition is a break.</summary>
    private static readonly HashSet<string> AddedBreaks = new(StringComparer.Ordinal) { ReleaseSet.Required, ReleaseSet.Abstract };

    private static readonly string[] Compared =
    [
        ReleaseSet.Routes, ReleaseSet.Statuses, ReleaseSet.Bodies, ReleaseSet.Required, ReleaseSet.Codes,
        ReleaseSet.Keys, ReleaseSet.Spelled, ReleaseSet.Metrics, ReleaseSet.Abstract, ReleaseSet.Surface, ReleaseSet.Hubs,
    ];

    /// <summary>What moving from <paramref name="baseline"/> to <paramref name="current"/> needs, with each line that needs more than a patch.</summary>
    internal static ReleaseNeeds Compare(ReleaseSet baseline, ReleaseSet current)
    {
        List<string> major = [];
        List<string> minor = [];
        foreach (string section in Compared)
        {
            IReadOnlySet<string> before = baseline[section];
            IReadOnlySet<string> after = current[section];
            major.AddRange(before.Where(line => !after.Contains(line) && !AddedBreaks.Contains(section)).Select(line => $"{section}: removed or changed: {line}"));
            foreach (string line in after.Where(line => !before.Contains(line)))
            {
                (AddedBreaks.Contains(section) && Existed(baseline, section, line) ? major : minor).Add($"{section}: added: {line}");
            }
        }

        string level = major.Count > 0 ? ReleaseNeeds.Major : minor.Count > 0 ? ReleaseNeeds.Minor : ReleaseNeeds.Patch;
        return new ReleaseNeeds(level, [.. major, .. minor]);
    }

    /// <summary>
    /// Whether the interface or body <paramref name="line"/> adds a member to was in the
    /// baseline, so that the addition binds a host or caller already using it; a new
    /// interface or body binds no one.
    /// </summary>
    private static bool Existed(ReleaseSet baseline, string section, string line)
    {
        if (section == ReleaseSet.Abstract)
        {
            string type = line[..line.LastIndexOf('.', line.IndexOf('(', StringComparison.Ordinal))];
            return baseline[ReleaseSet.Abstract].Any(held => held.StartsWith(type + ".", StringComparison.Ordinal))
                || baseline[ReleaseSet.Surface].Any(held => held.EndsWith(": " + type, StringComparison.Ordinal));
        }

        string body = line[..line.IndexOf('.', StringComparison.Ordinal)];
        return baseline[ReleaseSet.Bodies].Any(held => held.StartsWith(body + ".", StringComparison.Ordinal));
    }
}