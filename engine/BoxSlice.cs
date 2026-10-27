namespace FourD;

internal static class BoxSlice
{
    // Appends one part's unindexed triangles, with a PartId for each vertex.
    internal static void Append(Box4 box, BoxPart part, double w,
        List<double> positions, List<double> normals, List<double> uvs, List<PartId> parts)
    {
        if (box.Half.Any(h => h <= 0)) return;
        var radius = Enumerable.Range(0, 4).Sum(i => box.Half[i] * Math.Abs(box.Axes[i][3]));
        var min = box.Center[3] - radius;
        var max = box.Center[3] + radius;
        if (w < min || w > max) return;
        if ((w == min || w == max) && box.Axes.Count(a => a[3] != 0) != 1) return;

        var corners = new Point[16];
        for (var mask = 0; mask < 16; mask++)
        {
            var local = Enumerable.Range(0, 4).Select(i => ((mask & (1 << i)) == 0 ? -1 : 1) * box.Half[i]).ToArray();
            var world = box.Center.ToArray();
            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 4; j++) world[j] += local[i] * box.Axes[i][j];
            corners[mask] = new(local, world);
        }

        var emittedFaces = new List<List<Point>>();
        for (var axis = 0; axis < 4; axis++)
            foreach (var positive in new[] { false, true })
            {
                var sign = positive ? 1d : -1d;
                var normal = box.Axes[axis].Take(3).Select(v => sign * v).ToArray();
                var length = Math.Sqrt(Dot(normal, normal));
                if (length == 0) continue;
                for (var i = 0; i < 3; i++) normal[i] /= length;

                var points = new List<Point>();
                void Add(Point p)
                {
                    if (!points.Any(q => q.Local.SequenceEqual(p.Local))) points.Add(p);
                }
                for (var mask = 0; mask < 16; mask++)
                {
                    if (((mask & (1 << axis)) != 0) != positive) continue;
                    var a = corners[mask];
                    if (a.World[3] == w) Add(a);
                    for (var edge = 0; edge < 4; edge++)
                    {
                        if (edge == axis || (mask & (1 << edge)) != 0) continue;
                        var b = corners[mask | (1 << edge)];
                        if ((a.World[3] < w && b.World[3] > w) || (b.World[3] < w && a.World[3] > w))
                        {
                            var t = (w - a.World[3]) / (b.World[3] - a.World[3]);
                            var local = a.Local.ToArray();
                            local[edge] += t * (b.Local[edge] - a.Local[edge]);
                            var world = Enumerable.Range(0, 4).Select(i => a.World[i] + t * (b.World[i] - a.World[i])).ToArray();
                            Add(new(local, world));
                        }
                    }
                }
                if (points.Count < 3 || emittedFaces.Any(previous => previous.Count == points.Count &&
                    points.All(p => previous.Any(q => q.Local.SequenceEqual(p.Local))))) continue;
                emittedFaces.Add(points);
                var center = Enumerable.Range(0, 3).Select(i => points.Average(p => p.World[i])).ToArray();
                var reference = Math.Abs(normal[0]) < 0.9 ? new[] { 1d, 0, 0 } : new[] { 0d, 1, 0 };
                var u = Cross(reference, normal);
                var v = Cross(normal, u);
                points.Sort((a, b) => Angle(a).CompareTo(Angle(b)));
                double Angle(Point p)
                {
                    var d = Enumerable.Range(0, 3).Select(i => p.World[i] - center[i]).ToArray();
                    return Math.Atan2(Dot(d, v), Dot(d, u));
                }
                var face = axis < 3 ? part.FaceUvs.Single(f => (int)f.Axis == axis && f.Positive == positive) : null;
                for (var i = 1; i + 1 < points.Count; i++)
                {
                    var a = points[0]; var b = points[i]; var c = points[i + 1];
                    var ab = Enumerable.Range(0, 3).Select(j => b.World[j] - a.World[j]).ToArray();
                    var ac = Enumerable.Range(0, 3).Select(j => c.World[j] - a.World[j]).ToArray();
                    if (Dot(Cross(ab, ac), normal) <= 0) continue;
                    foreach (var p in new[] { a, b, c })
                    {
                        positions.AddRange(p.World.Take(3));
                        normals.AddRange(normal);
                        var (x, y) = axis switch
                        {
                            0 => (p.Local[2], p.Local[1]),
                            1 => (p.Local[0], p.Local[2]),
                            2 => (p.Local[0], p.Local[1]),
                            _ => (p.Local[0], p.Local[2])
                        };
                        if (face is not null)
                        {
                            x = (positive ? x : -x) * face.Scale.X + face.Offset.X;
                            y = y * face.Scale.Y + face.Offset.Y;
                        }
                        uvs.AddRange([x, y]);
                        parts.Add(part.Id);
                    }
                }
            }
    }

    private sealed record Point(double[] Local, double[] World);
    private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    private static double[] Cross(double[] a, double[] b) =>
        [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
}
