namespace FourD;

internal static class BoxSlice
{
    // A corner of the box (or a point on one of its edges): its position in the box and in the world.
    private sealed record Point(double[] Local, double[] World);

    // Appends one part's unindexed triangles, with a PartId for each vertex.
    internal static void Append(Box4 box, BoxPart part, double w,
        List<double> positions, List<double> normals, List<double> uvs, List<PartId> parts)
    {
        if (box.Half.Any(half => half <= 0) || !box.IsCutBy(w)) return;
        var corners = Enumerable.Range(0, 16).Select(mask => new Point(LocalCorner(box, mask), box.Corner(mask))).ToArray();
        var emitted = new List<List<Point>>();
        for (var axis = 0; axis < 4; axis++)
            foreach (var positive in new[] { false, true })
            {
                var normal = FaceNormal(box, axis, positive);
                if (normal is null) continue;
                var points = FacePoints(corners, axis, positive, w);
                if (points.Count < 3 || emitted.Any(previous => SameCorners(previous, points))) continue;
                emitted.Add(points);
                SortAroundCenter(points, normal);
                var face = axis < 3 ? part.FaceUvs.Single(uv => (int)uv.Axis == axis && uv.Positive == positive) : null;
                foreach (var vertex in FanVertices(points, normal))
                {
                    positions.AddRange(vertex.World.Take(3));
                    normals.AddRange(normal);
                    uvs.AddRange(Uv(vertex.Local, axis, positive, face));
                    parts.Add(part.Id);
                }
            }
    }

    private static double[] LocalCorner(Box4 box, int mask) =>
        Enumerable.Range(0, 4).Select(i => ((mask & (1 << i)) == 0 ? -1 : 1) * box.Half[i]).ToArray();

    // The outward unit normal of a face projected onto XYZ; null when the face has no XYZ part.
    private static double[]? FaceNormal(Box4 box, int axis, bool positive)
    {
        var sign = positive ? 1d : -1d;
        var normal = box.Axes[axis].Take(3).Select(value => sign * value).ToArray();
        var length = Math.Sqrt(Box4.Dot3(normal, normal));
        if (length == 0) return null;
        for (var i = 0; i < 3; i++) normal[i] /= length;
        return normal;
    }

    // The face's corners that lie in the section, and the points where its edges cross it.
    private static List<Point> FacePoints(Point[] corners, int axis, bool positive, double w)
    {
        var points = new List<Point>();
        void Add(Point point)
        {
            if (!points.Any(existing => existing.Local.SequenceEqual(point.Local))) points.Add(point);
        }
        for (var mask = 0; mask < 16; mask++)
        {
            if (((mask & (1 << axis)) != 0) != positive) continue;
            var start = corners[mask];
            if (start.World[3] == w) Add(start);
            for (var edge = 0; edge < 4; edge++)
            {
                if (edge == axis || (mask & (1 << edge)) != 0) continue;
                var end = corners[mask | (1 << edge)];
                if ((start.World[3] < w && end.World[3] > w) || (end.World[3] < w && start.World[3] > w))
                    Add(Crossing(start, end, edge, w));
            }
        }
        return points;
    }

    // Where the edge along `edge` from `start` to `end` crosses the section.
    private static Point Crossing(Point start, Point end, int edge, double w)
    {
        var t = (w - start.World[3]) / (end.World[3] - start.World[3]);
        var local = start.Local.ToArray();
        local[edge] += t * (end.Local[edge] - start.Local[edge]);
        var world = Enumerable.Range(0, 4).Select(i => start.World[i] + t * (end.World[i] - start.World[i])).ToArray();
        return new(local, world);
    }

    // Whether two faces produced the same polygon.
    private static bool SameCorners(List<Point> first, List<Point> second) =>
        first.Count == second.Count && second.All(point => first.Any(other => other.Local.SequenceEqual(point.Local)));

    // Orders the points counterclockwise around their center, seen from outside the face.
    private static void SortAroundCenter(List<Point> points, double[] normal)
    {
        var center = Enumerable.Range(0, 3).Select(i => points.Average(point => point.World[i])).ToArray();
        var reference = Math.Abs(normal[0]) < 0.9 ? new[] { 1d, 0, 0 } : new[] { 0d, 1, 0 };
        var u = Box4.Cross3(reference, normal);
        var v = Box4.Cross3(normal, u);
        double Angle(Point point)
        {
            var offset = Enumerable.Range(0, 3).Select(i => point.World[i] - center[i]).ToArray();
            return Math.Atan2(Box4.Dot3(offset, v), Box4.Dot3(offset, u));
        }
        points.Sort((first, second) => Angle(first).CompareTo(Angle(second)));
    }

    // The vertices of the polygon as a triangle fan, leaving out triangles that do not face outward.
    private static IEnumerable<Point> FanVertices(List<Point> points, double[] normal)
    {
        for (var i = 1; i + 1 < points.Count; i++)
        {
            Point[] triangle = [points[0], points[i], points[i + 1]];
            var first = Enumerable.Range(0, 3).Select(j => triangle[1].World[j] - triangle[0].World[j]).ToArray();
            var second = Enumerable.Range(0, 3).Select(j => triangle[2].World[j] - triangle[0].World[j]).ToArray();
            if (Box4.Dot3(Box4.Cross3(first, second), normal) <= 0) continue;
            foreach (var vertex in triangle) yield return vertex;
        }
    }

    // Texture coordinates from a point's position in the box, through the face's UV definition.
    private static double[] Uv(double[] local, int axis, bool positive, FaceUv? face)
    {
        var (x, y) = axis switch
        {
            0 => (local[2], local[1]),
            1 => (local[0], local[2]),
            2 => (local[0], local[1]),
            _ => (local[0], local[2])
        };
        if (face is not null)
        {
            x = (positive ? x : -x) * face.Scale.X + face.Offset.X;
            y = y * face.Scale.Y + face.Offset.Y;
        }
        return [x, y];
    }
}
