using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class StatementRuleTests
{
    [Fact]
    public void MSG_ENG_005_AC2_NoHandWrittenStatementReadsTheDatabaseClock()
    {
        Assert.Empty(StatementRules.ClockViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("update messaging.deliveries set moved_at = now() where id = @id")]
    [InlineData("SELECT CURRENT_TIMESTAMP")]
    [InlineData("select clock_timestamp()")]
    public void MSG_ENG_005_AC2_StatementReadingTheDatabaseClockIsFound(string statement)
    {
        Assert.NotEmpty(StatementRules.ClockViolations("src/Hermes/Sending/Claim.cs", statement));
    }

    [Theory]
    [InlineData("update messaging.deliveries set moved_at = @now where id = @id")]
    [InlineData("DateTimeOffset now = timeProvider.GetUtcNow();")]
    public void MSG_ENG_005_AC2_StatementTakingTheInstantAsAParameterIsAccepted(string statement)
    {
        Assert.Empty(StatementRules.ClockViolations("src/Hermes/Sending/Claim.cs", statement));
    }

    [Fact]
    public void MSG_ENG_005_AC3_NoHandWrittenStatementTakesASessionLevelAdvisoryLock()
    {
        Assert.Empty(StatementRules.SessionLockViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("select pg_advisory_lock(@key)")]
    [InlineData("SELECT PG_ADVISORY_LOCK_SHARED(@key)")]
    public void MSG_ENG_005_AC3_SessionLevelAdvisoryLockIsFound(string statement)
    {
        Assert.NotEmpty(StatementRules.SessionLockViolations("src/Hermes/Sending/Limits.cs", statement));
    }

    [Theory]
    [InlineData("select pg_advisory_xact_lock(@key)")]
    [InlineData("select pg_advisory_xact_lock_shared(@key)")]
    public void MSG_ENG_005_AC3_TransactionScopedAdvisoryLockIsAccepted(string statement)
    {
        Assert.Empty(StatementRules.SessionLockViolations("src/Hermes/Sending/Limits.cs", statement));
    }
}