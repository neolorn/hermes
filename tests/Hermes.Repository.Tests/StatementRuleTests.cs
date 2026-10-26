using System;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class StatementRuleTests
{
    private const string Claim = "src/Hermes/Sending/Claim.cs";

    private const string Limits = "src/Hermes/Sending/Limits.cs";

    [Fact]
    public void MSG_ENG_005_AC2_NoHandWrittenStatementReadsTheDatabaseClock()
    {
        Assert.Empty(StatementRules.ClockViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("update messaging.deliveries set moved_at = now() where id = @id", "now()")]
    [InlineData("SELECT CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP")]
    [InlineData("select clock_timestamp()", "clock_timestamp(")]
    public void MSG_ENG_005_AC2_StatementReadingTheDatabaseClockIsFound(string statement, string call)
    {
        Assert.Equal([$"{Claim} calls {call}; a statement takes the current instant as @now."], StatementRules.ClockViolations(Claim, statement));
    }

    [Theory]
    [InlineData("update messaging.deliveries set moved_at = @now where id = @id")]
    [InlineData("DateTimeOffset now = timeProvider.GetUtcNow();")]
    public void MSG_ENG_005_AC2_StatementTakingTheInstantAsAParameterIsAccepted(string statement)
    {
        Assert.Empty(StatementRules.ClockViolations(Claim, statement));
    }

    [Fact]
    public void MSG_ENG_005_AC3_NoHandWrittenStatementTakesASessionLevelAdvisoryLock()
    {
        Assert.Empty(StatementRules.SessionLockViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("select pg_advisory_lock(@key)", "pg_advisory_lock(")]
    [InlineData("SELECT PG_ADVISORY_LOCK_SHARED(@key)", "PG_ADVISORY_LOCK_SHARED(")]
    [InlineData("select pg_try_advisory_lock(@key)", "pg_try_advisory_lock(")]
    [InlineData("select pg_try_advisory_lock_shared(@key)", "pg_try_advisory_lock_shared(")]
    public void MSG_ENG_005_AC3_SessionLevelAdvisoryLockIsFound(string statement, string call)
    {
        Assert.Equal([$"{Limits} calls {call}; every advisory lock is transaction-scoped."], StatementRules.SessionLockViolations(Limits, statement));
    }

    [Theory]
    [InlineData("select pg_advisory_xact_lock(@key)")]
    [InlineData("select pg_advisory_xact_lock_shared(@key)")]
    [InlineData("select pg_try_advisory_xact_lock(@key)")]
    public void MSG_ENG_005_AC3_TransactionScopedAdvisoryLockIsAccepted(string statement)
    {
        Assert.Empty(StatementRules.SessionLockViolations(Limits, statement));
    }

    [Fact]
    public void MSG_ENG_005_AC2_StatementInAFileUnderSrcIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(Claim, """
            namespace Hermes.Sending;

            internal static class Claim
            {
                // Never the database's now(): the instant is @now.
                internal const string Statement = "update messaging.deliveries set moved_at = now() where id = @id";
            }
            """);

        Assert.Equal([$"{Claim} calls now(); a statement takes the current instant as @now."], StatementRules.ClockViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_005_AC2_CommentQuotingTheDatabaseClockIsNoStatement()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(Claim, """
            namespace Hermes.Sending;

            /* A statement calls never now() nor current_timestamp. */
            internal static class Claim
            {
                // Never the database's now(): the instant is @now.
                internal const string Statement = "update messaging.deliveries set moved_at = @now where id = @id";
            }
            """);
        fixture.Write("src/Hermes/Sending/claim.sql", "-- never now()\n/* nor current_timestamp */\nupdate messaging.deliveries set moved_at = @now;\n");

        Assert.Empty(StatementRules.ClockViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_005_AC3_HandWrittenStatementOfAMigrationIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes/Migrations/20261026000000_Locks.cs", """
            namespace Hermes.Migrations;

            internal sealed partial class Locks
            {
                internal static string Up() => @"select pg_try_advisory_lock(1)";
            }
            """);
        fixture.Write("src/Hermes/Migrations/20261026000000_Locks.Designer.cs", "namespace Hermes.Migrations;\n\ninternal sealed partial class Locks { private const string Text = \"select pg_advisory_lock(2)\"; }\n");
        fixture.Write("src/Hermes/Migrations/MessagingContextModelSnapshot.cs", "namespace Hermes.Migrations;\n\ninternal sealed class Snapshot { private const string Text = \"select pg_advisory_lock(3)\"; }\n");

        Assert.Equal(
            ["src/Hermes/Migrations/20261026000000_Locks.cs calls pg_try_advisory_lock(; every advisory lock is transaction-scoped."],
            StatementRules.SessionLockViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_005_AC3_SqlFileUnderSrcIsRead()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes/Sending/limits.sql", "select pg_advisory_lock_shared(@key);\n");

        Assert.Equal(
            ["src/Hermes/Sending/limits.sql calls pg_advisory_lock_shared(; every advisory lock is transaction-scoped."],
            StatementRules.SessionLockViolations(fixture.Root));
    }

    [Theory]
    [InlineData("string a = \"select 1\";", "select 1")]
    [InlineData("string a = @\"say \"\"now()\"\"\";", "say \"\"now()\"\"")]
    [InlineData("string a = $\"select {b}\";", "select {b}")]
    [InlineData("string a = \"\"\"\n    select \"x\"\n    \"\"\";", "\n    select \"x\"\n    ")]
    [InlineData("char c = '\"'; string a = \"select 2\";", "select 2")]
    [InlineData("string a = \"a \\\" b\";", "a \\\" b")]
    public void CSharpSource_StringLiteral_IsReadWhole(string source, string literal)
    {
        Assert.Equal([literal], CSharpSource.Read(source).Literals);
    }

    [Theory]
    [InlineData("// public sealed class A;\nint b;")]
    [InlineData("/* \"select now()\" */ int b;")]
    public void CSharpSource_Comment_IsBlankedAndHoldsNoLiteral(string source)
    {
        CSharpSource read = CSharpSource.Read(source);

        Assert.Empty(read.Literals);
        Assert.Equal(source.Length, read.Code.Length);
        Assert.Contains("int b;", read.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("class", read.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("now", read.Code, StringComparison.Ordinal);
    }
}