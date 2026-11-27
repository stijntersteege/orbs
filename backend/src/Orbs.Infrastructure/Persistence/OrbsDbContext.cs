using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Jobs;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;
using Orbs.Domain.Tools;
using Orbs.Domain.Users;

namespace Orbs.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work. Entity mappings live in <c>Configurations/</c>.
/// Migrations in <c>Migrations/</c> target PostgreSQL; <see cref="Sqlite.SqliteOrbsDbContext"/> carries the SQLite ones.
/// Owned data (orbs, providers, context, chats) is filtered to the <see cref="CurrentUser"/> and stamped with them when added.
/// </summary>
public class OrbsDbContext : DbContext, IUnitOfWork, IDataProtectionKeyContext
{
    private readonly CurrentUser? _currentUser;

    public OrbsDbContext(DbContextOptions<OrbsDbContext> options, CurrentUser? currentUser = null) : base(options) =>
        _currentUser = currentUser;

    /// <summary>For provider-specific subclasses that keep their own migrations.</summary>
    protected OrbsDbContext(DbContextOptions options, CurrentUser? currentUser) : base(options) =>
        _currentUser = currentUser;

    public DbSet<Orb> Orbs => Set<Orb>();

    public DbSet<ModelProvider> Providers => Set<ModelProvider>();

    public DbSet<ContextSource> ContextSources => Set<ContextSource>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<ToolApproval> ToolApprovals => Set<ToolApproval>();

    public DbSet<McpServer> McpServers => Set<McpServer>();

    public DbSet<Integration> Integrations => Set<Integration>();

    public DbSet<Memory> Memories => Set<Memory>();

    public DbSet<ContextChunk> ContextChunks => Set<ContextChunk>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>False at design time, without a user scope, and for system work: then every row is visible.</summary>
    private bool FilterByOwner => _currentUser is { IsSystem: false };

    /// <summary>Nobody signed in filters to an empty id, which matches nothing that has an owner.</summary>
    private Guid OwnerFilterId => _currentUser?.UserId ?? Guid.Empty;

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwners();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwners();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrbsDbContext).Assembly);
        FilterOwned<Orb>(modelBuilder);
        FilterOwned<ModelProvider>(modelBuilder);
        FilterOwned<ContextSource>(modelBuilder);
        FilterOwned<Conversation>(modelBuilder);
        FilterOwned<Job>(modelBuilder);
        FilterOwned<ToolApproval>(modelBuilder);
        FilterOwned<McpServer>(modelBuilder);
        FilterOwned<Integration>(modelBuilder);
        FilterOwned<Memory>(modelBuilder);
        FilterOwned<ContextChunk>(modelBuilder);
    }

    /// <summary>Applies the owner filter to one owned aggregate. Every owned aggregate is listed in <see cref="OnModelCreating"/>.</summary>
    private void FilterOwned<T>(ModelBuilder modelBuilder) where T : OwnedAggregateRoot =>
        modelBuilder.Entity<T>().HasQueryFilter(e => !FilterByOwner || e.OwnerId == OwnerFilterId);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // SQLite cannot sort or compare DateTimeOffset, so store it as a sortable number there.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    private void StampOwners()
    {
        if (_currentUser?.UserId is not { } userId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<OwnedAggregateRoot>())
        {
            if (entry.State == EntityState.Added && entry.Entity.OwnerId == Guid.Empty)
            {
                entry.Entity.AssignOwner(userId);
            }
        }
    }
}
