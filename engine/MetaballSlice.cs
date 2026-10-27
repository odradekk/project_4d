namespace FourD;

// The slice of a metaball body: the points of a section where the sum of the balls' fields reaches the threshold,
// meshed on the lattice of cubes of the world with linear interpolation of the field along their edges
// (docs/frame/contract-views.md).
internal static class MetaballSlice
{
    // The one part of a slice: fused balls have no single ball the surface comes from.
    private static readonly PartId BodyPart = new("body");

    // The edge of the cubes of the lattice, whose nodes are the multiples of it in world coordinates.
    private const double CellSize = 0.02;

    // What a section shows of one ball: where its field is positive, a disc around the ball's center whose radius is
    // `Reach`. The field at distance d from the center, in the section, is Weight × (1 - (d² + Offset²) / Radius²)².
    private sealed record Disc(double[] Center, double Reach, double Offset2, double Radius2, double Weight);

    // The balls' centers in world coordinates.
    private static double[] WorldCenter(Metaball ball, Pose4 pose) =>
        Box4.ToArray(World.Add(pose.Position, World.Transform(pose.Rotation, ball.Center)));

    // The W range the balls reach into, from the lowest end of a ball's field to the highest.
    internal static Interval WSpan(MetaballShape shape, Pose4 pose)
    {
        var spans = shape.Balls.Select(ball =>
        {
            var w = WorldCenter(ball, pose)[3];
            return new Interval(w - ball.Radius, w + ball.Radius);
        }).ToArray();
        return new(spans.Min(span => span.Min), spans.Max(span => span.Max));
    }

    // Appends the triangles of the body of `shape`, turned and placed by `pose`, in the section at w.
    internal static void Append(MetaballShape shape, Pose4 pose, double w, SliceBuffer buffer)
    {
        var discs = shape.Balls.Select(ball => DiscAt(ball, pose, w)).OfType<Disc>().ToArray();
        if (discs.Length == 0) return;

        // The lattice nodes from the one below the discs to the one above them, which all lie outside the body.
        var first = new int[3];
        var counts = new int[3];
        for (var axis = 0; axis < 3; axis++)
        {
            first[axis] = (int)Math.Floor(discs.Min(disc => disc.Center[axis] - disc.Reach) / CellSize);
            var last = (int)Math.Ceiling(discs.Max(disc => disc.Center[axis] + disc.Reach) / CellSize);
            counts[axis] = last - first[axis] + 1;
        }

        // The field less the threshold, at each node; it is positive inside the body.
        var values = new double[counts[0] * counts[1] * counts[2]];
        Array.Fill(values, -shape.Threshold);
        foreach (var disc in discs) AddField(disc, first, counts, values);

        double[] Position(int node) =>
        [
            (first[0] + node / (counts[1] * counts[2])) * CellSize,
            (first[1] + node / counts[2] % counts[1]) * CellSize,
            (first[2] + node % counts[2]) * CellSize
        ];
        double[] Cut(int inside, int outside)
        {
            double[] start = Position(inside), end = Position(outside);
            var fraction = values[inside] / (values[inside] - values[outside]);
            return [start[0] + fraction * (end[0] - start[0]), start[1] + fraction * (end[1] - start[1]), start[2] + fraction * (end[2] - start[2])];
        }
        Marching.Surface(BodyPart, counts, values, Position, Cut, buffer);
    }

    // The disc of the ball in the section at w, or null when the section lies beyond the ball's reach.
    private static Disc? DiscAt(Metaball ball, Pose4 pose, double w)
    {
        var center = WorldCenter(ball, pose);
        var offset2 = (w - center[3]) * (w - center[3]);
        var radius2 = ball.Radius * ball.Radius;
        return offset2 < radius2 ? new(center, Math.Sqrt(radius2 - offset2), offset2, radius2, ball.Weight) : null;
    }

    // Adds the ball's field to the nodes it reaches.
    private static void AddField(Disc disc, int[] first, int[] counts, double[] values)
    {
        int[] from = new int[3], to = new int[3];
        for (var axis = 0; axis < 3; axis++)
        {
            from[axis] = Math.Max(first[axis], (int)Math.Floor((disc.Center[axis] - disc.Reach) / CellSize));
            to[axis] = Math.Min(first[axis] + counts[axis] - 1, (int)Math.Ceiling((disc.Center[axis] + disc.Reach) / CellSize));
        }
        for (var x = from[0]; x <= to[0]; x++)
            for (var y = from[1]; y <= to[1]; y++)
                for (var z = from[2]; z <= to[2]; z++)
                {
                    double dx = x * CellSize - disc.Center[0], dy = y * CellSize - disc.Center[1], dz = z * CellSize - disc.Center[2];
                    var t = 1 - (dx * dx + dy * dy + dz * dz + disc.Offset2) / disc.Radius2;
                    if (t <= 0) continue;
                    values[((x - first[0]) * counts[1] + y - first[1]) * counts[2] + z - first[2]] += disc.Weight * t * t;
                }
    }
}
