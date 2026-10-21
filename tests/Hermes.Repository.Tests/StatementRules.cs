using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Repository.Tests;

/// <summary>
/// The hand-written statements of <c>07</c> MSG-ENG-005: none reads the database's clock
/// and none takes a session-level advisory lock.
/// </summary>
internal static partial class StatementRules
{
    private static readonly string[] StatementFiles = ["*.cs", "*.sql"];

    /// <summary>
    /// Answers each hand-written statement under <c>src/</c> of the repository at
    /// <paramref name="root"/> that reads the database's clock. The migrations EF Core
    /// generates are not hand-written.
    /// </summary>
    internal static IReadOnlyList<string> ClockViolations(string root)
    {
        return [.. Statements(root).SelectMany(statement => ClockViolations(statement.Where, statement.Text))];
    }

    internal static IEnumerable<string> ClockViolations(string where, string text)
    {
        return ClockPattern().Matches(text).Select(clock => $"{where} calls {clock.Value.Trim()}; a statement takes the current instant as @now.");
    }

    /// <summary>
    /// Answers each hand-written statement under <c>src/</c> of the repository at
    /// <paramref name="root"/> that takes a session-level advisory lock.
    /// </summary>
    internal static IReadOnlyList<string> SessionLockViolations(string root)
    {
        return [.. Statements(root).SelectMany(statement => SessionLockViolations(statement.Where, statement.Text))];
    }

    internal static IEnumerable<string> SessionLockViolations(string where, string text)
    {
        return SessionLockPattern().Matches(text).Select(sessionLock => $"{where} calls {sessionLock.Value.Trim()}; every advisory lock is transaction-scoped.");
    }

    private static IEnumerable<(string Where, string Text)> Statements(string root)
    {
        string migrations = Path.Combine(root, "src", Packages.Engine, "Migrations") + Path.DirectorySeparatorChar;
        string source = Path.Combine(root, "src");
        if (!Directory.Exists(source))
        {
            return [];
        }

        return StatementFiles.SelectMany(pattern => ProjectFile.Enumerate(source, pattern))
            .Where(file => !file.EndsWith(".g.cs", StringComparison.Ordinal))
            .Where(file => !file.StartsWith(migrations, StringComparison.Ordinal))
            .Select(file => (Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file)));
    }

    [GeneratedRegex(@"\b(now\s*\(\s*\)|current_timestamp\b|clock_timestamp\s*\()", RegexOptions.IgnoreCase)]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"\bpg_advisory_lock(_shared)?\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex SessionLockPattern();
}