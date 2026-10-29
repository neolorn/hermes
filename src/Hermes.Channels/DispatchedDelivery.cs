using System;
using System.Threading;

namespace Hermes.Channels;

/// <summary>
/// The delivery the engine is handing to a provider, set around each provider call so a
/// provider that keeps the message, the development inbox's, records the delivery's kind,
/// category, language and person (<c>02</c> MSG-DEV-003); none where a message is handed to
/// the providers directly.
/// </summary>
/// <param name="Kind">The delivery's kind.</param>
/// <param name="Category">The kind's category.</param>
/// <param name="Language">The delivery's language.</param>
/// <param name="Person">The person the delivery notice names, none for an anonymous recipient.</param>
internal sealed record DispatchedDelivery(string Kind, string Category, string Language, string? Person)
{
    private static readonly AsyncLocal<DispatchedDelivery?> InCall = new();

    /// <summary>The delivery of the provider call in progress, none outside the engine's calls.</summary>
    internal static DispatchedDelivery? Current => InCall.Value;

    /// <summary>Makes <paramref name="delivery"/> the current one until the answer is disposed.</summary>
    internal static Scope Enter(DispatchedDelivery delivery)
    {
        Scope scope = new(InCall.Value);
        InCall.Value = delivery;
        return scope;
    }

    /// <summary>Puts back the delivery that was current before <see cref="Enter"/>.</summary>
    internal readonly struct Scope(DispatchedDelivery? previous) : IDisposable
    {
        public void Dispose()
        {
            InCall.Value = previous;
        }
    }
}