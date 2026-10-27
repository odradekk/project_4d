namespace FourD;

public sealed partial class World
{
    /// <summary>
    /// Returns the world XYZ bounds of the entity's slice in the section, or of only one part of it. Empty when
    /// the entity, or the part, has no slice there; Rejected(UnknownId) for an unknown entity, section or part.
    /// </summary>
    public SliceBoundsResult GetSliceBounds(EntityId entity, SectionId section, PartId? part = null)
    {
        SliceBoundsResult Reject(string detail) => new SliceBoundsResult.Rejected(Fail(FailureCode.UnknownId, detail, entity));
        var target = sections.FirstOrDefault(candidate => candidate.Id == section);
        if (target is null || ReadEntity(entity) is not EntityReadResult.Found found) return Reject("Unknown entity or section.");
        if (part is { } id && !PartIds(found.Entity.Definition).Contains(id)) return Reject("Unknown part.");

        var geometry = (SectionGeometryResult.Geometry)GetGeometryAtW(target.W);
        var slice = geometry.Entities.FirstOrDefault(candidate => candidate.Entity == entity);
        if (slice is null) return new SliceBoundsResult.Empty();
        var vertices = Enumerable.Range(0, slice.Parts.Count).Where(i => part is null || slice.Parts[i] == part).ToArray();
        if (vertices.Length == 0) return new SliceBoundsResult.Empty();

        double Lowest(int axis) => vertices.Min(i => slice.Positions[3 * i + axis]);
        double Highest(int axis) => vertices.Max(i => slice.Positions[3 * i + axis]);
        return new SliceBoundsResult.Bounds(new(Lowest(0), Lowest(1), Lowest(2)), new(Highest(0), Highest(1), Highest(2)));
    }

    /// <summary>
    /// Returns the unit normal axis of the plane that two local axes of the entity, or of one of its parts, span in
    /// the world XYZ projection. The sign carries no meaning: the first of X, Y and Z that is not zero is positive.
    /// Degenerate when the projections of the axes are parallel or vanish; Rejected(InvalidInput) for equal or
    /// unknown axes, Rejected(NoSlice) when the entity or part has no slice in the section, and Rejected(UnknownId)
    /// for an unknown entity, section or part.
    /// </summary>
    public SliceDirectionResult GetSlicePlaneNormal(EntityId entity, SectionId section, Axis3 first, Axis3 second, PartId? part = null)
    {
        SliceDirectionResult Reject(FailureCode code, string detail) => new SliceDirectionResult.Rejected(Fail(code, detail, entity));
        var bounds = GetSliceBounds(entity, section, part);
        if (bounds is SliceBoundsResult.Rejected rejected) return new SliceDirectionResult.Rejected(rejected.Failure);
        if (first == second || !Enum.IsDefined(first) || !Enum.IsDefined(second))
            return Reject(FailureCode.InvalidInput, "Axes must be two different axes.");
        if (bounds is SliceBoundsResult.Empty) return Reject(FailureCode.NoSlice, "No slice in the section.");

        var state = ((EntityReadResult.Found)ReadEntity(entity)).Entity;
        var u = Box4.ToArray(SliceAxis(state, part, first));
        var v = Box4.ToArray(SliceAxis(state, part, second));
        var normal = Box4.Cross3(u, v);
        var length = Math.Sqrt(Box4.Dot3(normal, normal));
        if (length <= RoundingMargin) return new SliceDirectionResult.Degenerate();

        var unit = normal.Take(3).Select(value => value / length).ToArray();
        var sign = unit.First(value => Math.Abs(value) > RoundingMargin) < 0 ? -1 : 1;
        return new SliceDirectionResult.Direction(new(sign * unit[0], sign * unit[1], sign * unit[2]));
    }

    private static IReadOnlyList<BoxPart> BoxParts(EntityDefinition definition) => definition switch
    {
        BuildingElementDefinition element => element.Shape.Parts,
        FixedDefinition fixedObject => fixedObject.Shape.Parts,
        _ => DynamicShapes.Parts(definition)
    };

    private static IEnumerable<PartId> PartIds(EntityDefinition definition) =>
        BoxParts(definition).Select(part => part.Id).Concat(DynamicShapes.Rounds(definition, definition.Pose).Select(round => round.Part));

    // The world image of a unit vector along an axis of the part's local frame: a box part's rotation or a ring's,
    // both turned by the entity's pose. A part without a frame of its own, a ball, takes the entity's.
    private static Vec4 SliceAxis(EntityState entity, PartId? part, Axis3 axis)
    {
        var rotation = entity.Pose.Rotation;
        if (BoxParts(entity.Definition).FirstOrDefault(candidate => candidate.Id == part) is { } box)
            return Transform(rotation, axis switch { Axis3.X => box.Rotation.X, Axis3.Y => box.Rotation.Y, _ => box.Rotation.Z });
        if (entity.Definition is ItemDefinition { Shape: HiddenRingItem hidden } && hidden.Ring.Part == part)
            return Transform(rotation, LocalAxis(hidden.Ring.Rotation, axis));
        return LocalAxis(rotation, axis);
    }
}
