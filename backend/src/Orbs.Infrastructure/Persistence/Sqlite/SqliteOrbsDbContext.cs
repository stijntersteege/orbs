using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orbs.Application.Accounts;

namespace Orbs.Infrastructure.Persistence.Sqlite;

/// <summary>
/// The same model as <see cref="OrbsDbContext"/>, with its own migrations for SQLite in
/// <c>Persistence/Migrations/Sqlite</c>. SQLite and PostgreSQL need different migration code,
/// so each provider keeps a separate history.
/// </summary>
public sealed class SqliteOrbsDbContext(DbContextOptions<SqliteOrbsDbContext> options, CurrentUser? currentUser = null)
    : OrbsDbContext(options, currentUser);

/// <summary>Used only by <c>dotnet ef</c> to create SQLite migrations.</summary>
internal sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteOrbsDbContext>
{
    public SqliteOrbsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteOrbsDbContext>().UseSqlite("Data Source=orbs-design.db").Options);
}
