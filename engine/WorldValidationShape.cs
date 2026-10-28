namespace FourD;

internal sealed partial class WorldValidation
{
    private void Shape(BoxAssembly? shape, Source source, EntityId entity, Pose4? validPose)
    {
        if (shape?.Parts is null || shape.Parts.Count == 0)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "At least one box part is required.", entity);
            return;
        }
        var ids = new HashSet<PartId>();
        for (var i = 0; i < shape.Parts.Count; i++)
        {
            var part = shape.Parts[i];
            var location = source with { Property = $"{source.Property}.Parts[{i}]" };
            if (part is null)
            {
                Error(LoadErrorCode.InvalidGeometry, location, "Box part is required.", entity);
                continue;
            }
            var errorsBeforePart = errors.Count;
            Id(part.Id.Value, Field(location, "Id"), entity);
            if (!ids.Add(part.Id)) Error(LoadErrorCode.DuplicateId, Field(location, "Id"), "Duplicate part ID.", entity);
            PartBox(part, location, entity);
            if (!FaceUvs(part, location, entity)) continue;
            if (errors.Count == errorsBeforePart && validPose is Pose4 pose) DerivedGeometry(part, pose, location, entity);
        }
    }

    private void PartBox(BoxPart part, Source location, EntityId entity)
    {
        if (Box4.ToArray(part.Center).Any(value => !double.IsFinite(value)))
            Error(LoadErrorCode.InvalidNumber, Field(location, "Center"), "Box coordinates must be finite.", entity);
        var halfSizeSource = Field(location, "HalfSize");
        if (Box4.ToArray(part.HalfSize).Any(value => !double.IsFinite(value)))
            Error(LoadErrorCode.InvalidNumber, halfSizeSource, "Box lengths must be finite.", entity);
        else if (part.HalfSize.X <= 0 || part.HalfSize.Y <= 0 || part.HalfSize.Z <= 0)
            Error(LoadErrorCode.InvalidGeometry, halfSizeSource, "Box half sizes must be positive.", entity);
        else if (2 * Math.Min(part.HalfSize.X, Math.Min(part.HalfSize.Y, part.HalfSize.Z)) <= World.SamplingDistance)
            Error(LoadErrorCode.InvalidGeometry, halfSizeSource, "Box thickness must exceed sampling distance.", entity);
        double[][] axes = [Box4.ToArray(part.Rotation.X), Box4.ToArray(part.Rotation.Y), Box4.ToArray(part.Rotation.Z)];
        if (axes.SelectMany(axis => axis).Any(value => !double.IsFinite(value)))
            Error(LoadErrorCode.InvalidNumber, Field(location, "Rotation"), "Part rotation must be finite.", entity);
        else if (!IsRotation(axes))
            Error(LoadErrorCode.InvalidPose, Field(location, "Rotation"), "Part rotation must be right-handed orthonormal.", entity);
    }

    // Reports malformed face UVs; returns false when the set of faces is unusable.
    private bool FaceUvs(BoxPart part, Source location, EntityId entity)
    {
        var source = Field(location, "FaceUvs");
        if (part.FaceUvs is null || part.FaceUvs.Count != 6 || part.FaceUvs.Any(face => face is null)
            || part.FaceUvs.Select(face => (face.Axis, face.Positive)).Distinct().Count() != 6)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "Each of the six box faces requires exactly one UV definition.", entity);
            return false;
        }
        foreach (var uv in part.FaceUvs)
        {
            if (!Enum.IsDefined(uv.Axis) || uv.Scale.X == 0 || uv.Scale.Y == 0)
                Error(LoadErrorCode.InvalidGeometry, source, "UV axes must be valid and scale must be nonzero.", entity);
            if (new[] { uv.Scale.X, uv.Scale.Y, uv.Offset.X, uv.Offset.Y }.Any(value => !double.IsFinite(value)))
                Error(LoadErrorCode.InvalidNumber, source, "UV coordinates must be finite.", entity);
        }
        return true;
    }

    private void DerivedGeometry(BoxPart part, Pose4 pose, Source source, EntityId entity)
    {
        var slice = new StaticEntity(entity, pose, [part], [], false).Slice();
        if (slice.Uvs.Any(value => !double.IsFinite(value)))
            Error(LoadErrorCode.InvalidNumber, Field(source, "FaceUvs"), "Derived UV coordinates must be finite.", entity);
        if (slice.Positions.Concat(slice.Normals).Any(value => !double.IsFinite(value)))
        {
            Error(LoadErrorCode.InvalidNumber, source, "Transformed box vertices and normals must be finite.", entity);
            return;
        }
        var vertices = Enumerable.Range(0, slice.Parts.Count).Select(i => Vertex(slice.Positions, i)).ToArray();
        var corners = vertices.Distinct().ToArray();
        if (corners.Length != 8 || !HasVolume(corners))
        {
            Error(LoadErrorCode.InvalidGeometry, source, "Transformed box must retain eight corners and volume.", entity);
            return;
        }
        for (var i = 0; i < vertices.Length; i += 3)
        {
            var cross = Cross(Direction(vertices[i], vertices[i + 1]), Direction(vertices[i], vertices[i + 2]));
            if (!(Dot(cross, Vertex(slice.Normals, i)) > 0))
            {
                Error(LoadErrorCode.InvalidGeometry, source,
                    "Transformed triangles must retain nonzero area and outward winding at coordinate precision.", entity);
                return;
            }
        }
    }

    private static bool HasVolume(Vec3[] corners)
    {
        var directions = corners.Skip(1).Select(corner => Direction(corners[0], corner)).ToArray();
        var plane = directions.Select(direction => Cross(directions[0], direction)).FirstOrDefault(normal => normal != default);
        return plane != default && directions.Any(direction => Dot(Direction(default, plane), direction) != 0);
    }

    private static Vec3 Vertex(IReadOnlyList<double> values, int vertex) =>
        new(values[3 * vertex], values[3 * vertex + 1], values[3 * vertex + 2]);

    private static Vec3 Direction(Vec3 from, Vec3 to)
    {
        // Halving before subtraction prevents overflow between finite opposite endpoints.
        var difference = new Vec3(to.X / 2 - from.X / 2, to.Y / 2 - from.Y / 2, to.Z / 2 - from.Z / 2);
        var scale = Math.Max(Math.Abs(difference.X), Math.Max(Math.Abs(difference.Y), Math.Abs(difference.Z)));
        return scale == 0 ? default : new(difference.X / scale, difference.Y / scale, difference.Z / scale);
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
