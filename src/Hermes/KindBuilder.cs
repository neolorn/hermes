using System;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes;

/// <summary>
/// Declares the channels a kind may go out on, each with its options, and how long its
/// deliveries may wait. A kind declares at least one channel; a method called again replaces
/// what the earlier call declared.
/// </summary>
public sealed class KindBuilder
{
    private readonly string name;
    private readonly string category;
    private KindEmail? email;
    private KindSms? sms;
    private KindPush? push;
    private TimeSpan? expiresAfter;

    internal KindBuilder(string name, string category)
    {
        this.name = name;
        this.category = category;
    }

    /// <summary>Lets the kind go out by email.</summary>
    /// <param name="from">The sender, an address; none for <c>Hermes:Email:From</c>.</param>
    /// <param name="replyTo">The reply address, an address; or none.</param>
    /// <returns>This builder.</returns>
    public KindBuilder Email(EmailAddress? from = null, EmailAddress? replyTo = null)
    {
        email = new(from, replyTo);
        return this;
    }

    /// <summary>Lets the kind go out by SMS.</summary>
    /// <param name="maxSegments">The most segments one of its messages may take, 1 to 10.</param>
    /// <param name="sender">The sender, within the provider's rule; none for <c>Hermes:Sms:Sender</c>.</param>
    /// <returns>This builder.</returns>
    public KindBuilder Sms(int maxSegments = 1, string? sender = null)
    {
        sms = new(maxSegments, sender);
        return this;
    }

    /// <summary>Lets the kind go out by browser push.</summary>
    /// <param name="ttl">How long the push service keeps a message it cannot deliver yet, 1 second to 28 days; none for the channel's own.</param>
    /// <param name="urgency">The messages' urgency.</param>
    /// <returns>This builder.</returns>
    public KindBuilder Push(TimeSpan? ttl = null, PushUrgency urgency = PushUrgency.Normal)
    {
        push = new(ttl, urgency);
        return this;
    }

    /// <summary>Declares that a delivery not sent within <paramref name="duration"/> of its recording is not sent at all.</summary>
    /// <param name="duration">The duration, above zero.</param>
    /// <returns>This builder.</returns>
    public KindBuilder ExpiresAfter(TimeSpan duration)
    {
        expiresAfter = duration;
        return this;
    }

    internal KindDeclaration Declaration()
    {
        return new(name, category, email, sms, push, expiresAfter);
    }
}