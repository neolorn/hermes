using System;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>A kind's push options (<c>02</c> MSG-PUSH-002).</summary>
/// <param name="Ttl">How long the push service keeps a message it cannot deliver yet, or none for the channel's own.</param>
/// <param name="Urgency">The messages' urgency.</param>
internal sealed record KindPush(TimeSpan? Ttl, PushUrgency Urgency);