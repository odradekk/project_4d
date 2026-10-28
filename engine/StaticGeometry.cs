namespace FourD;

internal static class GeometryMath
{
    internal static Vec3 Add(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    internal static Vec3 Scale(Vec3 vector, double factor) => new(vector.X * factor, vector.Y * factor, vector.Z * factor);
    internal static Vec3 Transform(Rotation3 rotation, Vec3 point) =>
        Add(Add(Scale(rotation.X, point.X), Scale(rotation.Y, point.Y)), Scale(rotation.Z, point.Z));
}

// The same closed W segments back slicing and the later support/collision queries. A record is replaced and never
// edited in place, because World.pairLandings keeps results while it is the same object (see PairLandings).
internal sealed record StaticEntity(EntityId Id, Pose4 Pose, IReadOnlyList<BoxPart> Parts, IReadOnlyList<Interval> Segments, bool ThroughObject)
{
    internal SliceGeometry Slice()
    {
        var positions = new List<double>();
        var normals = new List<double>();
        var uvs = new List<double>();
        var ids = new List<PartId>();
        foreach (var part in Parts.OrderBy(candidate => candidate.Id.Value, StringComparer.Ordinal))
            foreach (var face in part.FaceUvs.OrderBy(uv => uv.Axis).ThenBy(uv => uv.Positive))
            {
                var sign = face.Positive ? 1d : -1d;
                var corners = FaceCorners(part.HalfSize, face.Axis, sign);
                var localNormal = face.Axis switch { Axis3.X => new Vec3(sign, 0, 0), Axis3.Y => new Vec3(0, sign, 0), _ => new Vec3(0, 0, sign) };
                var normal = World.Transform(Pose.Rotation, GeometryMath.Transform(part.Rotation, localNormal));
                // Reversing W also reverses the projected XYZ orientation.
                foreach (var index in face.Positive == (Pose.Rotation.W.W > 0) ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 })
                {
                    var corner = corners[index];
                    var point = GeometryMath.Add(part.Center, GeometryMath.Transform(part.Rotation, corner));
                    var offset = World.Transform(Pose.Rotation, point);
                    positions.AddRange([Pose.Position.X + offset.X, Pose.Position.Y + offset.Y, Pose.Position.Z + offset.Z]);
                    normals.AddRange([normal.X, normal.Y, normal.Z]);
                    var (u, v) = face.Axis switch { Axis3.X => (corner.Z, corner.Y), Axis3.Y => (corner.X, corner.Z), _ => (corner.X, corner.Y) };
                    uvs.AddRange([(face.Positive ? u : -u) * face.Scale.X + face.Offset.X, v * face.Scale.Y + face.Offset.Y]);
                    ids.Add(part.Id);
                }
            }
        return new(Id, positions.AsReadOnly(), normals.AsReadOnly(), uvs.AsReadOnly(), ids.AsReadOnly(), ThroughObject);
    }

    // The four corners of a box face in the part's local coordinates.
    private static Vec3[] FaceCorners(Vec3 half, Axis3 axis, double sign) => axis switch
    {
        Axis3.X =>
        [
            new(sign * half.X, -half.Y, -half.Z), new(sign * half.X, half.Y, -half.Z),
            new(sign * half.X, half.Y, half.Z), new(sign * half.X, -half.Y, half.Z)
        ],
        Axis3.Y =>
        [
            new(-half.X, sign * half.Y, -half.Z), new(-half.X, sign * half.Y, half.Z),
            new(half.X, sign * half.Y, half.Z), new(half.X, sign * half.Y, -half.Z)
        ],
        _ =>
        [
            new(-half.X, -half.Y, sign * half.Z), new(half.X, -half.Y, sign * half.Z),
            new(half.X, half.Y, sign * half.Z), new(-half.X, half.Y, sign * half.Z)
        ]
    };
}
