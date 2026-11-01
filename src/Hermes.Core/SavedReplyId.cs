using System;

namespace Hermes.Core;

/// <summary>A saved reply's identifier, a UUID of version 7 (<c>09</c> MSG-CONV-005).</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct SavedReplyId(Guid Value);