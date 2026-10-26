using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>
/// Protects and opens the values the library stores (<c>01</c> MSG-SEND-018; <c>04</c>
/// MSG-LIB-005): a person's values are written by the last host protector registered, or by
/// the library's own where there is none; an anonymous recipient's always by the library's;
/// a value is opened by the protector its envelope names, the library's always among them.
/// </summary>
internal sealed class ContentProtection(IEnumerable<IContentProtector> registered, LibraryProtector library)
{
    private readonly IReadOnlyList<IContentProtector> hosts = [.. registered];

    /// <summary>Protects <paramref name="plaintext"/> for <paramref name="person"/>, none for an anonymous recipient, bound to <paramref name="context"/>.</summary>
    internal async ValueTask<ContentProtecting> ProtectAsync(string? person, ReadOnlyMemory<byte> plaintext, string context, CancellationToken cancellationToken)
    {
        if (person is null || hosts.Count == 0)
        {
            ContentProtecting made = await library.ProtectAsync(person ?? string.Empty, plaintext, context, cancellationToken).ConfigureAwait(false);
            return made.Stored is null ? made : made with { Stored = ContentEnvelope.Wrap(LibraryProtector.LibraryId, made.Stored) };
        }

        IContentProtector writer = hosts[^1];
        byte[] output = await writer.ProtectAsync(person, plaintext, context, cancellationToken).ConfigureAwait(false);
        return new ContentProtecting(ContentEnvelope.Wrap(writer.Id, output), null, null);
    }

    /// <summary>Opens <paramref name="stored"/>, protected for <paramref name="person"/> (none for an anonymous recipient) under <paramref name="context"/>.</summary>
    internal async ValueTask<ContentOpening> OpenAsync(string? person, ReadOnlyMemory<byte> stored, string context, CancellationToken cancellationToken)
    {
        ContentEnvelope.Envelope envelope = ContentEnvelope.Read(stored);
        if (envelope.Form == ContentEnvelope.EnvelopeForm.Later)
        {
            return ContentOpening.Held(null);
        }

        if (envelope.Form == ContentEnvelope.EnvelopeForm.None)
        {
            return ContentOpening.Rejected;
        }

        if (string.Equals(envelope.Id, LibraryProtector.LibraryId, StringComparison.Ordinal))
        {
            return Opening(await library.UnprotectAsync(person ?? string.Empty, envelope.Output, context).ConfigureAwait(false), KeyNeeds.ContentKeys);
        }

        if (hosts.LastOrDefault(host => string.Equals(host.Id, envelope.Id, StringComparison.Ordinal)) is not { } opener)
        {
            return ContentOpening.Held(envelope.Id);
        }

        if (person is null)
        {
            return ContentOpening.Rejected;
        }

        return Opening(await opener.UnprotectAsync(person, envelope.Output, context, cancellationToken).ConfigureAwait(false), opener.Id);
    }

    private static ContentOpening Opening(UnprotectResult result, string key)
    {
        return result switch
        {
            UnprotectResult.Opened opened => ContentOpening.Opened(opened.Plaintext),
            UnprotectResult.Erased => ContentOpening.Erased,
            UnprotectResult.KeyUnavailable => ContentOpening.Held(key),
            _ => ContentOpening.Rejected,
        };
    }
}