using System.Collections.Generic;
using System.Linq;

using Hermes.Sending;

namespace Hermes.Operations;

/// <summary>Which of the five keys the application's used parts need (<c>06</c> MSG-OPS-008).</summary>
internal static class KeyNeeds
{
    internal const string ContentKeys = "contentKeys";

    internal const string HashKeys = "hashKeys";

    internal const string TokenKeys = "tokenKeys";

    internal const string SmsReportKey = "smsReportKey";

    internal const string PushVapidKeys = "pushVapidKeys";

    /// <summary>
    /// The keys needed, in the order of <c>08</c> section 7: <c>hashKeys</c> by the engine;
    /// <c>contentKeys</c> where the application records sends, dispatches or runs the jobs;
    /// <c>tokenKeys</c> where it records sends and an optional category has an email kind;
    /// <c>pushVapidKeys</c> where push is dispatched. The endpoint groups and the SMS
    /// report address need them too, with the items that build them.
    /// </summary>
    internal static IReadOnlyList<string> Needed(Declarations declarations, MessagingOptions options)
    {
        List<string> needed = [];
        if (options.Messages.Enabled || options.Dispatch.Enabled || options.Jobs.Enabled)
        {
            needed.Add(ContentKeys);
        }

        needed.Add(HashKeys);
        if (options.Messages.Enabled && declarations.NeedsPublicBaseAddress)
        {
            needed.Add(TokenKeys);
        }

        if (options.Dispatch.Enabled && declarations.Kinds.Any(kind => kind.Push is not null))
        {
            needed.Add(PushVapidKeys);
        }

        return needed;
    }

    /// <summary>The configuration key of <paramref name="key"/>'s write version.</summary>
    internal static string WriteVersionPath(string key)
    {
        return $"Secrets:{char.ToUpperInvariant(key[0])}{key[1..]}:WriteVersion";
    }
}