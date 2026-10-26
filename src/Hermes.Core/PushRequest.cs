using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// One push request to one subscription (<c>02</c> MSG-PUSH-005, MSG-PUSH-008); each platform
/// is a case, so a later platform adds its own members and changes none.
/// </summary>
public abstract record PushRequest
{
    private PushRequest()
    {
    }

    /// <summary>The platform the request goes to, fixed by its case.</summary>
    public abstract PushPlatform Platform { get; }

    /// <summary>A Web Push request; it never prints its endpoint, keys, payload or topic.</summary>
    /// <param name="Endpoint">The subscription's endpoint.</param>
    /// <param name="P256dh">The subscription's P-256 public key, base64url.</param>
    /// <param name="Auth">The subscription's authentication secret, base64url.</param>
    /// <param name="KeyVersion">The <c>pushVapidKeys</c> version the subscription was made with, which signs the request (<c>02</c> MSG-PUSH-007).</param>
    /// <param name="Payload">The notification, UTF-8 JSON, before encryption (<c>02</c> MSG-PUSH-002).</param>
    /// <param name="Ttl">How long the push service keeps the message, the request's <c>TTL</c>.</param>
    /// <param name="Urgency">The message's urgency, the request's <c>Urgency</c>.</param>
    /// <param name="Topic">The send's topic, the request's <c>Topic</c>, or none.</param>
    public sealed record WebPush(string Endpoint, string P256dh, string Auth, int KeyVersion, ReadOnlyMemory<byte> Payload, TimeSpan Ttl, PushUrgency Urgency, string? Topic) : PushRequest
    {
        /// <inheritdoc/>
        public override PushPlatform Platform => PushPlatform.WebPush;

        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Append(CultureInfo.InvariantCulture, $"Platform = {Platform}, KeyVersion = {KeyVersion}, Ttl = {Ttl}, Urgency = {Urgency}");
            return true;
        }
    }
}