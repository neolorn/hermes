using Hermes.Core;

namespace Hermes.Sending;

/// <summary>A kind's email options (<c>02</c> MSG-EMAIL-002).</summary>
/// <param name="From">The sender, or none for <c>Hermes:Email:From</c>.</param>
/// <param name="ReplyTo">The reply address, or none.</param>
internal sealed record KindEmail(EmailAddress? From, EmailAddress? ReplyTo);