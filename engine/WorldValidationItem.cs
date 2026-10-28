namespace FourD;

internal sealed partial class WorldValidation
{
    private void Item(ItemDefinition item, Pose4? validPose)
    {
        var owner = FindBuilding(item.Building, Field(item.Source, "Building"), item.Id);
        switch (item.Shape)
        {
            case Sphere4 ball:
                Ball(item, ball, owner);
                break;
            case HiddenRingItem hidden:
                HiddenRing(item, hidden, validPose, owner);
                break;
            default:
                Error(LoadErrorCode.UnsupportedDefinition, Field(item.Source, "Shape"), "This item shape is not implemented.", item.Id);
                break;
        }
    }

    private void Ball(ItemDefinition item, Sphere4 ball, BuildingDefinition? owner)
    {
        var source = Field(item.Source, "Shape");
        Id(ball.Part.Value, Field(source, "Part"), item.Id);
        if (PositiveNumber(ball.Radius, Field(source, "Radius"), "Ball radius", item.Id))
        {
            var w = item.Pose.Position.W;
            if (2 * ball.Radius <= World.SamplingDistance)
                Error(LoadErrorCode.InvalidGeometry, Field(source, "Radius"), "Ball must be thicker than the sampling distance.", item.Id);
            else if (owner is not null && ValidRange(owner.WRange) && double.IsFinite(w) && !Covers(owner, w - ball.Radius, w + ball.Radius))
                Error(LoadErrorCode.BuildingCoverage, Field(item.Source, "Building"), "Ball extends outside its building.", item.Id);
        }
        Interaction(item, null, owner);
    }

    private void HiddenRing(ItemDefinition item, HiddenRingItem hidden, Pose4? validPose, BuildingDefinition? owner)
    {
        var source = Field(item.Source, "Shape");
        if (double.IsFinite(item.Pose.Position.W) && sections.Values.All(section => section.W != item.Pose.Position.W))
            Error(LoadErrorCode.InvalidPose, Field(item.Source, "Pose.Position.W"), "Initial W must equal a section W.", item.Id);
        FixedThickness(item.Pose, item.Source, item.Id);
        Shape(hidden.Body, Field(source, "Body"), item.Id, validPose);
        var errorsBeforeInteraction = errors.Count;
        Interaction(item, hidden.Body, owner);
        var ringValid = Ring(item, hidden, Field(source, "Ring"));
        if (ringValid && errors.Count == errorsBeforeInteraction && validPose is Pose4 pose)
        {
            HiddenStructure(item, hidden.Ring, pose);
            RingCoverage(item, hidden.Ring, owner);
        }
    }

    // Reports what is wrong with the ring itself, and returns whether it is a usable shape.
    private bool Ring(ItemDefinition item, HiddenRingItem hidden, Source source)
    {
        var ring = hidden.Ring;
        if (ring is null)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "Ring is required.", item.Id);
            return false;
        }
        Id(ring.Part.Value, Field(source, "Part"), item.Id);
        if (hidden.Body?.Parts?.Any(part => part?.Id == ring.Part) == true)
            Error(LoadErrorCode.DuplicateId, Field(source, "Part"), "Ring part ID duplicates a body part.", item.Id);

        var errorsBefore = errors.Count;
        if (!Box4.ToArray(ring.Center).All(double.IsFinite))
            Error(LoadErrorCode.InvalidNumber, Field(source, "Center"), "Ring center must be finite.", item.Id);
        if (!Box4.AxesOf(ring.Rotation).SelectMany(axis => axis).All(double.IsFinite))
            Error(LoadErrorCode.InvalidNumber, Field(source, "Rotation"), "Ring rotation must be finite.", item.Id);
        else if (!IsRotation(Box4.AxesOf(ring.Rotation)))
            Error(LoadErrorCode.InvalidPose, Field(source, "Rotation"), "Ring rotation must be right-handed orthonormal.", item.Id);
        var major = PositiveNumber(ring.MajorRadius, Field(source, "MajorRadius"), "Ring major radius", item.Id);
        var tube = PositiveNumber(ring.TubeRadius, Field(source, "TubeRadius"), "Ring tube radius", item.Id);
        if (major && tube && ring.TubeRadius >= ring.MajorRadius)
            Error(LoadErrorCode.InvalidGeometry, Field(source, "TubeRadius"), "Tube radius must be less than the major radius.", item.Id);
        else if (tube && 2 * ring.TubeRadius <= World.SamplingDistance)
            Error(LoadErrorCode.InvalidGeometry, Field(source, "TubeRadius"), "Ring tube must be thicker than the sampling distance.", item.Id);
        return errors.Count == errorsBefore;
    }

    // At rest the ring must be at least its tube radius, ε and 2 cm from every section, so that no section
    // cuts it and standing the item up is what brings it into one.
    private void HiddenStructure(ItemDefinition item, Ring4 ring, Pose4 pose)
    {
        var round = Round4.From(item.Id, ring, pose);
        foreach (var section in sections.Values)
        {
            if (round.TooCloseToSection(section.W))
                Error(LoadErrorCode.HiddenStructureTooClose, Field(item.Source, "Shape.Ring.Center"),
                    $"Ring is too close to section {section.Id.Value}.", item.Id);
        }
    }

    // Standing the item up either way must keep the ring inside its building.
    private void RingCoverage(ItemDefinition item, Ring4 ring, BuildingDefinition? owner)
    {
        if (owner is null || !ValidRange(owner.WRange)) return;
        var outside = new[] { -1d, 1d }
            .SelectMany(sign => WorldInterference.Sweep(item, sign))
            .Select(pose => Round4.From(item.Id, ring, pose).WSpan)
            .Any(span => !Covers(owner, span.Min, span.Max));
        if (outside)
            Error(LoadErrorCode.BuildingCoverage, Field(item.Source, "Building"), "Ring's full turning range exceeds its building.", item.Id);
    }

    private static bool Covers(BuildingDefinition building, double min, double max) =>
        min >= building.WRange.Min - World.RoundingMargin && max <= building.WRange.Max + World.RoundingMargin;
}
