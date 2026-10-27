namespace FourD;

internal static class GeometryMath
{
    internal static Vec3 Add(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    internal static Vec3 Scale(Vec3 a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    internal static Vec3 Transform(Rotation3 r, Vec3 p) => Add(Add(Scale(r.X, p.X), Scale(r.Y, p.Y)), Scale(r.Z, p.Z));
    internal static Vec3 Transform(Rotation4 r, Vec3 p) => new(
        r.X.X * p.X + r.Y.X * p.Y + r.Z.X * p.Z,
        r.X.Y * p.X + r.Y.Y * p.Y + r.Z.Y * p.Z,
        r.X.Z * p.X + r.Y.Z * p.Y + r.Z.Z * p.Z);
}

// The same closed W segments back slicing and the later support/collision queries.
internal sealed record StaticEntity(EntityId Id, Pose4 Pose, BoxPart[] Parts, Interval[] Segments, bool ThroughObject)
{
    internal SliceGeometry Slice()
    {
        var positions = new List<double>();
        var normals = new List<double>();
        var uvs = new List<double>();
        var ids = new List<PartId>();
        foreach (var part in Parts.OrderBy(p => p.Id.Value, StringComparer.Ordinal))
            foreach (var face in part.FaceUvs.OrderBy(f => f.Axis).ThenBy(f => f.Positive))
            {
                var h = part.HalfSize;
                var sign = face.Positive ? 1d : -1d;
                Vec3[] corners = face.Axis switch
                {
                    Axis3.X => [new(sign * h.X, -h.Y, -h.Z), new(sign * h.X, h.Y, -h.Z), new(sign * h.X, h.Y, h.Z), new(sign * h.X, -h.Y, h.Z)],
                    Axis3.Y => [new(-h.X, sign * h.Y, -h.Z), new(-h.X, sign * h.Y, h.Z), new(h.X, sign * h.Y, h.Z), new(h.X, sign * h.Y, -h.Z)],
                    _ => [new(-h.X, -h.Y, sign * h.Z), new(h.X, -h.Y, sign * h.Z), new(h.X, h.Y, sign * h.Z), new(-h.X, h.Y, sign * h.Z)]
                };
                var localNormal = face.Axis switch { Axis3.X => new Vec3(sign, 0, 0), Axis3.Y => new Vec3(0, sign, 0), _ => new Vec3(0, 0, sign) };
                var normal = GeometryMath.Transform(Pose.Rotation, GeometryMath.Transform(part.Rotation, localNormal));
                // Reversing W also reverses the projected XYZ orientation.
                foreach (var index in face.Positive == (Pose.Rotation.W.W > 0) ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 })
                {
                    var corner = corners[index];
                    var point = GeometryMath.Add(part.Center, GeometryMath.Transform(part.Rotation, corner));
                    var worldPoint = GeometryMath.Add(new(Pose.Position.X, Pose.Position.Y, Pose.Position.Z), GeometryMath.Transform(Pose.Rotation, point));
                    positions.AddRange([worldPoint.X, worldPoint.Y, worldPoint.Z]);
                    normals.AddRange([normal.X, normal.Y, normal.Z]);
                    var (u, v) = face.Axis switch { Axis3.X => (corner.Z, corner.Y), Axis3.Y => (corner.X, corner.Z), _ => (corner.X, corner.Y) };
                    uvs.AddRange([(face.Positive ? u : -u) * face.Scale.X + face.Offset.X, v * face.Scale.Y + face.Offset.Y]);
                    ids.Add(part.Id);
                }
            }
        return new(Id, positions.AsReadOnly(), normals.AsReadOnly(), uvs.AsReadOnly(), ids.AsReadOnly(), ThroughObject);
    }
}
