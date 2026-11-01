using System.Collections.Generic;
using System.Linq;

using Hermes.Conversations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Hermes.Operations;

/// <summary>The library's own context over its schema, <c>messaging</c> (<c>06</c> MSG-OPS-002; <c>07</c> MSG-ENG-005).</summary>
internal sealed class MessagingContext(DbContextOptions<MessagingContext> options) : DbContext(options)
{
    /// <summary>The schema, fixed whatever the library's name (<c>08</c> section 8).</summary>
    internal const string Schema = "messaging";

    /// <summary>The migration history table, in the schema, so the library's history never meets a host's.</summary>
    internal const string HistoryTable = "__migrations_history";

    internal DbSet<KeyVersion> KeyVersions => Set<KeyVersion>();

    internal DbSet<TokenLifetime> TokenLifetimes => Set<TokenLifetime>();

    internal DbSet<AlertState> Alerts => Set<AlertState>();

    internal DbSet<SendRow> Sends => Set<SendRow>();

    internal DbSet<DeliveryRow> Deliveries => Set<DeliveryRow>();

    internal DbSet<AttemptRow> Attempts => Set<AttemptRow>();

    internal DbSet<CallRow> Calls => Set<CallRow>();

    internal DbSet<IdempotencyRow> Idempotency => Set<IdempotencyRow>();

    internal DbSet<RateCounter> RateCounters => Set<RateCounter>();

    internal DbSet<NoticeRow> Notices => Set<NoticeRow>();

    internal DbSet<JobRow> Jobs => Set<JobRow>();

    internal DbSet<SuppressionRow> Suppressions => Set<SuppressionRow>();

    internal DbSet<PreferenceRow> Preferences => Set<PreferenceRow>();

    internal DbSet<PlaceRow> Places => Set<PlaceRow>();

    internal DbSet<HeldReportRow> HeldReports => Set<HeldReportRow>();

    internal DbSet<RejectedReportRow> RejectedReports => Set<RejectedReportRow>();

    internal DbSet<BalanceRow> BalanceReadings => Set<BalanceRow>();

    internal DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();

    internal DbSet<PushAuthFailureRow> PushAuthFailures => Set<PushAuthFailureRow>();

    internal DbSet<EntryRow> Entries => Set<EntryRow>();

    internal DbSet<ConversationRow> Conversations => Set<ConversationRow>();

    internal DbSet<ParticipantRow> Participants => Set<ParticipantRow>();

    internal DbSet<MessageRow> Messages => Set<MessageRow>();

    internal DbSet<MentionRow> Mentions => Set<MentionRow>();

    /// <summary>A context on its own connection to <paramref name="connection"/>, as the migrations run.</summary>
    internal static MessagingContext Create(string connection)
    {
        return new MessagingContext(new DbContextOptionsBuilder<MessagingContext>().UseNpgsql(connection, Provider).Options);
    }

    /// <summary>A context on a host's open connection, which it neither opens nor closes (<c>01</c> MSG-SEND-004).</summary>
    internal static MessagingContext On(NpgsqlConnection connection)
    {
        return new MessagingContext(new DbContextOptionsBuilder<MessagingContext>().UseNpgsql(connection, Provider).Options);
    }

    /// <summary>The options of a context on the library's data source.</summary>
    internal static DbContextOptions<MessagingContext> Options(NpgsqlDataSource source)
    {
        return new DbContextOptionsBuilder<MessagingContext>().UseNpgsql(source, Provider).Options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<KeyVersion>(key =>
        {
            key.ToTable("key_versions", table =>
            {
                table.HasCheckConstraint("ck_key_versions_key", $"\"key\" IN ({InList(KeyVersion.Keys)})");
                table.HasCheckConstraint("ck_key_versions_version", "version >= 1");
                table.HasCheckConstraint("ck_key_versions_value_count", "value_count >= 0");
            });
            key.HasKey(row => new { row.Key, row.Version }).HasName("pk_key_versions");
            key.Property(row => row.Key).HasColumnName("key");
            key.Property(row => row.Version).HasColumnName("version");
            key.Property(row => row.CheckValue).HasColumnName("check_value");
            key.Property(row => row.FirstMadeAt).HasColumnName("first_made_at");
            key.Property(row => row.LastMadeAt).HasColumnName("last_made_at");
            key.Property(row => row.Retired).HasColumnName("retired");
            key.Property(row => row.ValueCount).HasColumnName("value_count");
        });

        modelBuilder.Entity<TokenLifetime>(lifetime =>
        {
            lifetime.ToTable("token_lifetime", table => table.HasCheckConstraint("ck_token_lifetime_id", "id = 1"));
            lifetime.HasKey(row => row.Id).HasName("pk_token_lifetime");
            lifetime.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            lifetime.Property(row => row.Longest).HasColumnName("longest");
        });

        modelBuilder.Entity<AlertState>(alert =>
        {
            alert.ToTable("alerts", table => table.HasCheckConstraint("ck_alerts_condition", $"condition IN ({InList(AlertState.Conditions)})"));
            alert.HasKey(row => new { row.Condition, row.Scope }).HasName("pk_alerts");
            alert.Property(row => row.Condition).HasColumnName("condition");
            alert.Property(row => row.Scope).HasColumnName("scope");
            alert.Property(row => row.Holding).HasColumnName("holding");
            alert.Property(row => row.LastRaisedAt).HasColumnName("last_raised_at");
            alert.Property(row => row.HoldsUntil).HasColumnName("holds_until");
        });

        SendTables(modelBuilder);
        JobTable(modelBuilder);
        ChoiceTables(modelBuilder);
        ReportTables(modelBuilder);
        BalanceTable(modelBuilder);
        PushTables(modelBuilder);
        EntryTable(modelBuilder);
        ConversationTables(modelBuilder);
    }

    /// <summary>The send's tables (<c>01</c> MSG-SEND-003 to MSG-SEND-009, MSG-SEND-014, MSG-SEND-017), each closed set checked by the database.</summary>
    private static void SendTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SendRow>(send =>
        {
            send.ToTable("sends");
            send.HasKey(row => row.Id).HasName("pk_sends");
            send.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            send.Property(row => row.Kind).HasColumnName("kind");
            send.Property(row => row.RecordedAt).HasColumnName("recorded_at");
        });

        modelBuilder.Entity<DeliveryRow>(delivery =>
        {
            delivery.ToTable("deliveries", table =>
            {
                table.HasCheckConstraint("ck_deliveries_channel", $"channel IN ({InList(Spellings.AllChannels)})");
                table.HasCheckConstraint("ck_deliveries_status", $"status IN ({InList(Spellings.AllStatuses)})");
                table.HasCheckConstraint("ck_deliveries_reason", $"reason IS NULL OR reason IN ({InList(Spellings.AllReasons)})");
                table.HasCheckConstraint("ck_deliveries_attempts", "attempts >= 0");
                table.HasCheckConstraint("ck_deliveries_next_sequence", "next_sequence >= 1");
            });
            delivery.HasKey(row => row.Id).HasName("pk_deliveries");
            delivery.HasOne<SendRow>().WithMany().HasForeignKey(row => row.SendId).HasConstraintName("fk_deliveries_sends").OnDelete(DeleteBehavior.Restrict);
            delivery.HasIndex(row => row.SendId).HasDatabaseName("ix_deliveries_send_id");
            delivery.HasIndex(row => new { row.Channel, row.Status, row.DueAt }).HasDatabaseName("ix_deliveries_due");
            delivery.HasIndex(row => row.ContentKeyVersion).HasDatabaseName("ix_deliveries_content_key_version");
            delivery.HasIndex(row => row.Person).HasDatabaseName("ix_deliveries_person");
            delivery.HasIndex(row => row.ProviderReference).HasDatabaseName("ix_deliveries_provider_reference");
            delivery.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            delivery.Property(row => row.SendId).HasColumnName("send_id");
            delivery.Property(row => row.Kind).HasColumnName("kind");
            delivery.Property(row => row.Category).HasColumnName("category");
            delivery.Property(row => row.Channel).HasColumnName("channel");
            delivery.Property(row => row.Status).HasColumnName("status");
            delivery.Property(row => row.Reason).HasColumnName("reason");
            delivery.Property(row => row.Person).HasColumnName("person");
            delivery.Property(row => row.Language).HasColumnName("language");
            delivery.Property(row => row.TimeZone).HasColumnName("time_zone");
            delivery.Property(row => row.PushTopic).HasColumnName("push_topic");
            delivery.Property(row => row.Destination).HasColumnName("destination");
            delivery.Property(row => row.Unsubscribe).HasColumnName("unsubscribe");
            delivery.Property(row => row.Subject).HasColumnName("subject");
            delivery.Property(row => row.Text).HasColumnName("text");
            delivery.Property(row => row.Html).HasColumnName("html");
            delivery.Property(row => row.Title).HasColumnName("title");
            delivery.Property(row => row.Body).HasColumnName("body");
            delivery.Property(row => row.Url).HasColumnName("url");
            delivery.Property(row => row.Tag).HasColumnName("tag");
            delivery.Property(row => row.Icon).HasColumnName("icon");
            delivery.Property(row => row.Badge).HasColumnName("badge");
            delivery.Property(row => row.IdempotencyKey).HasColumnName("idempotency_key");
            delivery.Property(row => row.Parts).HasColumnName("parts").HasColumnType("jsonb");
            delivery.Property(row => row.ContentKeyVersion).HasColumnName("content_key_version");
            delivery.Property(row => row.RecordedAt).HasColumnName("recorded_at");
            delivery.Property(row => row.DueAt).HasColumnName("due_at");
            delivery.Property(row => row.BecameDueAt).HasColumnName("became_due_at");
            delivery.Property(row => row.LastMovedAt).HasColumnName("last_moved_at");
            delivery.Property(row => row.Attempts).HasColumnName("attempts");
            delivery.Property(row => row.FirstHeldAt).HasColumnName("first_held_at");
            delivery.Property(row => row.ClaimedBy).HasColumnName("claimed_by");
            delivery.Property(row => row.ClaimedAt).HasColumnName("claimed_at");
            delivery.Property(row => row.ClaimExpiresAt).HasColumnName("claim_expires_at");
            delivery.Property(row => row.ProviderReference).HasColumnName("provider_reference");
            delivery.Property(row => row.NextSequence).HasColumnName("next_sequence");
        });

        modelBuilder.Entity<AttemptRow>(attempt =>
        {
            attempt.ToTable("attempts", table =>
            {
                table.HasCheckConstraint("ck_attempts_class", $"class IS NULL OR class IN ({InList(Spellings.AllClasses)})");
                table.HasCheckConstraint("ck_attempts_reason", $"reason IS NULL OR reason IN ({InList(Spellings.AllReasons)})");
                table.HasCheckConstraint("ck_attempts_number", "number >= 1");
                table.HasCheckConstraint("ck_attempts_reported", "reported IS NULL OR reported IN ('delivered', 'failed')");
            });
            attempt.HasKey(row => new { row.DeliveryId, row.Number }).HasName("pk_attempts");
            attempt.HasOne<DeliveryRow>().WithMany().HasForeignKey(row => row.DeliveryId).HasConstraintName("fk_attempts_deliveries").OnDelete(DeleteBehavior.Cascade);
            attempt.HasIndex(row => row.Reference).HasDatabaseName("ix_attempts_reference");
            attempt.Property(row => row.DeliveryId).HasColumnName("delivery_id");
            attempt.Property(row => row.Number).HasColumnName("number");
            attempt.Property(row => row.Reference).HasColumnName("reference");
            attempt.Property(row => row.At).HasColumnName("at");
            attempt.Property(row => row.Class).HasColumnName("class");
            attempt.Property(row => row.Reason).HasColumnName("reason");
            attempt.Property(row => row.Reported).HasColumnName("reported");
        });

        modelBuilder.Entity<CallRow>(call =>
        {
            call.ToTable("calls", table =>
            {
                table.HasCheckConstraint("ck_calls_class", $"class IN ({InList(Spellings.AllClasses)})");
                table.HasCheckConstraint("ck_calls_reason", $"reason IS NULL OR reason IN ({InList(Spellings.AllReasons)})");
                table.HasCheckConstraint("ck_calls_push_service", $"push_service IS NULL OR push_service IN ({InList(Spellings.AllServices)})");
                table.HasCheckConstraint("ck_calls_number", "number >= 1");
            });
            call.HasKey(row => new { row.DeliveryId, row.Attempt, row.Number }).HasName("pk_calls");
            call.HasOne<AttemptRow>().WithMany().HasForeignKey(row => new { row.DeliveryId, row.Attempt }).HasConstraintName("fk_calls_attempts").OnDelete(DeleteBehavior.Cascade);
            call.Property(row => row.DeliveryId).HasColumnName("delivery_id");
            call.Property(row => row.Attempt).HasColumnName("attempt");
            call.Property(row => row.Number).HasColumnName("number");
            call.Property(row => row.Class).HasColumnName("class");
            call.Property(row => row.Reason).HasColumnName("reason");
            call.Property(row => row.Status).HasColumnName("status");
            call.Property(row => row.SubscriptionId).HasColumnName("subscription_id");
            call.Property(row => row.PushService).HasColumnName("push_service");
            call.Property(row => row.At).HasColumnName("at");
        });

        modelBuilder.Entity<IdempotencyRow>(key =>
        {
            key.ToTable("idempotency", table => table.HasCheckConstraint("ck_idempotency_hash_version", "hash_version >= 1"));
            key.HasKey(row => new { row.Kind, row.Anonymous, row.Scope, row.Key }).HasName("pk_idempotency");
            key.HasIndex(row => row.LapsesAt).HasDatabaseName("ix_idempotency_lapses_at");
            key.Property(row => row.Kind).HasColumnName("kind");
            key.Property(row => row.Anonymous).HasColumnName("anonymous");
            key.Property(row => row.Scope).HasColumnName("scope");
            key.Property(row => row.Key).HasColumnName("key");
            key.Property(row => row.SendId).HasColumnName("send_id");
            key.Property(row => row.Receipt).HasColumnName("receipt").HasColumnType("jsonb");
            key.Property(row => row.Digest).HasColumnName("digest");
            key.Property(row => row.HashVersion).HasColumnName("hash_version");
            key.Property(row => row.RecordedAt).HasColumnName("recorded_at");
            key.Property(row => row.LapsesAt).HasColumnName("lapses_at");
        });

        modelBuilder.Entity<RateCounter>(counter =>
        {
            counter.ToTable("rate_counters", table =>
            {
                table.HasCheckConstraint("ck_rate_counters_channel", $"channel IN ({InList(Spellings.AllChannels)})");
                table.HasCheckConstraint("ck_rate_counters_count", "count >= 0");
            });
            counter.HasKey(row => new { row.Channel, row.Second }).HasName("pk_rate_counters");
            counter.Property(row => row.Channel).HasColumnName("channel");
            counter.Property(row => row.Second).HasColumnName("second");
            counter.Property(row => row.Count).HasColumnName("count");
        });

        modelBuilder.Entity<NoticeRow>(notice =>
        {
            notice.ToTable("notices", table =>
            {
                table.HasCheckConstraint("ck_notices_status", $"status IN ({InList(Spellings.AllStatuses)})");
                table.HasCheckConstraint("ck_notices_reason", $"reason IS NULL OR reason IN ({InList(Spellings.AllReasons)})");
                table.HasCheckConstraint("ck_notices_sequence", "sequence >= 1");
            });
            notice.HasKey(row => new { row.DeliveryId, row.Sequence }).HasName("pk_notices");
            notice.HasOne<DeliveryRow>().WithMany().HasForeignKey(row => row.DeliveryId).HasConstraintName("fk_notices_deliveries").OnDelete(DeleteBehavior.Cascade);
            notice.HasIndex(row => row.At).HasDatabaseName("ix_notices_at");
            notice.Property(row => row.DeliveryId).HasColumnName("delivery_id");
            notice.Property(row => row.Sequence).HasColumnName("sequence");
            notice.Property(row => row.Status).HasColumnName("status");
            notice.Property(row => row.Reason).HasColumnName("reason");
            notice.Property(row => row.At).HasColumnName("at");
            notice.Property(row => row.ClaimedBy).HasColumnName("claimed_by");
            notice.Property(row => row.ClaimExpiresAt).HasColumnName("claim_expires_at");
        });
    }

    /// <summary>The person's choices (<c>01</c> MSG-SEND-010 to MSG-SEND-014), each closed set checked by the database.</summary>
    private static void ChoiceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SuppressionRow>(suppression =>
        {
            suppression.ToTable("suppressions", table =>
            {
                table.HasCheckConstraint("ck_suppressions_channel", "channel IN ('email', 'sms')");
                table.HasCheckConstraint("ck_suppressions_hash_version", "hash_version >= 1");
            });
            suppression.HasKey(row => new { row.Channel, row.Category, row.Hash }).HasName("pk_suppressions");
            suppression.HasIndex(row => row.HashVersion).HasDatabaseName("ix_suppressions_hash_version");
            suppression.Property(row => row.Channel).HasColumnName("channel");
            suppression.Property(row => row.Category).HasColumnName("category");
            suppression.Property(row => row.Hash).HasColumnName("hash");
            suppression.Property(row => row.HashVersion).HasColumnName("hash_version");
            suppression.Property(row => row.MadeAt).HasColumnName("made_at");
        });

        modelBuilder.Entity<PreferenceRow>(preference =>
        {
            preference.ToTable("preferences", table => table.HasCheckConstraint("ck_preferences_channel", $"channel IN ({InList(Spellings.AllChannels)})"));
            preference.HasKey(row => new { row.Person, row.Category, row.Channel }).HasName("pk_preferences");
            preference.Property(row => row.Person).HasColumnName("person");
            preference.Property(row => row.Category).HasColumnName("category");
            preference.Property(row => row.Channel).HasColumnName("channel");
            preference.Property(row => row.On).HasColumnName("on");
            preference.Property(row => row.SetAt).HasColumnName("set_at");
        });

        modelBuilder.Entity<PlaceRow>(place =>
        {
            place.ToTable("places", table =>
            {
                table.HasCheckConstraint("ck_places_kind", "kind IN ('limit', 'deployment')");
                table.HasCheckConstraint("ck_places_channel", $"channel IN ({InList(Spellings.AllChannels)})");
                table.HasCheckConstraint("ck_places_window_seconds", "window_seconds >= 60");
                table.HasCheckConstraint("ck_places_hash", "(kind = 'limit') = (hash IS NOT NULL AND hash_version IS NOT NULL)");
            });
            place.HasKey(row => row.Id).HasName("pk_places");
            place.HasOne<DeliveryRow>().WithMany().HasForeignKey(row => row.DeliveryId).HasConstraintName("fk_places_deliveries").OnDelete(DeleteBehavior.SetNull);
            place.HasIndex(row => new { row.Channel, row.Category, row.WindowSeconds, row.Kind, row.TakenAt }).HasDatabaseName("ix_places_window");
            place.HasIndex(row => row.DeliveryId).HasDatabaseName("ix_places_delivery_id");
            place.HasIndex(row => row.LapsesAt).HasDatabaseName("ix_places_lapses_at");
            place.HasIndex(row => row.HashVersion).HasDatabaseName("ix_places_hash_version");
            place.Property(row => row.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            place.Property(row => row.DeliveryId).HasColumnName("delivery_id");
            place.Property(row => row.Kind).HasColumnName("kind");
            place.Property(row => row.Channel).HasColumnName("channel");
            place.Property(row => row.Category).HasColumnName("category");
            place.Property(row => row.WindowSeconds).HasColumnName("window_seconds");
            place.Property(row => row.Hash).HasColumnName("hash");
            place.Property(row => row.HashVersion).HasColumnName("hash_version");
            place.Property(row => row.TakenAt).HasColumnName("taken_at");
            place.Property(row => row.LapsesAt).HasColumnName("lapses_at");
        });
    }

    /// <summary>The SMS delivery reports' tables (<c>02</c> MSG-SMS-004): those held, and the rejected counted per minute.</summary>
    private static void ReportTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HeldReportRow>(held =>
        {
            held.ToTable("held_reports", table =>
            {
                table.HasCheckConstraint("ck_held_reports_matched", "matched IN ('delivery', 'provider')");
                table.HasCheckConstraint("ck_held_reports_outcome", "outcome IN ('delivered', 'failed')");
            });
            held.HasKey(row => row.Id).HasName("pk_held_reports");
            held.HasIndex(row => row.Reference).HasDatabaseName("ix_held_reports_reference");
            held.HasIndex(row => row.LapsesAt).HasDatabaseName("ix_held_reports_lapses_at");
            held.Property(row => row.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            held.Property(row => row.Reference).HasColumnName("reference");
            held.Property(row => row.Matched).HasColumnName("matched");
            held.Property(row => row.Outcome).HasColumnName("outcome");
            held.Property(row => row.Source).HasColumnName("source");
            held.Property(row => row.HeldAt).HasColumnName("held_at");
            held.Property(row => row.LapsesAt).HasColumnName("lapses_at");
        });

        modelBuilder.Entity<RejectedReportRow>(rejected =>
        {
            rejected.ToTable("rejected_reports", table => table.HasCheckConstraint("ck_rejected_reports_count", "count >= 1"));
            rejected.HasKey(row => row.Minute).HasName("pk_rejected_reports");
            rejected.Property(row => row.Minute).HasColumnName("minute");
            rejected.Property(row => row.Count).HasColumnName("count");
        });
    }

    /// <summary>
    /// The push subscriptions (<c>02</c> MSG-PUSH-003), a call's link to one set to null when it
    /// is removed (MSG-PUSH-005), and the push services' authorization failures per minute.
    /// </summary>
    private static void PushTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionRow>(subscription =>
        {
            subscription.ToTable("subscriptions", table => table.HasCheckConstraint("ck_subscriptions_service", $"service IN ({InList(Spellings.AllServices)})"));
            subscription.HasKey(row => row.Id).HasName("pk_subscriptions");
            subscription.HasIndex(row => row.EndpointHash).IsUnique().HasDatabaseName("ux_subscriptions_endpoint_hash");
            subscription.HasIndex(row => row.Person).HasDatabaseName("ix_subscriptions_person");
            subscription.HasIndex(row => row.ExpiresAt).HasDatabaseName("ix_subscriptions_expires_at");
            subscription.HasIndex(row => row.VapidVersion).HasDatabaseName("ix_subscriptions_vapid_version");
            subscription.HasIndex(row => row.ContentKeyVersion).HasDatabaseName("ix_subscriptions_content_key_version");
            subscription.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            subscription.Property(row => row.Person).HasColumnName("person");
            subscription.Property(row => row.EndpointHash).HasColumnName("endpoint_hash");
            subscription.Property(row => row.HashVersion).HasColumnName("hash_version");
            subscription.Property(row => row.Endpoint).HasColumnName("endpoint");
            subscription.Property(row => row.Keys).HasColumnName("keys");
            subscription.Property(row => row.ContentKeyVersion).HasColumnName("content_key_version");
            subscription.Property(row => row.VapidVersion).HasColumnName("vapid_version");
            subscription.Property(row => row.Service).HasColumnName("service");
            subscription.Property(row => row.MadeAt).HasColumnName("made_at");
            subscription.Property(row => row.ExpiresAt).HasColumnName("expires_at");
            subscription.Property(row => row.LastAcceptedAt).HasColumnName("last_accepted_at");
        });

        modelBuilder.Entity<CallRow>(call =>
        {
            call.HasOne<SubscriptionRow>().WithMany().HasForeignKey(row => row.SubscriptionId).HasConstraintName("fk_calls_subscriptions").OnDelete(DeleteBehavior.SetNull);
            call.HasIndex(row => row.SubscriptionId).HasDatabaseName("ix_calls_subscription_id");
        });

        modelBuilder.Entity<PushAuthFailureRow>(failure =>
        {
            failure.ToTable("push_auth_failures", table =>
            {
                table.HasCheckConstraint("ck_push_auth_failures_service", $"service IN ({InList(Spellings.AllServices)})");
                table.HasCheckConstraint("ck_push_auth_failures_count", "count >= 1");
            });
            failure.HasKey(row => new { row.Service, row.Minute }).HasName("pk_push_auth_failures");
            failure.Property(row => row.Service).HasColumnName("service");
            failure.Property(row => row.Minute).HasColumnName("minute");
            failure.Property(row => row.Count).HasColumnName("count");
        });
    }

    /// <summary>The in-application list (<c>02</c> MSG-INAPP-001), read a page at a time by person, newest first.</summary>
    private static void EntryTable(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntryRow>(entry =>
        {
            entry.ToTable("entries");
            entry.HasKey(row => row.Id).HasName("pk_entries");
            entry.HasOne<DeliveryRow>().WithMany().HasForeignKey(row => row.DeliveryId).HasConstraintName("fk_entries_deliveries").OnDelete(DeleteBehavior.SetNull);
            entry.HasIndex(row => new { row.Person, row.WrittenAt, row.Id }).HasDatabaseName("ix_entries_person_written_at");
            entry.HasIndex(row => row.WrittenAt).HasDatabaseName("ix_entries_written_at");
            entry.HasIndex(row => row.DeliveryId).HasDatabaseName("ix_entries_delivery_id");
            entry.HasIndex(row => row.ContentKeyVersion).HasDatabaseName("ix_entries_content_key_version");
            entry.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entry.Property(row => row.Person).HasColumnName("person");
            entry.Property(row => row.Kind).HasColumnName("kind");
            entry.Property(row => row.Category).HasColumnName("category");
            entry.Property(row => row.Language).HasColumnName("language");
            entry.Property(row => row.WrittenAt).HasColumnName("written_at");
            entry.Property(row => row.ReadAt).HasColumnName("read_at");
            entry.Property(row => row.DeliveryId).HasColumnName("delivery_id");
            entry.Property(row => row.Target).HasColumnName("target");
            entry.Property(row => row.Parts).HasColumnName("parts").HasColumnType("jsonb");
            entry.Property(row => row.ContentKeyVersion).HasColumnName("content_key_version");
        });
    }

    /// <summary>The SMS balance's readings (<c>02</c> MSG-SMS-006), one per poll.</summary>
    private static void BalanceTable(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BalanceRow>(reading =>
        {
            reading.ToTable("balance_readings");
            reading.HasKey(row => row.ReadAt).HasName("pk_balance_readings");
            reading.Property(row => row.ReadAt).HasColumnName("read_at");
            reading.Property(row => row.Units).HasColumnName("units");
        });
    }

    /// <summary>The job table (<c>06</c> MSG-OPS-004).</summary>
    private static void JobTable(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobRow>(job =>
        {
            job.ToTable("jobs", table => table.HasCheckConstraint("ck_jobs_name", $"name IN ({InList(JobRow.Names)})"));
            job.HasKey(row => row.Name).HasName("pk_jobs");
            job.Property(row => row.Name).HasColumnName("name");
            job.Property(row => row.ClaimedBy).HasColumnName("claimed_by");
            job.Property(row => row.ClaimedAt).HasColumnName("claimed_at");
            job.Property(row => row.ClaimExpiresAt).HasColumnName("claim_expires_at");
            job.Property(row => row.FirstStartedAt).HasColumnName("first_started_at");
            job.Property(row => row.LastStartedAt).HasColumnName("last_started_at");
            job.Property(row => row.LastRenewedAt).HasColumnName("last_renewed_at");
            job.Property(row => row.LastCompletedAt).HasColumnName("last_completed_at");
        });
    }

    private static void Provider(NpgsqlDbContextOptionsBuilder provider)
    {
        provider.MigrationsHistoryTable(HistoryTable, Schema);
    }

    /// <summary>
    /// The conversations' tables (<c>09</c> MSG-CONV-001), each closed set checked by the
    /// database: one group conversation a scope, a participant once a conversation, and a
    /// conversation's messages read a page at a time, newest first.
    /// </summary>
    private static void ConversationTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationRow>(conversation =>
        {
            conversation.ToTable("conversations", table =>
            {
                table.HasCheckConstraint("ck_conversations_type", $"type IN ({InList(ConversationSpellings.AllTypes)})");
                table.HasCheckConstraint("ck_conversations_origin", $"origin IN ({InList(ConversationSpellings.AllOrigins)})");
                table.HasCheckConstraint("ck_conversations_status", $"status IN ({InList(ConversationSpellings.AllStatuses)})");
                table.HasCheckConstraint("ck_conversations_scope_kind", $"scope_kind IN ({InList(ConversationSpellings.AllScopeKinds)})");
                table.HasCheckConstraint("ck_conversations_scope", "(type = 'group') = (scope_kind IS NOT NULL) AND (scope_kind IS NULL) = (scope_reference IS NULL)");
            });
            conversation.HasKey(row => row.Id).HasName("pk_conversations");
            conversation.HasIndex(row => new { row.ScopeKind, row.ScopeReference }).IsUnique().HasDatabaseName("ux_conversations_scope");
            conversation.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            conversation.Property(row => row.Type).HasColumnName("type");
            conversation.Property(row => row.Origin).HasColumnName("origin");
            conversation.Property(row => row.Status).HasColumnName("status");
            conversation.Property(row => row.ScopeKind).HasColumnName("scope_kind");
            conversation.Property(row => row.ScopeReference).HasColumnName("scope_reference");
            conversation.Property(row => row.CreatedAt).HasColumnName("created_at");
            conversation.Property(row => row.LastMessageAt).HasColumnName("last_message_at");
        });

        modelBuilder.Entity<ParticipantRow>(participant =>
        {
            participant.ToTable("participants");
            participant.HasKey(row => row.Id).HasName("pk_participants");
            participant.HasOne<ConversationRow>().WithMany().HasForeignKey(row => row.ConversationId).HasConstraintName("fk_participants_conversations").OnDelete(DeleteBehavior.Cascade);
            participant.HasOne<MessageRow>().WithMany().HasForeignKey(row => row.ReadPosition).HasConstraintName("fk_participants_messages").OnDelete(DeleteBehavior.SetNull);
            participant.HasIndex(row => new { row.ConversationId, row.Person }).IsUnique().HasDatabaseName("ux_participants_conversation_person");
            participant.HasIndex(row => row.Person).HasDatabaseName("ix_participants_person");
            participant.HasIndex(row => row.ReadPosition).HasDatabaseName("ix_participants_read_position");
            participant.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            participant.Property(row => row.ConversationId).HasColumnName("conversation_id");
            participant.Property(row => row.Person).HasColumnName("person");
            participant.Property(row => row.ReadPosition).HasColumnName("read_position");
            participant.Property(row => row.Muted).HasColumnName("muted");
            participant.Property(row => row.JoinedAt).HasColumnName("joined_at");
            participant.Property(row => row.LeftAt).HasColumnName("left_at");
        });

        modelBuilder.Entity<MessageRow>(message =>
        {
            message.ToTable("messages", table =>
            {
                table.HasCheckConstraint("ck_messages_author_kind", $"author_kind IN ({InList(ConversationSpellings.AllAuthorKinds)})");
                table.HasCheckConstraint("ck_messages_type", $"type IN ({InList(ConversationSpellings.AllMessageTypes)})");
            });
            message.HasKey(row => row.Id).HasName("pk_messages");
            message.HasOne<ConversationRow>().WithMany().HasForeignKey(row => row.ConversationId).HasConstraintName("fk_messages_conversations").OnDelete(DeleteBehavior.Cascade);
            message.HasOne<MessageRow>().WithMany().HasForeignKey(row => row.ReplyTo).HasConstraintName("fk_messages_messages").OnDelete(DeleteBehavior.SetNull);
            message.HasIndex(row => new { row.ConversationId, row.SentAt, row.Id }).HasDatabaseName("ix_messages_conversation_sent_at");
            message.HasIndex(row => row.Author).HasDatabaseName("ix_messages_author");
            message.HasIndex(row => row.ReplyTo).HasDatabaseName("ix_messages_reply_to");
            message.HasIndex(row => row.ContentKeyVersion).HasDatabaseName("ix_messages_content_key_version");
            message.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            message.Property(row => row.ConversationId).HasColumnName("conversation_id");
            message.Property(row => row.AuthorKind).HasColumnName("author_kind");
            message.Property(row => row.Author).HasColumnName("author");
            message.Property(row => row.Type).HasColumnName("type");
            message.Property(row => row.Text).HasColumnName("text");
            message.Property(row => row.ContentKeyVersion).HasColumnName("content_key_version");
            message.Property(row => row.ReplyTo).HasColumnName("reply_to");
            message.Property(row => row.SentAt).HasColumnName("sent_at");
            message.Property(row => row.EditedAt).HasColumnName("edited_at");
            message.Property(row => row.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<MentionRow>(mention =>
        {
            mention.ToTable("mentions");
            mention.HasKey(row => new { row.MessageId, row.Person }).HasName("pk_mentions");
            mention.HasOne<MessageRow>().WithMany().HasForeignKey(row => row.MessageId).HasConstraintName("fk_mentions_messages").OnDelete(DeleteBehavior.Cascade);
            mention.HasIndex(row => row.Person).HasDatabaseName("ix_mentions_person");
            mention.Property(row => row.MessageId).HasColumnName("message_id");
            mention.Property(row => row.Person).HasColumnName("person");
        });
    }

    private static string InList(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value}'"));
    }
}