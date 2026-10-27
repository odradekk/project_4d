namespace FourD;

internal sealed partial class WorldValidation
{
    private void Turnable(TurnableDefinition turnable, Pose4? validPose)
    {
        if (FindSection(turnable.Section, Field(turnable.Source, "Section"), turnable.Id) is { } section
            && double.IsFinite(section.W) && turnable.Pose.Position.W != section.W)
            Error(LoadErrorCode.InvalidPose, Field(turnable.Source, "Pose.Position.W"), "Initial W must equal section W.", turnable.Id);
        var owner = FindBuilding(turnable.Building, Field(turnable.Source, "Building"), turnable.Id);
        FixedThickness(turnable.Pose, turnable.Source, turnable.Id);
        Shape(turnable.Shape, turnable.Source, turnable.Id, validPose);
        Interaction(turnable, owner);
    }

    private void Interaction(TurnableDefinition turnable, BuildingDefinition? building)
    {
        var interaction = turnable.Interaction;
        var source = Field(turnable.Source, "Interaction");
        if (interaction is null)
        {
            Error(LoadErrorCode.InvalidInteraction, source, "Interaction is required.", turnable.Id);
            return;
        }
        if (!double.IsFinite(interaction.HorizontalRadius) || !double.IsFinite(interaction.Reach)
            || Box4.ToArray(interaction.Hinge).Any(value => !double.IsFinite(value)))
        {
            Error(LoadErrorCode.InvalidNumber, source, "Interaction values must be finite.", turnable.Id);
            return;
        }
        if (interaction.HorizontalRadius <= 0 || interaction.Reach <= 0 || !Enum.IsDefined(interaction.LongAxis))
        {
            Error(LoadErrorCode.InvalidInteraction, source, "Radius, reach and long axis must be valid.", turnable.Id);
            return;
        }
        if (turnable.Shape?.Parts is null || turnable.Shape.Parts.Count == 0 || turnable.Shape.Parts.Any(part => part is null)) return;

        double Along(Vec3 vector) => interaction.LongAxis switch { Axis3.X => vector.X, Axis3.Y => vector.Y, _ => vector.Z };
        double Spread(Vec3 axis, double halfSize) => Math.Abs(Along(axis)) * halfSize;
        var bounds = turnable.Shape.Parts.Select(part => (
            Min: Along(part.Center) - Spread(part.Rotation.X, part.HalfSize.X) - Spread(part.Rotation.Y, part.HalfSize.Y) - Spread(part.Rotation.Z, part.HalfSize.Z),
            Max: Along(part.Center) + Spread(part.Rotation.X, part.HalfSize.X) + Spread(part.Rotation.Y, part.HalfSize.Y) + Spread(part.Rotation.Z, part.HalfSize.Z)))
            .ToArray();
        var hinge = Along(interaction.Hinge);
        if (hinge - bounds.Min(bound => bound.Min) < World.SectionClearance
            || Math.Abs(bounds.Max(bound => bound.Max) - hinge - interaction.Reach) > World.RoundingMargin)
            Error(LoadErrorCode.InvalidInteraction, source, "Hinge and reach must match shape.", turnable.Id);

        // W extrema during either quarter-turn include the W thickness of each vertex.
        var farthest = bounds.SelectMany(bound => new[] { Math.Abs(bound.Min - hinge), Math.Abs(bound.Max - hinge) }).Max();
        var sweptW = Math.Sqrt(farthest * farthest + World.Epsilon * World.Epsilon);
        var buildingSource = Field(turnable.Source, "Building");
        var w = turnable.Pose.Position.W;
        if (building is not null
            && (w - sweptW < building.WRange.Min - World.RoundingMargin || w + sweptW > building.WRange.Max + World.RoundingMargin))
            Error(LoadErrorCode.BuildingCoverage, buildingSource, "Object's full turning range exceeds its building.", turnable.Id);
        foreach (var direction in new[] { -1d, 1d })
            foreach (var bound in bounds)
            {
                double[] ends = [w + direction * (bound.Min - hinge), w + direction * (bound.Max - hinge)];
                if (building is not null && (ends.Min() < building.WRange.Min || ends.Max() > building.WRange.Max))
                    Error(LoadErrorCode.BuildingCoverage, buildingSource, "Object exceeds its building.", turnable.Id);
                if (sections.Values.Any(section => ends.Any(end => Math.Abs(end - section.W) < World.SectionClearance)))
                    Error(LoadErrorCode.PartBoundaryTooClose, source, "Part end is too close to a section.", turnable.Id);
            }
    }
}
