using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orbs.Infrastructure.Persistence;
using Orbs.Infrastructure.Persistence.Sqlite;

namespace Orbs.Infrastructure.Tests;

/// <summary>Both migration histories match the model, and old SQLite databases upgrade cleanly.</summary>
public sealed class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public MigrationTests() => _connection.Open();

    [Fact]
    public void Sqlite_migrations_match_the_model()
    {
        using var db = CreateSqlite();
        db.Database.HasPendingModelChanges().ShouldBeFalse("run: make migration name=<Name>");
    }

    [Fact]
    public void Postgres_migrations_match_the_model()
    {
        var options = new DbContextOptionsBuilder<OrbsDbContext>().UseNpgsql("Host=localhost").Options;
        using var db = new OrbsDbContext(options);
        db.Database.HasPendingModelChanges().ShouldBeFalse("run: make migration name=<Name>");
    }

    [Fact]
    public async Task A_new_sqlite_database_is_migrated()
    {
        await using var db = CreateSqlite();
        await SqliteBaseline.EnsureAsync(db, CancellationToken.None);
        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.Orbs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_database_from_before_migrations_keeps_its_data()
    {
        await using (var old = CreateSqlite())
        {
            // Orbs 0.2 built its SQLite schema (the first migration) without a migration history.
            var first = old.Database.GetMigrations().First();
            await old.GetService<IMigrator>().MigrateAsync(first);
            await old.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO providers (Id, Name, Kind, IsEnabled, CreatedAt, UpdatedAt) VALUES ('6f1c1b1e-0000-4000-8000-000000000001', 'Echo', 'echo', 1, 0, 0)");
        }

        await using var db = CreateSqlite();
        await SqliteBaseline.EnsureAsync(db, CancellationToken.None);
        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.Providers.SingleAsync()).Name.ShouldBe("Echo");
    }

    public void Dispose() => _connection.Dispose();

    private SqliteOrbsDbContext CreateSqlite() =>
        new(new DbContextOptionsBuilder<SqliteOrbsDbContext>().UseSqlite(_connection).Options);
}
