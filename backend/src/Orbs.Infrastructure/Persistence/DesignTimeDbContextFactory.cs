using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orbs.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations. Migrations target PostgreSQL;
/// SQLite installs build their schema straight from the model.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrbsDbContext>
{
    public OrbsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrbsDbContext>()
            .UseNpgsql("Host=localhost;Database=orbs;Username=orbs;Password=orbs", n => n.MigrationsHistoryTable("__ef_migrations"))
            .Options;
        return new OrbsDbContext(options);
    }
}
