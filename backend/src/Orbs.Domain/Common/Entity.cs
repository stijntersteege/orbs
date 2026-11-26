namespace Orbs.Domain.Common;

/// <summary>Base type for everything with an identity that outlives its attribute values.</summary>
public abstract class Entity
{
    protected Entity(Guid id)
    {
        Guard.NotEmpty(id, nameof(id));
        Id = id;
    }

    // For EF Core materialization.
    protected Entity() { }

    public Guid Id { get; private set; }
}

/// <summary>An entity that is the consistency boundary for a cluster of objects.</summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot(Guid id, DateTimeOffset createdAt) : base(id)
    {
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    // For EF Core materialization.
    protected AggregateRoot() { }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    protected void Touch(DateTimeOffset now) => UpdatedAt = now;
}
