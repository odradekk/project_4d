using System.Buffers;

namespace FourD;

// The slice of a metaball body: the points of a section where the sum of the balls' fields reaches the threshold,
// meshed on a lattice of cubes with linear interpolation of the field along their edges
// (docs/frame/contract-views.md). The lattice has a node at the grazer's position and a node at every multiple of the
// cell from it along the world axes. It follows the position in X, Y and Z but not the turn of the pose: a grazer that
// moves in XYZ with its turn unchanged keeps its mesh and moves it rigidly with it. When the grazer turns or moves in
// W, the lattice meets its parts differently, and a part smaller than a cube may appear and vanish.
internal static class MetaballSlice
{
    // The one part of a slice: fused balls have no single ball the surface comes from.
    private static readonly PartId BodyPart = new("body");

    // The edge of the cubes of the lattice.
    private const double CellSize = 0.2;

    // The most nodes along a side of the lattice, which bounds the memory a wide body takes.
    private const int MaxNodes = 128;

    // What a section shows of one ball: where its field is positive, a disc around the ball's center whose radius is
    // `Reach`. The field at distance d from the center, in the section, is Weight × (1 - (d² + Offset²) / Radius²)².
    // Places are relative to the grazer's position.
    private sealed record Disc(double[] Center, double Reach, double Offset2, double Radius2, double Weight);

    // Where the center of the ball is from the grazer's position, with the pose's turn.
    private static double[] CenterFrom(Metaball ball, Pose4 pose) =>
        Box4.ToArray(World.Transform(pose.Rotation, ball.Center));

    // Whether the balls reach farther in W, from the lowest end of a ball's field to the highest, than the layer of an
    // ordinary object, 2ε.
    internal static bool ReachesBeyondALayer(MetaballShape shape, Pose4 pose)
    {
        var lowest = shape.Balls.Min(ball => CenterFrom(ball, pose)[3] - ball.Radius);
        var highest = shape.Balls.Max(ball => CenterFrom(ball, pose)[3] + ball.Radius);
        return highest - lowest > 2 * World.Epsilon + World.RoundingMargin;
    }

    // Appends the triangles of the body of `shape`, turned and placed by `pose`, in the section at w.
    internal static void Append(MetaballShape shape, Pose4 pose, double w, SliceBuffer buffer)
    {
        var discs = shape.Balls.Select(ball => DiscAt(ball, pose, w - pose.Position.W)).OfType<Disc>().ToArray();
        if (discs.Length == 0) return;

        var low = Enumerable.Range(0, 3).Select(axis => discs.Min(disc => disc.Center[axis] - disc.Reach)).ToArray();
        var high = Enumerable.Range(0, 3).Select(axis => discs.Max(disc => disc.Center[axis] + disc.Reach)).ToArray();

        // The lattice nodes from the one below the discs to the one above them, which all lie outside the body. A
        // body so wide that this passes MaxNodes nodes along a side gets cubes of a whole number of times CellSize,
        // the smallest number that brings it to MaxNodes or fewer.
        (int[] First, int[] Counts) Lattice(double multiple)
        {
            var edge = multiple * CellSize;
            var lowest = low.Select(value => (int)Math.Floor(value / edge)).ToArray();
            return (lowest, Enumerable.Range(0, 3).Select(axis => (int)Math.Ceiling(high[axis] / edge) - lowest[axis] + 1).ToArray());
        }
        var widest = Enumerable.Range(0, 3).Max(axis => high[axis] - low[axis]);
        var scale = Math.Max(1, Math.Floor(widest / (CellSize * MaxNodes)));
        var (first, counts) = Lattice(scale);
        while (counts.Max() > MaxNodes) (first, counts) = Lattice(++scale);
        var cell = scale * CellSize;

        // The field less the threshold, at each node; it is positive inside the body.
        var size = counts[0] * counts[1] * counts[2];
        var values = ArrayPool<double>.Shared.Rent(size);
        try
        {
            Array.Fill(values, -shape.Threshold, 0, size);
            foreach (var disc in discs) AddField(disc, cell, first, counts, values);

            // The place of a node, from the grazer's position.
            (double X, double Y, double Z) Place(int node) =>
            (
                (first[0] + node / (counts[1] * counts[2])) * cell,
                (first[1] + node / counts[2] % counts[1]) * cell,
                (first[2] + node % counts[2]) * cell
            );
            double[] Position(int node)
            {
                var (x, y, z) = Place(node);
                return [pose.Position.X + x, pose.Position.Y + y, pose.Position.Z + z];
            }
            double[] Cut(int inside, int outside)
            {
                var (x0, y0, z0) = Place(inside);
                var (x1, y1, z1) = Place(outside);
                var fraction = values[inside] / (values[inside] - values[outside]);
                return
                [
                    pose.Position.X + (x0 + fraction * (x1 - x0)),
                    pose.Position.Y + (y0 + fraction * (y1 - y0)),
                    pose.Position.Z + (z0 + fraction * (z1 - z0))
                ];
            }
            Marching.Surface(BodyPart, counts, values, Position, Cut, buffer);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(values);
        }
    }

    // The disc of the ball in the section that lies `wFromGrazer` from the grazer's position in W, or null when the
    // section lies beyond the ball's reach.
    private static Disc? DiscAt(Metaball ball, Pose4 pose, double wFromGrazer)
    {
        var center = CenterFrom(ball, pose);
        var offset2 = (wFromGrazer - center[3]) * (wFromGrazer - center[3]);
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
