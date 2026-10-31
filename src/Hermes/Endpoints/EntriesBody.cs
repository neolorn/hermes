using System.Collections.Generic;

namespace Hermes.Endpoints;

/// <summary>The answer of <c>GET /inapp</c> (<c>05</c> MSG-API-008).</summary>
/// <param name="Entries">The page's entries, newest first.</param>
/// <param name="Next">The cursor of the next page, or none on the last.</param>
internal sealed record EntriesBody(IReadOnlyList<EntryBody> Entries, string? Next);