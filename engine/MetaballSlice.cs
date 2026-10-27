using System.Buffers;

namespace FourD;

// The slice of a metaball body: the points of a section where the sum of the balls' fields reaches the threshold,
// meshed on the lattice of cubes of the world with linear interpolation of the field along their edges
// (docs/frame/contract-views.md).
internal static class MetaballSlice
{
    // The one part of a slice: fused balls have no single ball the surface comes from.
    private static readonly PartId BodyPart = new("body");

    // The edge of the cubes of the lattice, whose nodes are the multiples of it in world coordinates.
    private const double CellSize = 0.2;

    // The most nodes along a side of the lattice, which bounds the memory a wide body takes.
    private const int MaxNodes = 128;

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

        // The cell is CellSize, or a whole multiple of it for a body so wide that the lattice would pass MaxNodes.
        var low = Enumerable.Range(0, 3).Select(axis => discs.Min(disc => disc.Center[axis] - disc.Reach)).ToArray();
        var high = Enumerable.Range(0, 3).Select(axis => discs.Max(disc => disc.Center[axis] + disc.Reach)).ToArray();
        var widest = Enumerable.Range(0, 3).Max(axis => high[axis] - low[axis]);
        var cell = CellSize * Math.Max(1, Math.Ceiling(widest / (CellSize * (MaxNodes - 3))));

        // The lattice nodes from the one below the discs to the one above them, which all lie outside the body.
        var first = low.Select(value => (int)Math.Floor(value / cell)).ToArray();
        var counts = Enumerable.Range(0, 3).Select(axis => (int)Math.Ceiling(high[axis] / cell) - first[axis] + 1).ToArray();

        // The field less the threshold, at each node; it is positive inside the body.
        var size = counts[0] * counts[1] * counts[2];
        var values = ArrayPool<double>.Shared.Rent(size);
        try
        {
            Array.Fill(values, -shape.Threshold, 0, size);
            foreach (var disc in discs) AddField(disc, cell, first, counts, values);

            (double X, double Y, double Z) Place(int node) =>
            (
                (first[0] + node / (counts[1] * counts[2])) * cell,
                (first[1] + node / counts[2] % counts[1]) * cell,
                (first[2] + node % counts[2]) * cell
            );
            double[] Position(int node)
            {
                var (x, y, z) = Place(node);
                return [x, y, z];
            }
            double[] Cut(int inside, int outside)
            {
                var (x0, y0, z0) = Place(inside);
                var (x1, y1, z1) = Place(outside);
                var fraction = values[inside] / (values[inside] - values[outside]);
                return [x0 + fraction * (x1 - x0), y0 + fraction * (y1 - y0), z0 + fraction * (z1 - z0)];
            }
            Marching.Surface(BodyPart, counts, values, Position, Cut, buffer);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(values);
        }
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
    private static void AddField(Disc disc, double cell, int[] first, int[] counts, double[] values)
    {
        int[] from = new int[3], to = new int[3];
        for (var axis = 0; axis < 3; axis++)
        {
            from[axis] = Math.Max(first[axis], (int)Math.Floor((disc.Center[axis] - disc.Reach) / cell));
            to[axis] = Math.Min(first[axis] + counts[axis] - 1, (int)Math.Ceiling((disc.Center[axis] + disc.Reach) / cell));
        }
        for (var x = from[0]; x <= to[0]; x++)
            for (var y = from[1]; y <= to[1]; y++)
                for (var z = from[2]; z <= to[2]; z++)
                {
                    double dx = x * cell - disc.Center[0], dy = y * cell - disc.Center[1], dz = z * cell - disc.Center[2];
                    var t = 1 - (dx * dx + dy * dy + dz * dz + disc.Offset2) / disc.Radius2;
                    if (t <= 0) continue;
                    values[((x - first[0]) * counts[1] + y - first[1]) * counts[2] + z - first[2]] += disc.Weight * t * t;
                }
    }
}
