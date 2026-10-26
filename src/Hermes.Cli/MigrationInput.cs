using System;
using System.Collections.Generic;
using System.Text.Json;

using Hermes.Operations;

namespace Hermes.Cli;

/// <summary>
/// The document <c>hermes migrate</c> reads, <c>{ "connection", "applicationRoles" }</c>, read
/// as the library reads every JSON input (<c>05</c> MSG-API-001): members case-sensitive,
/// an unknown one ignored, and <c>details.member</c> naming the member where one is at fault.
/// </summary>
/// <param name="Connection">The connection string.</param>
/// <param name="ApplicationRoles">The roles granted the use of the schema.</param>
internal sealed record MigrationInput(string Connection, IReadOnlyList<string> ApplicationRoles)
{
    private static readonly JsonDocumentOptions Options = new() { AllowDuplicateProperties = false };

    /// <summary>Reads <paramref name="text"/>; answers none and the input, or the failure where it does not read.</summary>
    internal static MigrationFailure? Read(string text, out MigrationInput? input)
    {
        input = null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException)
        {
            return Invalid(null);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(null);
            }

            if (!root.TryGetProperty("connection", out JsonElement connection) || connection.ValueKind != JsonValueKind.String || connection.GetString() is not { Length: > 0 } read)
            {
                return Invalid("connection");
            }

            List<string> roles = [];
            if (root.TryGetProperty("applicationRoles", out JsonElement applicationRoles))
            {
                if (applicationRoles.ValueKind != JsonValueKind.Array)
                {
                    return Invalid("applicationRoles");
                }

                foreach (JsonElement role in applicationRoles.EnumerateArray())
                {
                    if (role.ValueKind != JsonValueKind.String || role.GetString() is not { Length: > 0 } name)
                    {
                        return Invalid("applicationRoles");
                    }

                    roles.Add(name);
                }
            }

            input = new MigrationInput(read, roles);
            return null;
        }
    }

    private static MigrationFailure Invalid(string? member)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (member is not null)
        {
            details["member"] = member;
        }

        return new MigrationFailure(SchemaMigration.InputInvalid, details);
    }
}