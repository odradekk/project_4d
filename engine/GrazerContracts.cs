namespace FourD;

// Decision note: 2026-10-26-prologue-greybox

public sealed record GrazerDefinition(EntityId Id, Pose4 Pose, Source Source, MetaballShape Shape)
    : EntityDefinition(Id, Pose, Source);

/// <summary>
/// The body is the set of points where the balls' field reaches the threshold. A ball's center is local to the
/// grazer's pose, and a ball contributes <c>Weight × max(0, 1 − distance² / Radius²)²</c>.
/// </summary>
public sealed record MetaballShape(IReadOnlyList<Metaball> Balls, double Threshold);

public sealed record Metaball(PartId Id, Vec4 Center, double Radius, double Weight);
