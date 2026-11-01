using System;
using System.Collections.Generic;

namespace Hermes.Sending;

/// <summary>A row of a keyed table as the retention job reads it to protect its values again: its id, its person and each value in the table's order.</summary>
/// <param name="Id">The row's id.</param>
/// <param name="Person">The person its values are for, or none.</param>
/// <param name="Values">Each value, none where the row holds none.</param>
internal sealed record KeyedRow(Guid Id, string? Person, IReadOnlyList<byte[]?> Values);