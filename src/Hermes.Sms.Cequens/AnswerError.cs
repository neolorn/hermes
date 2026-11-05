using System.Collections.Generic;

namespace Hermes.Sms.Cequens;

/// <summary>The <c>error</c> of the gateway's answer.</summary>
/// <param name="InternalErrors">The gateway's reasons, each with its numeric code.</param>
internal sealed record AnswerError(IReadOnlyList<InternalError?>? InternalErrors);