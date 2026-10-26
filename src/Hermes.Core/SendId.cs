using System;

namespace Hermes.Core;

/// <summary>A send's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct SendId(Guid Value);