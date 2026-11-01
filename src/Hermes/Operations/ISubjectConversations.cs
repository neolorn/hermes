using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Operations;

/// <summary>
/// The conversations' part of a person's export, which <c>Hermes.Conversations</c> registers
/// (<c>01</c> MSG-SEND-020; <c>09</c> MSG-CONV-010): the export reads it in its own
/// transaction, under the person's locks, and opens it after that transaction commits.
/// </summary>
internal interface ISubjectConversations
{
    /// <summary>
    /// Reads the person's conversations in <paramref name="context"/>'s transaction, answering
    /// what opens them afterwards: their export, or the failure of a value that cannot open yet.
    /// </summary>
    Task<Func<CancellationToken, Task<Result<IReadOnlyList<ConversationExport>>>>> ReadAsync(MessagingContext context, string person, CancellationToken cancellationToken);
}