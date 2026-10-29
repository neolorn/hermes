using System;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>
/// Which captured messages a <see cref="DevelopmentInbox"/> reads (<c>02</c> MSG-DEV-006);
/// a member left none matches every message.
/// </summary>
public sealed record InboxFilter
{
    /// <summary>The recipient: an email address, matched in any case, or a phone number, matched exactly.</summary>
    public string? To { get; init; }

    /// <summary>The person the engine's delivery named.</summary>
    public string? Person { get; init; }

    /// <summary>The message's channel.</summary>
    public Channel? Channel { get; init; }

    /// <summary>The name of the application that sent the message.</summary>
    public string? App { get; init; }

    /// <summary>The kind of the engine's delivery.</summary>
    public string? Kind { get; init; }

    /// <summary>Messages captured after this instant alone.</summary>
    public DateTimeOffset? After { get; init; }
}