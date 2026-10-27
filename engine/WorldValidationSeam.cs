namespace FourD;

internal sealed partial class WorldValidation
{
    private void ValidateSeams(WorldDescription description, Source root)
    {
        if (description.Seams is null)
        {
            Error(LoadErrorCode.InvalidGeometry, root, "Seams collection is required.");
            return;
        }
        var ids = new HashSet<SeamId>();
        var usable = new List<SeamDefinition>();
        foreach (var seam in description.Seams)
        {
            if (seam is null)
            {
                Error(LoadErrorCode.InvalidGeometry, root, "Seam is required.");
                continue;
            }
            var errorsBefore = errors.Count;
            Id(seam.Id.Value, Field(seam.Source, "Id"));
            if (!ids.Add(seam.Id)) Error(LoadErrorCode.DuplicateId, Field(seam.Source, "Id"), "Duplicate seam ID.");
            SeamPlacement(seam);
            SeamSections(seam);
            if (errors.Count == errorsBefore) usable.Add(seam);
        }
        SeamSpacing(usable);
    }

    private void SeamPlacement(SeamDefinition seam)
    {
        if (!Box4.ToArray(seam.Position).All(double.IsFinite))
            Error(LoadErrorCode.InvalidNumber, Field(seam.Source, "Position"), "Seam position must be finite.");
        var normal = seam.HorizontalNormal;
        var source = Field(seam.Source, "HorizontalNormal");
        if (!Box4.ToArray(normal).All(double.IsFinite))
            Error(LoadErrorCode.InvalidNumber, source, "Seam normal must be finite.");
        else if (Math.Abs(normal.Y) > MatrixTolerance || Math.Abs(Math.Sqrt(normal.X * normal.X + normal.Z * normal.Z) - 1) > MatrixTolerance)
            Error(LoadErrorCode.InvalidGeometry, source, "Seam normal must be a horizontal unit vector.");
    }

    // A seam joins at least two sections, listed once each by increasing W.
    private void SeamSections(SeamDefinition seam)
    {
        var source = Field(seam.Source, "Sections");
        if (seam.Sections is null || seam.Sections.Count < 2)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "A seam joins at least two sections.");
            return;
        }
        var seen = new HashSet<SectionId>();
        double? previous = null;
        foreach (var id in seam.Sections)
        {
            if (!seen.Add(id)) Error(LoadErrorCode.DuplicateId, source, "A seam lists a section only once.");
            else if (!sections.TryGetValue(id, out var section)) Error(LoadErrorCode.MissingReference, source, "Unknown section.");
            else
            {
                if (section.W <= previous) Error(LoadErrorCode.InvalidSectionOrder, source, "Seam sections must be listed by increasing W.");
                previous = section.W;
            }
        }
    }

    // Two seams of one section are at least 0.8 m apart on the floor plan; a different W does not make up for it.
    private void SeamSpacing(List<SeamDefinition> usable)
    {
        for (var i = 0; i < usable.Count; i++)
            for (var j = i + 1; j < usable.Count; j++)
            {
                var (first, second) = (usable[i], usable[j]);
                var dx = first.Position.X - second.Position.X;
                var dz = first.Position.Z - second.Position.Z;
                if (first.Sections.Intersect(second.Sections).Any() && Math.Sqrt(dx * dx + dz * dz) < World.SeamSpacing - World.RoundingMargin)
                    Error(LoadErrorCode.SeamTooClose, Field(second.Source, "Position"),
                        $"Seam is closer than {World.SeamSpacing} m to seam {first.Id.Value} in a shared section.");
            }
    }
}
