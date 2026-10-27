using System;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>One recorded call of a push delivery (<c>02</c> MSG-PUSH-005), in the order made.</summary>
/// <param name="Attempt">The attempt it was made in.</param>
/// <param name="Subscription">Its subscription's row, none once that subscription is removed.</param>
/// <param name="Class">How it was answered.</param>
/// <param name="Reason">Why, where the answer gave a reason.</param>
internal sealed record PushCall(int Attempt, Guid? Subscription, AttemptClass Class, AttemptReason? Reason);