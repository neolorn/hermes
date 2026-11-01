using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Conversations.Attachments;

/// <summary>
/// Opens an attachment's protected values (<c>09</c> MSG-CONV-009): its name, and its content
/// from the store chunk by chunk, each bound to its own context and opened within
/// <c>Hermes:Dispatch:Timeout</c>.
/// </summary>
internal static class AttachmentOpening
{
    /// <summary>Opens <paramref name="stored"/>, protected for <paramref name="owner"/> under <paramref name="context"/>, within <paramref name="bound"/>; a protector that fails or overruns holds it.</summary>
    internal static async Task<ContentOpening> OpenAsync(ContentProtection protection, string? owner, byte[] stored, string context, TimeSpan bound, TimeProvider clock, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource timed = new(bound, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timed.Token, cancellationToken);
            return await protection.OpenAsync(owner, stored, context, linked.Token)
                .AsTask()
                .WaitAsync(bound, clock, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return ContentOpening.Held(ContentEnvelope.Read(stored).Id);
        }
    }

    /// <summary>
    /// The content of <paramref name="row"/>, read from the store under its key, its chunks
    /// opened as they are reached; none where the store holds no object under the key.
    /// <paramref name="held"/> is told the key of a chunk that cannot open yet.
    /// </summary>
    internal static async Task<AttachmentFiles.Content?> ContentAsync(
        ContentProtection protection,
        IAttachmentStore store,
        AttachmentRow row,
        TimeSpan bound,
        TimeProvider clock,
        Action<string?> held,
        CancellationToken cancellationToken)
    {
        if (row.StoreKey is not { } key || await store.OpenAsync(key, cancellationToken).ConfigureAwait(false) is not { } stored)
        {
            return null;
        }

        return new AttachmentFiles.Content(stored, row.Chunks, async (chunk, frame, token) =>
        {
            ContentOpening opening = await OpenAsync(protection, row.Owner, frame, AttachmentFiles.ChunkContext(row.Id, chunk), bound, clock, token).ConfigureAwait(false);
            if (opening.Status == ContentOpeningStatus.Opened)
            {
                return opening.Plaintext.ToArray();
            }

            if (opening.Status == ContentOpeningStatus.Held)
            {
                held(opening.Key);
            }

            return null;
        });
    }
}