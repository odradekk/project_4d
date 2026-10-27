namespace FourD;

internal sealed class WorldValidation
{
    private readonly List<LoadError> errors = [];
    private const double MatrixTolerance = 1e-10;
    internal static LoadError[] Check(WorldDescription? description)
    {
        var validation = new WorldValidation();
        validation.Validate(description);
        return validation.errors.OrderBy(e => e.Source.NodePath, StringComparer.Ordinal)
            .ThenBy(e => e.Source.Property, StringComparer.Ordinal).ThenBy(e => e.Code.ToString(), StringComparer.Ordinal)
            .ThenBy(e => e.Entity?.Value, StringComparer.Ordinal).ThenBy(e => e.Operation?.Value, StringComparer.Ordinal)
            .ThenBy(e => e.StepIndex).ToArray();
    }
    private static Source Field(Source source, string field) => source with { Property = $"{source.Property}.{field}" };
    private void Error(LoadErrorCode code, Source source, string detail, EntityId? entity = null) =>
        errors.Add(new(code, source, entity, null, null, detail, Array.Empty<Contact>()));
    private void Validate(WorldDescription? d)
    {
        var root = new Source("", "World");
        if (d is null) { Error(LoadErrorCode.InvalidGeometry, root, "World description is required."); return; }
        var sections = new Dictionary<SectionId, SectionDefinition>();
        if (d.Sections is null || d.Sections.Count == 0) Error(LoadErrorCode.InvalidSectionOrder, root, "At least one section is required.");
        double? previous = null;
        foreach (var section in d.Sections ?? [])
        {
            if (section is null) { Error(LoadErrorCode.InvalidSectionOrder, root, "Section is required."); continue; }
            Id(section.Id.Value, Field(section.Source, "Id"));
            if (!sections.TryAdd(section.Id, section)) Error(LoadErrorCode.DuplicateId, Field(section.Source, "Id"), "Duplicate section ID.");
            if (!double.IsFinite(section.W)) Error(LoadErrorCode.InvalidNumber, section.Source, "Section W must be finite.");
            else if (previous is double last && section.W <= last) Error(LoadErrorCode.InvalidSectionOrder, section.Source, "Sections must have increasing W.");
            previous = section.W;
        }
        var buildings = new Dictionary<BuildingId, BuildingDefinition>();
        if (d.Buildings is null) Error(LoadErrorCode.InvalidGeometry, root, "Buildings collection is required.");
        foreach (var building in d.Buildings ?? [])
        {
            if (building is null) { Error(LoadErrorCode.InvalidGeometry, root, "Building is required."); continue; }
            Id(building.Id.Value, building.Source);
            if (!buildings.TryAdd(building.Id, building)) Error(LoadErrorCode.DuplicateId, building.Source, "Duplicate building ID.");
            Range(building.WRange, Field(building.Source, "WRange"));
        }
        var ids = new HashSet<EntityId>();
        if (d.Entities is null) Error(LoadErrorCode.InvalidGeometry, root, "Entities collection is required.");
        foreach (var e in d.Entities ?? [])
        {
            if (e is null) { Error(LoadErrorCode.InvalidGeometry, root, "Entity is required."); continue; }
            Id(e.Id.Value, e.Source, e.Id);
            if (!ids.Add(e.Id)) Error(LoadErrorCode.DuplicateId, e.Source, "Duplicate entity ID.", e.Id);
            var beforePose = errors.Count;
            Pose(e.Pose, Field(e.Source, "Pose"), e.Id);
            Pose4? validPose = errors.Count == beforePose ? e.Pose : null;
            switch (e)
            {
                case BuildingElementDefinition b:
                    var rangeValid = Range(b.WRange, Field(b.Source, "WRange"), b.Id);
                    if (!buildings.TryGetValue(b.Building, out var building)) Error(LoadErrorCode.MissingReference, Field(b.Source, "Building"), "Unknown building.", b.Id);
                    else if (rangeValid && ValidRange(building.WRange) && (b.WRange.Min < building.WRange.Min || b.WRange.Max > building.WRange.Max))
                        Error(LoadErrorCode.BuildingCoverage, Field(b.Source, b.WRange.Min < building.WRange.Min ? "WRange.Min" : "WRange.Max"),
                            "Element extends outside its building range.", b.Id);
                    if (b.Pose.Position.W != 0 || b.Pose.Rotation.W.W != 1) Error(LoadErrorCode.InvalidPose, Field(b.Source, "Pose"), "Building pose must be an XYZ transform with W=0.", b.Id);
                    Gaps(b, rangeValid);
                    Shape(b.Shape, b.Source, b.Id, validPose);
                    break;
                case FixedDefinition f:
                    if (!sections.TryGetValue(f.Section, out var section)) Error(LoadErrorCode.MissingReference, f.Source, "Unknown section.", f.Id);
                    else if (double.IsFinite(section.W) && f.Pose.Position.W != section.W) Error(LoadErrorCode.InvalidPose, f.Source, "Fixed position W must equal its section W.", f.Id);
                    FixedThickness(f);
                    Shape(f.Shape, f.Source, f.Id, validPose);
                    break;
                default:
                    Error(LoadErrorCode.UnsupportedDefinition, e.Source, "This entity category is not implemented.", e.Id);
                    break;
            }
        }
        Unsupported(d.Seams, s => s.Source, root, "Seams");
        Unsupported(d.SharedValues, s => s.Source, root, "SharedValues");
        Unsupported(d.ExpectedOperations, s => s.Source, root, "ExpectedOperations");
    }
    private void Unsupported<T>(IReadOnlyList<T>? values, Func<T, Source> source, Source root, string name)
    {
        if (values is null) Error(LoadErrorCode.InvalidGeometry, root, $"{name} collection is required.");
        else foreach (var value in values) Error(LoadErrorCode.UnsupportedDefinition, value is null ? root : source(value), $"{name} are not implemented.");
    }
    private void Id(string? id, Source source, EntityId? entity = null)
    {
        if (string.IsNullOrWhiteSpace(id)) Error(LoadErrorCode.InvalidId, source, "ID must be nonempty.", entity);
    }
    private static bool ValidRange(Interval r) => double.IsFinite(r.Min) && double.IsFinite(r.Max) && r.Min < r.Max;
    private bool Range(Interval r, Source source, EntityId? entity = null)
    {
        if (!double.IsFinite(r.Min)) Error(LoadErrorCode.InvalidNumber, Field(source, "Min"), "Interval ends must be finite.", entity);
        if (!double.IsFinite(r.Max)) Error(LoadErrorCode.InvalidNumber, Field(source, "Max"), "Interval ends must be finite.", entity);
        if (double.IsFinite(r.Min) && double.IsFinite(r.Max) && r.Min >= r.Max) Error(LoadErrorCode.InvalidInterval, source, "Interval must have positive length.", entity);
        return ValidRange(r);
    }
    private void Gaps(BuildingElementDefinition b, bool rangeValid)
    {
        var source = Field(b.Source, "Gaps");
        if (b.Gaps is null) { Error(LoadErrorCode.InvalidInterval, source, "Gaps collection is required.", b.Id); return; }
        var valid = rangeValid;
        double? previous = null;
        foreach (var gap in b.Gaps)
        {
            var gapValid = Range(gap, source, b.Id);
            if (gapValid && rangeValid && (gap.Min < b.WRange.Min || gap.Max > b.WRange.Max || previous is double last && gap.Min < last))
            { Error(LoadErrorCode.InvalidInterval, source, "Gaps must be ordered, nonoverlapping, and inside the element range.", b.Id); valid = false; }
            valid &= gapValid;
            previous = gap.Max;
        }
        if (valid)
        {
            var start = b.WRange.Min;
            foreach (var gap in b.Gaps)
            {
                Thickness(gap.Min - start, source, b.Id);
                start = gap.Max;
            }
            Thickness(b.WRange.Max - start, b.Gaps.Count == 0 ? Field(b.Source, "WRange") : source, b.Id);
        }
    }
    private void Thickness(double value, Source source, EntityId entity)
    {
        if (value > 0 && value <= World.SamplingDistance) Error(LoadErrorCode.InvalidGeometry, source, "Positive solid thickness must exceed the sweep sampling distance.", entity);
    }
    private void Pose(Pose4 pose, Source source, EntityId entity)
    {
        double[][] axes = [ToArray(pose.Rotation.X), ToArray(pose.Rotation.Y), ToArray(pose.Rotation.Z), ToArray(pose.Rotation.W)];
        if (ToArray(pose.Position).Concat(axes.SelectMany(a => a)).Any(v => !double.IsFinite(v)))
        { Error(LoadErrorCode.InvalidNumber, source, "Pose components must be finite.", entity); return; }
        if (!Rotation(axes) ||
            Math.Abs(pose.Rotation.W.X) > MatrixTolerance || Math.Abs(pose.Rotation.W.Y) > MatrixTolerance || Math.Abs(pose.Rotation.W.Z) > MatrixTolerance)
            Error(LoadErrorCode.InvalidPose, source, "Static pose must be right-handed orthonormal and preserve the W axis.", entity);
    }
    private void Shape(BoxAssembly? shape, Source source, EntityId entity, Pose4? validPose)
    {
        if (shape?.Parts is null || shape.Parts.Count == 0) { Error(LoadErrorCode.InvalidGeometry, source, "At least one box part is required.", entity); return; }
        var ids = new HashSet<PartId>();
        for (var i = 0; i < shape.Parts.Count; i++)
        {
            var part = shape.Parts[i];
            var location = source with { Property = $"{source.Property}.Parts[{i}]" };
            if (part is null) { Error(LoadErrorCode.InvalidGeometry, location, "Box part is required.", entity); continue; }
            var beforePart = errors.Count;
            Id(part.Id.Value, Field(location, "Id"), entity);
            if (!ids.Add(part.Id)) Error(LoadErrorCode.DuplicateId, Field(location, "Id"), "Duplicate part ID.", entity);
            if (ToArray(part.Center).Any(v => !double.IsFinite(v))) Error(LoadErrorCode.InvalidNumber, Field(location, "Center"), "Box coordinates must be finite.", entity);
            if (ToArray(part.HalfSize).Any(v => !double.IsFinite(v))) Error(LoadErrorCode.InvalidNumber, Field(location, "HalfSize"), "Box lengths must be finite.", entity);
            else if (part.HalfSize.X <= 0 || part.HalfSize.Y <= 0 || part.HalfSize.Z <= 0) Error(LoadErrorCode.InvalidGeometry, Field(location, "HalfSize"), "Box half sizes must be positive.", entity);
            else if (2 * Math.Min(part.HalfSize.X, Math.Min(part.HalfSize.Y, part.HalfSize.Z)) <= World.SamplingDistance)
                Error(LoadErrorCode.InvalidGeometry, Field(location, "HalfSize"), "Box thickness must exceed the sweep sampling distance.", entity);
            double[][] axes = [ToArray(part.Rotation.X), ToArray(part.Rotation.Y), ToArray(part.Rotation.Z)];
            if (axes.SelectMany(a => a).Any(v => !double.IsFinite(v))) Error(LoadErrorCode.InvalidNumber, Field(location, "Rotation"), "Part rotation must be finite.", entity);
            else if (!Rotation(axes)) Error(LoadErrorCode.InvalidPose, Field(location, "Rotation"), "Part rotation must be right-handed orthonormal.", entity);
            if (part.FaceUvs is null || part.FaceUvs.Count != 6 || part.FaceUvs.Any(f => f is null) || part.FaceUvs.Select(f => (f.Axis, f.Positive)).Distinct().Count() != 6)
            { Error(LoadErrorCode.InvalidGeometry, Field(location, "FaceUvs"), "Each of the six box faces requires exactly one UV definition.", entity); continue; }
            foreach (var uv in part.FaceUvs)
            {
                if (!Enum.IsDefined(uv.Axis) || uv.Scale.X == 0 || uv.Scale.Y == 0) Error(LoadErrorCode.InvalidGeometry, Field(location, "FaceUvs"), "UV axes must be valid and scale must be nonzero.", entity);
                if (new[] { uv.Scale.X, uv.Scale.Y, uv.Offset.X, uv.Offset.Y }.Any(v => !double.IsFinite(v))) Error(LoadErrorCode.InvalidNumber, Field(location, "FaceUvs"), "UV coordinates must be finite.", entity);
            }
            if (errors.Count == beforePart && validPose is Pose4 pose) DerivedGeometry(part, pose, location, entity);
        }
    }
    private void FixedThickness(FixedDefinition definition)
    {
        var w = definition.Pose.Position.W;
        if (!double.IsFinite(w)) return;
        var min = w - World.Epsilon;
        var max = w + World.Epsilon;
        if (!(min < w && w < max) || max - min <= World.SamplingDistance)
            Error(LoadErrorCode.InvalidGeometry, definition.Source with { Property = $"{definition.Source.Property}.Pose.Position.W" },
                "Fixed W thickness must remain positive and exceed the sweep sampling distance at this coordinate precision.", definition.Id);
    }
    private void DerivedGeometry(BoxPart part, Pose4 pose, Source source, EntityId entity)
    {
        var slice = new StaticEntity(entity, pose, [part], [], false).Slice();
        if (slice.Uvs.Any(v => !double.IsFinite(v)))
            Error(LoadErrorCode.InvalidNumber, source with { Property = $"{source.Property}.FaceUvs" }, "Derived UV coordinates must be finite.", entity);
        if (slice.Positions.Concat(slice.Normals).Any(v => !double.IsFinite(v)))
        {
            Error(LoadErrorCode.InvalidNumber, source, "Transformed box vertices and normals must be finite.", entity);
            return;
        }
        var vertices = Enumerable.Range(0, slice.Parts.Count).Select(i => Vertex(slice.Positions, i)).ToArray();
        var corners = vertices.Distinct().ToArray();
        if (corners.Length != 8 || !HasVolume(corners))
        {
            Error(LoadErrorCode.InvalidGeometry, source, "Transformed box must retain eight corners and three-dimensional volume at coordinate precision.", entity);
            return;
        }
        for (var i = 0; i < vertices.Length; i += 3)
        {
            var cross = Cross(Direction(vertices[i], vertices[i + 1]), Direction(vertices[i], vertices[i + 2]));
            if (!(Dot(cross, Vertex(slice.Normals, i)) > 0))
            {
                Error(LoadErrorCode.InvalidGeometry, source, "Transformed triangles must retain nonzero area and outward winding at coordinate precision.", entity);
                return;
            }
        }
    }
    private static bool HasVolume(Vec3[] corners)
    {
        var directions = corners.Skip(1).Select(c => Direction(corners[0], c)).ToArray();
        var plane = directions.Select(d => Cross(directions[0], d)).FirstOrDefault(n => n != default);
        return plane != default && directions.Any(d => Dot(Direction(default, plane), d) != 0);
    }
    private static Vec3 Vertex(IReadOnlyList<double> values, int vertex) => new(values[3 * vertex], values[3 * vertex + 1], values[3 * vertex + 2]);
    private static Vec3 Direction(Vec3 a, Vec3 b)
    {
        // Halving before subtraction prevents overflow between finite opposite endpoints.
        var difference = new Vec3(b.X / 2 - a.X / 2, b.Y / 2 - a.Y / 2, b.Z / 2 - a.Z / 2);
        var scale = Math.Max(Math.Abs(difference.X), Math.Max(Math.Abs(difference.Y), Math.Abs(difference.Z)));
        return scale == 0 ? default : new(difference.X / scale, difference.Y / scale, difference.Z / scale);
    }
    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static double[] ToArray(Vec3 v) => [v.X, v.Y, v.Z];
    private static double[] ToArray(Vec4 v) => [v.X, v.Y, v.Z, v.W];
    private static bool Rotation(double[][] axes)
    {
        for (var i = 0; i < axes.Length; i++)
            for (var j = 0; j < axes.Length; j++)
            {
                var dot = 0d;
                for (var k = 0; k < axes.Length; k++) dot += axes[i][k] * axes[j][k];
                if (Math.Abs(dot - (i == j ? 1 : 0)) > MatrixTolerance) return false;
            }
        var matrix = axes.Select(a => a.ToArray()).ToArray();
        var determinant = 1d;
        for (var i = 0; i < matrix.Length; i++)
        {
            var pivot = i;
            for (var j = i + 1; j < matrix.Length; j++) if (Math.Abs(matrix[j][i]) > Math.Abs(matrix[pivot][i])) pivot = j;
            if (pivot != i) { (matrix[i], matrix[pivot]) = (matrix[pivot], matrix[i]); determinant = -determinant; }
            var diagonal = matrix[i][i];
            if (diagonal == 0) return false;
            determinant *= diagonal;
            for (var j = i + 1; j < matrix.Length; j++)
            {
                var factor = matrix[j][i] / diagonal;
                for (var k = i + 1; k < matrix.Length; k++) matrix[j][k] -= factor * matrix[i][k];
            }
        }
        return Math.Abs(determinant - 1) <= MatrixTolerance;
    }
}
