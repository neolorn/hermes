using System;

namespace Hermes.Core;

/// <summary>A conversation's identifier, a UUID of version 7 (<c>09</c> MSG-CONV-001).</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct ConversationId(Guid Value);