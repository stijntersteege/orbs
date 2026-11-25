using Orbs.Domain.Common;

namespace Orbs.Domain.Orbs;

/// <summary>Points at one model offered by one connected provider.</summary>
public sealed record ModelRef
{
    public const int MaxModelLength = 200;

    public ModelRef(Guid providerId, string model)
    {
        Guard.NotEmpty(providerId, nameof(providerId));
        ProviderId = providerId;
        Model = Guard.Text(model, nameof(model), MaxModelLength);
    }

    public Guid ProviderId { get; }

    public string Model { get; }
}
