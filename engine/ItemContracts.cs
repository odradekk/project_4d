namespace FourD;

// Decision note: 2026-10-26-prologue-greybox

/// <summary>
/// A four-dimensional item the player can hold and turn. Its pose places the ball's center, or the nut
/// body's origin; the ring is placed relative to that pose.
/// </summary>
public sealed record ItemDefinition(
    EntityId Id, Pose4 Pose, Source Source, BuildingId Building, ItemShape Shape,
    TurnableProperties Interaction)
    : EntityDefinition(Id, Pose, Source);

public abstract record ItemShape;

/// <summary>The points within <paramref name="Radius"/> of the item's center.</summary>
public sealed record Sphere4(PartId Part, double Radius) : ItemShape;

/// <summary>An ordinary body plus a ring that rests between sections until the body is stood up.</summary>
public sealed record HiddenRingItem(BoxAssembly Body, Ring4 Ring) : ItemShape;

/// <summary>
/// The points within <paramref name="TubeRadius"/> of a circle of radius <paramref name="MajorRadius"/>.
/// The circle lies in the local XY plane of <paramref name="Rotation"/> about <paramref name="Center"/>,
/// both given in the item's local frame.
/// </summary>
public sealed record Ring4(PartId Part, Vec4 Center, Rotation4 Rotation, double MajorRadius, double TubeRadius);
