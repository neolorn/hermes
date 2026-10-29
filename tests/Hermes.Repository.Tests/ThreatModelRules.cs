using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Repository.Tests;

/// <summary>
/// The controls of <c>10</c> MSG-THR-001, read from the threat model's table, and the tests
/// that carry each one's requirement (<c>10</c> MSG-THR-001).
/// </summary>
internal static partial class ThreatModelRules
{
    /// <summary>
    /// Requirements the control column names that the library does not carry: the
    /// deployment's (<c>06</c> MSG-OPS-007) and the host's own controls (MSG-THR-002).
    /// </summary>
    internal static IReadOnlySet<string> NotTheLibrarys { get; } = new HashSet<string>(StringComparer.Ordinal) { "MSG-OPS-007", "MSG-THR-002" };

    /// <summary>The requirements named in the control column of MSG-THR-001's table, in order.</summary>
    internal static IReadOnlyList<string> Controls(string threatModel)
    {
        SortedSet<string> controls = new(StringComparer.Ordinal);
        bool inSection = false;
        foreach (string line in File.ReadLines(threatModel))
        {
            if (line.StartsWith("**MSG-THR-001**", StringComparison.Ordinal))
            {
                inSection = true;
                continue;
            }

            if (inSection && line.StartsWith("*Source", StringComparison.Ordinal))
            {
                break;
            }

            string[] cells = line.Split('|');
            if (inSection && line.StartsWith("| ", StringComparison.Ordinal) && cells.Length >= 4)
            {
                foreach (Match requirement in RequirementPattern().Matches(cells[2]))
                {
                    controls.Add(requirement.Value);
                }
            }
        }

        return [.. controls];
    }

    /// <summary>The requirements a test method in <paramref name="tests"/> carries, by its <c>MSG_AREA_NNN_ACn</c> name.</summary>
    internal static IReadOnlySet<string> Tested(string tests)
    {
        HashSet<string> tested = new(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(tests, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match method in TestPattern().Matches(File.ReadAllText(file)))
            {
                tested.Add($"MSG-{method.Groups["area"].Value}-{method.Groups["number"].Value}");
            }
        }

        return tested;
    }

    /// <summary>Each control the library carries that no test carries.</summary>
    internal static IEnumerable<string> Untested(IReadOnlyList<string> controls, IReadOnlySet<string> tested)
    {
        return controls.Where(control => !NotTheLibrarys.Contains(control) && !tested.Contains(control))
            .Select(control => $"{control} names a control of 10 MSG-THR-001, and no test carries it.");
    }

    [GeneratedRegex(@"MSG-[A-Z]+-[0-9]{3}")]
    private static partial Regex RequirementPattern();

    [GeneratedRegex(@"\bMSG_(?<area>[A-Z]+)_(?<number>[0-9]{3})_AC[0-9]+_\w*\(")]
    private static partial Regex TestPattern();
}