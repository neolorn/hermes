using System.Text.Json;

namespace Hermes.Endpoints;

/// <summary>An entry of <c>GET /inapp</c> (<c>05</c> MSG-API-008), its instants in ISO 8601 UTC with milliseconds.</summary>
/// <param name="Id">The entry's identifier, lower-case and hyphenated.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Category">The kind's category.</param>
/// <param name="WrittenAt">The instant it was written.</param>
/// <param name="ReadAt">The instant it was marked read, or none.</param>
/// <param name="Language">The declared language it reads in.</param>
/// <param name="Title">The title, plain text.</param>
/// <param name="Body">The body, plain text, or none.</param>
/// <param name="Target">The key of where it leads, or none.</param>
/// <param name="TargetValues">The target values by name, each a string, a number, a boolean or an instant.</param>
internal sealed record EntryBody(
    string Id,
    string Kind,
    string Category,
    string WrittenAt,
    string? ReadAt,
    string Language,
    string Title,
    string? Body,
    string? Target,
    JsonElement TargetValues);