namespace FourD;

internal sealed partial class WorldValidation
{
    // Scene data are single-precision numbers, so a face counts as flat, a lofted face as parallel to its counterpart,
    // and a track as lying on an axis, to within a micrometre.
    private const double SceneTolerance = 1e-6;

    private void Device(DeviceDefinition device, Pose4? validPose)
    {
        Loft(device.Shape, Field(device.Source, "Shape"), device.Id);
        Motion(device, validPose);
    }

    private static Source At(Source source, string path) => source with { Property = $"{source.Property}{path}" };

    private void Loft(LoftShape? shape, Source source, EntityId entity)
    {
        if (shape?.Segments is null || shape.Segments.Count == 0)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "A loft needs at least one segment.", entity);
            return;
        }
        var ids = new HashSet<PartId>();
        double? previousEnd = null;
        for (var i = 0; i < shape.Segments.Count; i++)
        {
            var location = At(source, $".Segments[{i}]");
            var keys = shape.Segments[i]?.Keys;
            if (keys is null || keys.Count < 2)
            {
                Error(LoadErrorCode.InvalidGeometry, location, "A segment needs at least two keys.", entity);
                continue;
            }
            Segment(keys, location, entity, ids, previousEnd);
            previousEnd = keys[^1]?.W;
        }
    }

    private void Segment(IReadOnlyList<LoftKey> keys, Source location, EntityId entity, HashSet<PartId> ids, double? previousEnd)
    {
        var errorsBefore = errors.Count;
        for (var j = 0; j < keys.Count; j++)
        {
            var key = At(location, $".Keys[{j}]");
            if (keys[j]?.Parts is null || keys[j].Parts.Count == 0)
            {
                Error(LoadErrorCode.InvalidGeometry, key, "A key needs at least one part.", entity);
                continue;
            }
            if (!double.IsFinite(keys[j].W)) Error(LoadErrorCode.InvalidNumber, At(key, ".W"), "Key W must be finite.", entity);
            else if (j > 0 && keys[j - 1] is not null && keys[j].W <= keys[j - 1].W)
                Error(LoadErrorCode.InvalidInterval, At(key, ".W"), "Keys must have increasing W.", entity);
            else if (j == 0 && keys[j].W < previousEnd)
                Error(LoadErrorCode.InvalidInterval, At(key, ".W"), "A segment must start where the one before it ends or later.", entity);
            if (j == 0 && keys[j].Parts.Any(part => part is not null && !ids.Add(part.Id)))
                Error(LoadErrorCode.DuplicateId, key, "Part IDs are unique within a device.", entity);
            var sameParts = j > 0 && keys[0]?.Parts is not null && keys[0].Parts.Count == keys[j].Parts.Count;
            if (j > 0 && !sameParts) Error(LoadErrorCode.InvalidGeometry, key, "Every key of a segment needs the same parts.", entity);
            for (var k = 0; k < keys[j].Parts.Count; k++)
                if (sameParts) Corresponding(keys[0].Parts[k], keys[j].Parts[k], At(key, $".Parts[{k}]"), entity);
                else ConvexSolid(keys[j].Parts[k], At(key, $".Parts[{k}]"), entity);
        }
        if (errors.Count > errorsBefore) return;

        for (var j = 0; j + 1 < keys.Count; j++)
            for (var k = 0; k < keys[j].Parts.Count; k++)
                Piece(new(keys[j], keys[j + 1], k, false, false), At(location, $".Keys[{j}].Parts[{k}]"), entity);
    }

    // Reports what is wrong with a part, and whether it is a closed convex solid with faces that wind outward.
    private void ConvexSolid(ConvexPart? part, Source location, EntityId entity)
    {
        var errorsBefore = errors.Count;
        if (part is null)
        {
            Error(LoadErrorCode.InvalidGeometry, location, "Part is required.", entity);
            return;
        }
        Id(part.Id.Value, At(location, ".Id"), entity);
        if (part.Vertices is null || part.Faces is null || part.Faces.Count < 4)
        {
            Error(LoadErrorCode.InvalidGeometry, location, "A part needs vertices and at least four faces.", entity);
            return;
        }
        if (part.Vertices.Any(vertex => !Box4.ToArray(vertex).All(double.IsFinite)))
        {
            Error(LoadErrorCode.InvalidNumber, At(location, ".Vertices"), "Vertices must be finite.", entity);
            return;
        }
        foreach (var face in part.Faces)
        {
            if (face?.Vertices is null || face.Vertices.Count < 3 || face.Vertices.Any(vertex => vertex < 0 || vertex >= part.Vertices.Count))
                Error(LoadErrorCode.InvalidGeometry, At(location, ".Faces"), "A face lists at least three vertices of the part.", entity);
            else if (!ValidUv(face.Uv))
                Error(LoadErrorCode.InvalidGeometry, At(location, ".Faces"), "UV axes must be valid, scale nonzero and offsets finite.", entity);
        }
        if (errors.Count == errorsBefore) CheckConvex(part, location, entity);
    }

    private static bool ValidUv(FaceUv? uv) =>
        uv is not null && Enum.IsDefined(uv.Axis) && uv.Scale.X != 0 && uv.Scale.Y != 0
        && new[] { uv.Scale.X, uv.Scale.Y, uv.Offset.X, uv.Offset.Y }.All(double.IsFinite);

    // A part is a closed convex solid when every edge joins two faces, one running each way, and every face is a
    // strictly convex polygon with all the vertices of the part behind its plane, which also fixes the winding. A
    // face that is not flat is found by the piece check, which sees every face of the loft as a facet. The
    // comparisons are written so that the NaN of a face without area fails them.
    private void CheckConvex(ConvexPart part, Source location, EntityId entity)
    {
        void Fail(string detail) => Error(LoadErrorCode.InvalidGeometry, At(location, ".Faces"), detail, entity);
        var vertices = part.Vertices.Select(vertex => new[] { vertex.X, vertex.Y, vertex.Z }).ToArray();
        var edges = part.Faces
            .SelectMany(face => face.Vertices.Select((vertex, i) => (vertex, face.Vertices[(i + 1) % face.Vertices.Count])))
            .ToList();
        if (edges.Distinct().Count() != edges.Count || edges.Any(edge => !edges.Contains((edge.Item2, edge.Item1))))
        {
            Fail("Each edge must join exactly two faces, one running each way.");
            return;
        }

        foreach (var face in part.Faces)
        {
            var corners = face.Vertices.Select(vertex => vertices[vertex]).ToArray();
            // The sum of the cross products of consecutive corners is twice the area vector of a flat polygon.
            var area = corners.Select((corner, i) => Box4.Cross3(corner, corners[(i + 1) % corners.Length]))
                .Aggregate((sum, cross) => [sum[0] + cross[0], sum[1] + cross[1], sum[2] + cross[2]]);
            var length = Math.Sqrt(Box4.Dot3(area, area));
            var normal = area.Select(value => value / length).ToArray();
            var plane = Box4.Dot3(normal, corners[0]);
            if (vertices.Any(vertex => !(Box4.Dot3(normal, vertex) <= plane + SceneTolerance)))
                Fail("A part must be convex, and its faces must wind counterclockwise seen from outside.");
            else if (!IsConvexPolygon(corners, normal))
                Fail("A face must be a convex polygon with no straight angle.");
        }
    }

    // Whether every corner turns left, seen against the normal, by more than the rounding margin.
    private static bool IsConvexPolygon(double[][] corners, double[] normal) => Enumerable.Range(0, corners.Length).All(i =>
    {
        var (first, second, third) = (corners[i], corners[(i + 1) % corners.Length], corners[(i + 2) % corners.Length]);
        return Box4.Dot3(Box4.Cross3(Sub3(second, first), Sub3(third, second)), normal) > World.RoundingMargin;
    });

    private static double[] Sub3(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];

    // A part at a later key has the same ID, vertices count, faces and UVs as at the first key of its segment.
    private void Corresponding(ConvexPart? first, ConvexPart? later, Source location, EntityId entity)
    {
        ConvexSolid(later, location, entity);
        if (first is null || later is null) return;
        var sameFaces = (first.Faces ?? []).Zip(later.Faces ?? []).All(pair =>
            pair.First?.Uv == pair.Second?.Uv && (pair.First?.Vertices ?? []).SequenceEqual(pair.Second?.Vertices ?? []));
        var sameShape = first.Id == later.Id && first.Vertices?.Count == later.Vertices?.Count && first.Faces?.Count == later.Faces?.Count;
        if (!sameShape || !sameFaces)
            Error(LoadErrorCode.InvalidGeometry, location, "A part must keep its ID, vertices count, faces and UVs from the first key of its segment.", entity);
    }

    // The piece between two keys must keep its faces flat and parallel to those of the key before, and be thicker
    // than the sampling distance along every facet.
    private void Piece(LoftPiece piece, Source location, EntityId entity)
    {
        for (var f = 0; f < piece.Facets.Length; f++)
        {
            var facet = piece.Facets[f];
            var origin = piece.Vertices[facet.Vertices[0]];
            var heights = piece.Vertices.Select(vertex => Box4.Dot(facet.Normal, Box4.Sub(vertex, origin))).ToArray();
            var onFacet = facet.Vertices.ToHashSet();
            if (heights.Where((_, vertex) => onFacet.Contains(vertex)).Any(height => Math.Abs(height) > SceneTolerance))
            {
                Error(LoadErrorCode.InvalidGeometry, location, "A face and its counterpart at the next key must be parallel, so that the loft stays flat.", entity);
                return;
            }
            if (-heights.Min() <= World.SamplingDistance)
            {
                Error(LoadErrorCode.InvalidGeometry, location, "A part must be thicker than the sampling distance.", entity);
                return;
            }
        }
    }

    private void Motion(DeviceDefinition device, Pose4? validPose)
    {
        var source = Field(device.Source, "Motion");
        switch (device.Motion)
        {
            case DeviceRotation rotation:
                if (!Enum.IsDefined(rotation.Plane)) Error(LoadErrorCode.InvalidMotion, At(source, ".Plane"), "Unknown rotation plane.", device.Id);
                if (!Box4.ToArray(rotation.Pivot).All(double.IsFinite)) Error(LoadErrorCode.InvalidNumber, At(source, ".Pivot"), "Pivot must be finite.", device.Id);
                MotionRange(rotation.Limits, rotation.Initial, source, device.Id);
                break;
            case DeviceTranslation translation:
                if (!Enum.IsDefined(translation.Axis)) Error(LoadErrorCode.InvalidMotion, At(source, ".Axis"), "Unknown track axis.", device.Id);
                else if (validPose is Pose4 pose && !OnWorldAxis(World.LocalAxis(pose.Rotation, translation.Axis)))
                    Error(LoadErrorCode.InvalidMotion, At(source, ".Axis"), "The track must run along the world X, Y or Z axis, with no part in W.", device.Id);
                MotionRange(translation.Limits, translation.Initial, source, device.Id);
                break;
            default:
                Error(LoadErrorCode.InvalidMotion, source, "A device needs a rotation or a translation.", device.Id);
                break;
        }
    }

    private static bool OnWorldAxis(Vec4 direction)
    {
        double[] spatial = [direction.X, direction.Y, direction.Z];
        return Math.Abs(direction.W) <= SceneTolerance
            && spatial.Count(value => Math.Abs(Math.Abs(value) - 1) <= SceneTolerance) == 1
            && spatial.Count(value => Math.Abs(value) <= SceneTolerance) == 2;
    }

    private void MotionRange(Interval limits, double initial, Source source, EntityId entity)
    {
        if (!double.IsFinite(limits.Min) || !double.IsFinite(limits.Max))
            Error(LoadErrorCode.InvalidNumber, At(source, ".Limits"), "Limits must be finite.", entity);
        else if (limits.Min >= limits.Max)
            Error(LoadErrorCode.InvalidMotion, At(source, ".Limits"), "Limits must have positive length.", entity);
        else if (double.IsFinite(initial) && (initial < limits.Min || initial > limits.Max))
            Error(LoadErrorCode.InvalidMotion, At(source, ".Initial"), "The initial position must lie within the limits.", entity);
        if (!double.IsFinite(initial)) Error(LoadErrorCode.InvalidNumber, At(source, ".Initial"), "The initial position must be finite.", entity);
    }
}
