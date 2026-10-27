namespace FourD;

// What turnable objects and items have in common: a body of box parts, optional rounds, and turn properties.
internal static class DynamicShapes
{
    internal static bool IsDynamic(EntityDefinition definition) => definition is TurnableDefinition or ItemDefinition;

    internal static bool IsBall(EntityDefinition definition) => definition is ItemDefinition { Shape: Sphere4 };

    internal static TurnableProperties Interaction(EntityDefinition definition) => definition switch
    {
        TurnableDefinition turnable => turnable.Interaction,
        ItemDefinition item => item.Interaction,
        _ => throw new ArgumentException("Only turnables and items can be turned.", nameof(definition))
    };

    internal static BuildingId Building(EntityDefinition definition) => definition switch
    {
        TurnableDefinition turnable => turnable.Building,
        ItemDefinition item => item.Building,
        _ => throw new ArgumentException("Only turnables and items belong to a building.", nameof(definition))
    };

    // The box parts of the body; a ball has none.
    internal static IReadOnlyList<BoxPart> Parts(EntityDefinition definition) => definition switch
    {
        TurnableDefinition turnable => turnable.Shape.Parts,
        ItemDefinition { Shape: HiddenRingItem hidden } => hidden.Body.Parts,
        _ => []
    };

    internal static Box4[] Boxes(EntityDefinition definition, Pose4 pose) =>
        Parts(definition).Select(part => Box4.From(definition.Id, pose, part)).ToArray();

    internal static Round4[] Rounds(EntityDefinition definition, Pose4 pose) => definition switch
    {
        ItemDefinition { Shape: Sphere4 ball } => [Round4.Ball(definition.Id, ball.Part, Box4.ToArray(pose.Position), ball.Radius)],
        ItemDefinition { Shape: HiddenRingItem hidden } => [Round4.From(definition.Id, hidden.Ring, pose)],
        _ => []
    };

    internal static Solid4[] Solids(EntityDefinition definition, Pose4 pose) =>
        [.. Boxes(definition, pose), .. Rounds(definition, pose)];

    // The point the entity turns about when it stands up: the hinge of its body, or the center of a ball.
    internal static Vec4 Hinge(EntityDefinition definition, Pose4 pose) => IsBall(definition)
        ? pose.Position
        : World.Add(pose.Position, World.Transform(pose.Rotation, Interaction(definition).Hinge));
}
