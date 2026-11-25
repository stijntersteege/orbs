namespace Orbs.Domain.Orbs;

public enum OrbColor { Plasma, Ocean, Sky, Mint, Sun, Coral, Bubblegum }

public enum OrbShape { Circle, Blob, Squircle, Drop }

public enum OrbEyes { Dots, Happy, Wide, Sleepy, Wink, Visor }

public enum OrbMouth { Smile, Open, Flat, Grin, None }

public enum OrbAccessory { None, Antenna, Halo, Sprout, Ring, Headphones, Crown }

/// <summary>
/// The face of an orb. A value object: two orbs that look the same have equal appearances.
/// Customizing the face is a first-class feature; it is how people tell orbs apart.
/// </summary>
public sealed record OrbAppearance(
    OrbColor Color,
    OrbShape Shape,
    OrbEyes Eyes,
    OrbMouth Mouth,
    OrbAccessory Accessory,
    bool Blush,
    bool Glow,
    bool Floating)
{
    public static OrbAppearance Default { get; } =
        new(OrbColor.Plasma, OrbShape.Circle, OrbEyes.Happy, OrbMouth.Smile, OrbAccessory.Antenna, Blush: true, Glow: true, Floating: true);
}
