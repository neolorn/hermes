using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// The pipeline's release rule, read from the text of <c>.github/workflows/gates.yml</c>: the
/// release job runs only for a release tag, after the gates job, packs every package and creates
/// the tag's release, and is the one job that can write to the repository's contents
/// (<c>07</c> MSG-ENG-011).
/// </summary>
internal static class WorkflowRules
{
    internal const string Path = ".github/workflows/gates.yml";

    internal const string ReleaseJob = "release";

    /// <summary>Each way <paramref name="workflow"/> breaks the release rule.</summary>
    internal static IEnumerable<string> Violations(string workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        string[] lines = workflow.ReplaceLineEndings("\n").Split('\n');

        if (!Block(lines, "permissions:", 0).SequenceEqual(["  contents: read"], StringComparer.Ordinal))
        {
            yield return "the workflow's permissions are not contents: read alone";
        }

        if (!Block(Block(Block(lines, "on:", 0), "push:", 2), "tags:", 4).SequenceEqual(["      - 'v[0-9]+.[0-9]+.[0-9]+'"], StringComparer.Ordinal))
        {
            yield return "the workflow's tags are not the release tags alone";
        }

        Dictionary<string, string[]> jobs = Jobs(lines);
        foreach ((string name, string[] job) in jobs.Where(pair => pair.Key != ReleaseJob))
        {
            if (job.Any(line => line.Contains(": write", StringComparison.Ordinal) || line.Contains("write-all", StringComparison.Ordinal)) || Block(job, "permissions:", 4).Length > 0)
            {
                yield return $"the job {name} sets its own permissions";
            }
        }

        if (!jobs.TryGetValue(ReleaseJob, out string[]? release))
        {
            yield return "no release job releases a release tag";
            yield break;
        }

        if (!release.Contains("    needs: gates", StringComparer.Ordinal))
        {
            yield return "the release job does not wait for the gates job";
        }

        if (!release.Contains("    if: github.ref_type == 'tag'", StringComparer.Ordinal))
        {
            yield return "the release job does not run only for a release tag";
        }

        if (!Block(release, "permissions:", 4).SequenceEqual(["      contents: write"], StringComparer.Ordinal))
        {
            yield return "the release job's permissions are not contents: write alone";
        }

        // The job's token can write to the contents, so no checkout leaves it in .git/config
        // while the restore and the build run code of other parties.
        int checkouts = release.Count(line => line.TrimStart().StartsWith("uses: actions/checkout@", StringComparison.Ordinal));
        if (checkouts == 0 || release.Count(line => line == "          persist-credentials: false") != checkouts)
        {
            yield return "the release job's checkout leaves its token in the repository's configuration";
        }

        if (!release.Any(line => line.TrimStart().StartsWith("dotnet pack Hermes.sln ", StringComparison.Ordinal)))
        {
            yield return "the release job does not pack every package";
        }

        if (!release.Any(line => line.TrimStart().StartsWith("gh release create ", StringComparison.Ordinal) && line.Contains(" --notes-file RELEASE-NOTES.md ", StringComparison.Ordinal)))
        {
            yield return "the release job does not create the tag's release with RELEASE-NOTES.md as its notes";
        }
    }

    /// <summary>The jobs of <paramref name="lines"/>, each by its name, its lines below its name.</summary>
    private static Dictionary<string, string[]> Jobs(string[] lines)
    {
        Dictionary<string, string[]> jobs = new(StringComparer.Ordinal);
        string[] block = Block(lines, "jobs:", 0);
        for (int index = 0; index < block.Length; index++)
        {
            string line = block[index];
            if (line.Length > 3 && line.StartsWith("  ", StringComparison.Ordinal) && line[2] != ' ' && line[2] != '#' && line.EndsWith(':'))
            {
                jobs[line[2..^1]] = Block(block, line.Trim(), 2);
            }
        }

        return jobs;
    }

    /// <summary>
    /// The lines below the first line of <paramref name="lines"/> that is <paramref name="key"/>
    /// indented by <paramref name="indent"/>, up to the next line indented by as little, blank
    /// lines and comments aside; none where no line is the key.
    /// </summary>
    private static string[] Block(string[] lines, string key, int indent)
    {
        string start = new string(' ', indent) + key;
        int at = Array.FindIndex(lines, line => line.TrimEnd() == start);
        if (at < 0)
        {
            return [];
        }

        List<string> block = [];
        foreach (string line in lines.Skip(at + 1))
        {
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (line.Length - line.TrimStart().Length <= indent)
            {
                break;
            }

            block.Add(line.TrimEnd());
        }

        return [.. block];
    }
}