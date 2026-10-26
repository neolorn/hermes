using System.Collections.Generic;
using System.Linq;

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

    /// <summary>A context on its own connection to <paramref name="connection"/>, as the migrations run.</summary>
    internal static MessagingContext Create(string connection)
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
    }

    private static void Provider(NpgsqlDbContextOptionsBuilder provider)
    {
        provider.MigrationsHistoryTable(HistoryTable, Schema);
    }

    private static string InList(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value}'"));
    }
}