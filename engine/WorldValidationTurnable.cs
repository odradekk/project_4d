namespace FourD;

internal sealed partial class WorldValidation
{
    private void Interaction(TurnableDefinition t, BuildingDefinition? building, IEnumerable<SectionDefinition> sections)
    {
        var interaction = t.Interaction;
        var source = Field(t.Source, "Interaction");
        if (interaction is null) { Error(LoadErrorCode.InvalidInteraction, source, "Interaction is required.", t.Id); return; }
        if (!double.IsFinite(interaction.HorizontalRadius) || !double.IsFinite(interaction.Reach) || ToArray(interaction.Hinge).Any(v => !double.IsFinite(v)))
        { Error(LoadErrorCode.InvalidNumber, source, "Interaction values must be finite.", t.Id); return; }
        if (interaction.HorizontalRadius <= 0 || interaction.Reach <= 0 || !Enum.IsDefined(interaction.LongAxis))
        { Error(LoadErrorCode.InvalidInteraction, source, "Radius, reach and long axis must be valid.", t.Id); return; }
        if (t.Shape?.Parts is null || t.Shape.Parts.Count == 0 || t.Shape.Parts.Any(p => p is null)) return;
        var axis=(int)interaction.LongAxis;
        double Component(Vec3 v) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        var bounds = t.Shape.Parts.Select(p => (Min: Component(p.Center) - Math.Abs(Component(p.Rotation.X))*p.HalfSize.X - Math.Abs(Component(p.Rotation.Y))*p.HalfSize.Y - Math.Abs(Component(p.Rotation.Z))*p.HalfSize.Z,
            Max: Component(p.Center) + Math.Abs(Component(p.Rotation.X))*p.HalfSize.X + Math.Abs(Component(p.Rotation.Y))*p.HalfSize.Y + Math.Abs(Component(p.Rotation.Z))*p.HalfSize.Z)).ToArray();
        var hinge=Component(interaction.Hinge);
        if (hinge-bounds.Min(x=>x.Min) < .02-1e-10 || Math.Abs(bounds.Max(x=>x.Max)-hinge-interaction.Reach)>1e-10)
            Error(LoadErrorCode.InvalidInteraction, source, "Hinge and reach must match shape.", t.Id);
        // W extrema during either quarter-turn include the W thickness of each vertex.
        var farthest=bounds.SelectMany(b=>new[]{Math.Abs(b.Min-hinge),Math.Abs(b.Max-hinge)}).Max();
        var sweptW=Math.Sqrt(farthest*farthest+World.Epsilon*World.Epsilon);
        if(building is not null && (t.Pose.Position.W-sweptW<building.WRange.Min-1e-10 || t.Pose.Position.W+sweptW>building.WRange.Max+1e-10))
            Error(LoadErrorCode.BuildingCoverage, Field(t.Source,"Building"), "Object's full turning range exceeds its building.",t.Id);
        foreach(var direction in new[] {-1d,1d}) foreach(var b in bounds)
        {
            var ends=new[] {t.Pose.Position.W + direction*(b.Min-hinge),t.Pose.Position.W + direction*(b.Max-hinge)};
            if(building is not null && (ends.Min()<building.WRange.Min || ends.Max()>building.WRange.Max)) Error(LoadErrorCode.BuildingCoverage, Field(t.Source,"Building"), "Object exceeds its building.",t.Id);
            if(sections.Any(s=>ends.Any(w=>Math.Abs(w-s.W)<.02-1e-10))) Error(LoadErrorCode.PartBoundaryTooClose, source, "Part end is too close to a section.",t.Id);
        }
    }
}
