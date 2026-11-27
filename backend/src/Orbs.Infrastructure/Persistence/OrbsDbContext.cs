using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orbs.Application.Abstractions;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work. Entity mappings live in <c>Configurations/</c>.
/// Migrations in <c>Migrations/</c> target PostgreSQL; <see cref="Sqlite.SqliteOrbsDbContext"/> carries the SQLite ones.
/// </summary>
public class OrbsDbContext : DbContext, IUnitOfWork, IDataProtectionKeyContext
{
    public OrbsDbContext(DbContextOptions<OrbsDbContext> options) : base(options) { }

    /// <summary>For provider-specific subclasses that keep their own migrations.</summary>
    protected OrbsDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Orb> Orbs => Set<Orb>();

    public DbSet<ModelProvider> Providers => Set<ModelProvider>();

    public DbSet<ContextSource> ContextSources => Set<ContextSource>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrbsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // SQLite cannot sort or compare DateTimeOffset, so store it as a sortable number there.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}
