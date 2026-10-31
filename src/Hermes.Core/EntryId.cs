using System;

namespace Hermes.Core;

/// <summary>An in-app entry's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct EntryId(Guid Value);