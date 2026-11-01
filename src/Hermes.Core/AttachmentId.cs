using System;

namespace Hermes.Core;

/// <summary>An attachment's identifier, a UUID of version 7 (<c>09</c> MSG-CONV-007).</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct AttachmentId(Guid Value);