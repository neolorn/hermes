using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class TransactionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string SerializationFailure = "DO $$ BEGIN RAISE EXCEPTION 'serialization failure' USING ERRCODE = '40001'; END $$";

    [Fact]
    public async Task MSG_ENG_005_AC4_ATransactionAbortedWith40001IsRunAgainAtMostRetriesTimes()
    {
        await using MessagingDatabase database = await OpenAsync();
        MessagingOptions options = new() { Database = { Retries = 3 } };
        int runs = 0;

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => LibraryTransactions.RunAsync(
            database,
            options,
            async (context, cancellationToken) =>
            {
                runs++;
                return await context.Database.ExecuteSqlRawAsync(SerializationFailure, cancellationToken);
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.SerializationFailure, failure.SqlState);
        Assert.Equal(4, runs);
    }

    [Fact]
    public async Task RunAsync_TransactionAbortedTwiceWith40001_CommitsOnTheThirdRun()
    {
        await using MessagingDatabase database = await OpenAsync();
        int runs = 0;

        int result = await LibraryTransactions.RunAsync(
            database,
            new MessagingOptions(),
            async (context, cancellationToken) =>
            {
                if (++runs < 3)
                {
                    await context.Database.ExecuteSqlRawAsync(SerializationFailure, cancellationToken);
                }

                return runs;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task RunAsync_RetriesZero_RunsOnce()
    {
        await using MessagingDatabase database = await OpenAsync();
        int runs = 0;

        await Assert.ThrowsAsync<PostgresException>(() => LibraryTransactions.RunAsync(
            database,
            new MessagingOptions { Database = { Retries = 0 } },
            async (context, cancellationToken) =>
            {
                runs++;
                return await context.Database.ExecuteSqlRawAsync(SerializationFailure, cancellationToken);
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task RunAsync_EachTransaction_SetsItsIdleTimeoutToTwiceTheDispatchTimeout()
    {
        await using MessagingDatabase database = await OpenAsync();

        string timeout = await LibraryTransactions.RunAsync(
            database,
            new MessagingOptions(),
            (context, cancellationToken) => context.Database.SqlQueryRaw<string>("SELECT current_setting('idle_in_transaction_session_timeout') AS \"Value\"").SingleAsync(cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("40s", timeout);
    }

    private async Task<MessagingDatabase> OpenAsync()
    {
        MessagingDatabase database = new();
        database.Open(await postgres.NewDatabaseAsync());
        return database;
    }
}