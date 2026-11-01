using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>
/// The conversations' tables as the engine knows them, wherever it is registered (<c>06</c>
/// MSG-OPS-002; <c>09</c> MSG-CONV-009): a message's text, protected under its author's key,
/// is protected again by the retention job, and no <c>contentKeys</c> version a message uses is
/// retired.
/// </summary>
[Trait("Category", "integration")]
public sealed class ConversationTableTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Author = "person-chat-5d21";

    [Fact]
    public async Task MSG_CONV_009_AC1_AMessageIsProtectedAgainUnderTheWriteVersionAndItsOldVersionRetiredOnlyThen()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid message = Guid.CreateVersion7();
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            await InsertAsync(one, message, "a staff chat's words");
        }

        await using EngineHost two = await StartRotatedAsync(connection);
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.ProtectAgainAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));

        Assert.Equal(2, await ScalarAsync<int>(two, database => With(new NpgsqlCommand("SELECT content_key_version FROM messaging.messages WHERE id = @id", database), message)));
        Assert.Equal("a staff chat's words", await OpenAsync(two, message));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task MSG_CONV_009_AC1_AKeyVersionAMessageUsesIsNeverRetired()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid message = Guid.CreateVersion7();
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            await InsertAsync(one, message, "a staff chat's words");
        }

        await using EngineHost two = await StartRotatedAsync(connection);

        // Copied to another row, the text no longer opens, so the job leaves the row under version 1.
        Guid copy = Guid.CreateVersion7();
        await two.RunAsync(database =>
        {
            NpgsqlCommand command = With(new NpgsqlCommand("UPDATE messaging.messages SET id = @copy WHERE id = @id", database), message);
            command.Parameters.AddWithValue("copy", copy);
            return command;
        });
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.ProtectAgainAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.Equal(1, await ScalarAsync<int>(two, database => With(new NpgsqlCommand("SELECT content_key_version FROM messaging.messages WHERE id = @id", database), copy)));
        Assert.DoesNotContain("contentKeys:1", await RetiredAsync(two));

        await two.RunAsync(database => new NpgsqlCommand("DELETE FROM messaging.messages", database));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task MSG_CONV_009_AC1_ACustomersSupportTextAndOfflineAddressAreProtectedAgainForTheCustomer()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid conversation = Guid.CreateVersion7();
        Guid message = Guid.CreateVersion7();
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            await InsertSupportAsync(one, conversation, message);
        }

        await using EngineHost two = await StartRotatedAsync(connection);
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.ProtectAgainAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));

        Assert.Equal(2, await ScalarAsync<int>(two, database => With(new NpgsqlCommand("SELECT email_key_version FROM messaging.conversations WHERE id = @id", database), conversation)));
        Assert.Equal(2, await ScalarAsync<int>(two, database => With(new NpgsqlCommand("SELECT content_key_version FROM messaging.messages WHERE id = @id", database), message)));
        Assert.Equal("customer@example.com", await OpenForAsync(two, Customer, database => With(new NpgsqlCommand("SELECT email FROM messaging.conversations WHERE id = @id", database), conversation), ContentContext.Of(ContentContext.Conversations, conversation, "email")));
        Assert.Equal("a staff reply", await OpenForAsync(two, Customer, database => With(new NpgsqlCommand("SELECT text FROM messaging.messages WHERE id = @id", database), message), ContentContext.Of(ContentContext.Messages, message, "text")));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task Erase_AStaffMemberOfASupportConversation_EmptiesTheirReferencesAndKeepsTheCustomersText()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid conversation = Guid.CreateVersion7();
        Guid message = Guid.CreateVersion7();
        await using EngineHost host = await EngineHost.StartAsync(connection);
        await InsertSupportAsync(host, conversation, message);

        Assert.Null((await host.WithSubjectsAsync(subjects => subjects.EraseAsync(Staff, TestContext.Current.CancellationToken).AsTask())).Failure);

        Assert.Equal(0L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.conversations WHERE id = @id AND (assignee IS NOT NULL OR closed_by IS NOT NULL)", database), conversation)));
        Assert.Equal(0L, await ScalarAsync<long>(host, database => new NpgsqlCommand("SELECT count(*) FROM messaging.support_agents", database)));
        Assert.Equal(1L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE id = @id AND author IS NULL AND owner IS NOT NULL AND text IS NOT NULL", database), message)));
    }

    [Fact]
    public async Task Erase_ACustomer_DeletesTheirSupportConversationsWithEveryRowOfThem()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid conversation = Guid.CreateVersion7();
        Guid message = Guid.CreateVersion7();
        await using EngineHost host = await EngineHost.StartAsync(connection);
        await InsertSupportAsync(host, conversation, message);

        Assert.Null((await host.WithSubjectsAsync(subjects => subjects.EraseAsync(Customer, TestContext.Current.CancellationToken).AsTask())).Failure);

        Assert.Equal(0L, await ScalarAsync<long>(host, database => new NpgsqlCommand("SELECT (SELECT count(*) FROM messaging.conversations) + (SELECT count(*) FROM messaging.messages) + (SELECT count(*) FROM messaging.participants)", database)));
        Assert.Equal(1L, await ScalarAsync<long>(host, database => new NpgsqlCommand("SELECT count(*) FROM messaging.support_agents", database)));
    }

    private const string Customer = "person-customer-81aa";

    private const string Staff = "person-staff-0c3e";

    /// <summary>Writes a customer's offline support conversation, assigned to and closed by a staff member, with a staff reply under the customer's key.</summary>
    private static async Task InsertSupportAsync(EngineHost host, Guid conversation, Guid message)
    {
        host.Uses(Customer, Staff, "customer@example.com", "a staff reply");
        byte[] email = await ProtectAsync(host, Customer, "customer@example.com", ContentContext.Of(ContentContext.Conversations, conversation, "email"));
        byte[] text = await ProtectAsync(host, Customer, "a staff reply", ContentContext.Of(ContentContext.Messages, message, "text"));
        await host.RunAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                INSERT INTO messaging.conversations (id, type, origin, status, created_at, topic, assignee, queued_at, assigned_at, offline, closed_at, closed_by, customer, email, email_key_version)
                VALUES (@conversation, 'support', 'web', 'closed', now(), 'billing', @staff, now(), now(), true, now(), @staff, @customer, @email, @email_version);
                INSERT INTO messaging.participants (id, conversation_id, person, muted, joined_at) VALUES (gen_random_uuid(), @conversation, @customer, false, now());
                INSERT INTO messaging.participants (id, conversation_id, person, muted, joined_at) VALUES (gen_random_uuid(), @conversation, @staff, false, now());
                INSERT INTO messaging.messages (id, conversation_id, author_kind, author, owner, type, text, content_key_version, sent_at)
                VALUES (@id, @conversation, 'person', @staff, @customer, 'text', @text, @text_version, now());
                INSERT INTO messaging.support_agents (id, person, available, changed_at) VALUES (gen_random_uuid(), @staff, true, now());
                """,
                database);
            command.Parameters.AddWithValue("id", message);
            command.Parameters.AddWithValue("conversation", conversation);
            command.Parameters.AddWithValue("customer", Customer);
            command.Parameters.AddWithValue("staff", Staff);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("email_version", Messages.LibraryVersion(email)!.Value);
            command.Parameters.AddWithValue("text", text);
            command.Parameters.AddWithValue("text_version", Messages.LibraryVersion(text)!.Value);
            return command;
        });
    }

    private static async Task<byte[]> ProtectAsync(EngineHost host, string person, string value, string context)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentProtecting made = await scope.ServiceProvider.GetRequiredService<ContentProtection>()
                .ProtectAsync(person, Encoding.UTF8.GetBytes(value), context, TestContext.Current.CancellationToken);
            return made.Stored!;
        }
    }

    private static async Task<string> OpenForAsync(EngineHost host, string person, Func<NpgsqlConnection, NpgsqlCommand> select, string context)
    {
        byte[] stored = await ScalarAsync<byte[]>(host, select);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentOpening opening = await scope.ServiceProvider.GetRequiredService<ContentProtection>().OpenAsync(person, stored, context, TestContext.Current.CancellationToken);
            Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
            return Encoding.UTF8.GetString(opening.Plaintext.Span);
        }
    }

    private static Task<bool> Renewed()
    {
        return Task.FromResult(true);
    }

    /// <summary>A process eight days on, holding versions 1 and 2 of the keys and writing under the second.</summary>
    private static Task<EngineHost> StartRotatedAsync(string connection)
    {
        return EngineHost.StartAsync(
            connection,
            configure: configuration =>
            {
                configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = "2";
                configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
                configuration["Hermes:Secrets:TokenKeys:WriteVersion"] = "2";
            },
            secrets: Secrets.Held(connection)
                .With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4))
                .With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 5))
                .With("tokenKeys", Secrets.Bytes(32, 3), Secrets.Bytes(32, 6)),
            clock: new ManualClock(EngineHost.Start.AddDays(8)));
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }

    /// <summary>Writes a direct conversation and a message of it whose text is protected for its author, as the conversations package does.</summary>
    private static async Task InsertAsync(EngineHost host, Guid message, string text)
    {
        host.Uses(Author, text);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ContentProtecting made;
        await using (scope)
        {
            made = await scope.ServiceProvider.GetRequiredService<ContentProtection>()
                .ProtectAsync(Author, Encoding.UTF8.GetBytes(text), ContentContext.Of(ContentContext.Messages, message, "text"), TestContext.Current.CancellationToken);
        }

        Guid conversation = Guid.CreateVersion7();
        await host.RunAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                INSERT INTO messaging.conversations (id, type, origin, status, created_at) VALUES (@conversation, 'direct', 'web', 'open', now());
                INSERT INTO messaging.messages (id, conversation_id, author_kind, author, owner, type, text, content_key_version, sent_at)
                VALUES (@id, @conversation, 'person', @author, @author, 'text', @text, @version, now());
                """,
                database);
            command.Parameters.AddWithValue("id", message);
            command.Parameters.AddWithValue("conversation", conversation);
            command.Parameters.AddWithValue("author", Author);
            command.Parameters.AddWithValue("text", made.Stored!);
            command.Parameters.AddWithValue("version", Messages.LibraryVersion(made.Stored!)!.Value);
            return command;
        });
    }

    private static async Task<string> OpenAsync(EngineHost host, Guid message)
    {
        byte[] stored = await ScalarAsync<byte[]>(host, database => With(new NpgsqlCommand("SELECT text FROM messaging.messages WHERE id = @id", database), message));
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentOpening opening = await scope.ServiceProvider.GetRequiredService<ContentProtection>()
                .OpenAsync(Author, stored, ContentContext.Of(ContentContext.Messages, message, "text"), TestContext.Current.CancellationToken);
            Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
            return Encoding.UTF8.GetString(opening.Plaintext.Span);
        }
    }

    private static async Task<T> ScalarAsync<T>(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> make)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = make(database);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<List<string>> RetiredAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT key || ':' || version FROM messaging.key_versions WHERE retired ORDER BY key, version", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<string> retired = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            retired.Add(reader.GetString(0));
        }

        return retired;
    }
}