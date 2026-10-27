using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Hermes.Tests;

/// <summary>The release a change needs, and the lines that need it.</summary>
/// <param name="Level"><c>major</c>, <c>minor</c> or <c>patch</c>.</param>
/// <param name="Lines">Each line that needs more than a patch.</param>
internal sealed record ReleaseNeeds(string Level, IReadOnlyList<string> Lines)
{
    internal const string Major = "major";

    internal const string Minor = "minor";

    internal const string Patch = "patch";

    /// <summary>The file the gate reads.</summary>
    internal string ToJson()
    {
        JsonObject root = new()
        {
            ["needs"] = Level,
            ["lines"] = new JsonArray([.. Lines.Select(line => (JsonNode?)JsonValue.Create(line))]),
        };
        return root.ToJsonString() + "\n";
    }
}