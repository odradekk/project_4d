namespace FourD;

// Four-dimensional oriented boxes are shared by occupancy, placement and support.
internal sealed record Box4(EntityId Entity, PartId Part, double[] Center, double[][] Axes, double[] Half)
{
    internal static Box4 From(StaticEntity entity, BoxPart part, Interval segment)
    {
        var r = entity.Pose.Rotation;
        double[] Transform(Vec3 p) => [r.X.X*p.X+r.Y.X*p.Y+r.Z.X*p.Z, r.X.Y*p.X+r.Y.Y*p.Y+r.Z.Y*p.Z,
            r.X.Z*p.X+r.Y.Z*p.Y+r.Z.Z*p.Z, r.X.W*p.X+r.Y.W*p.Y+r.Z.W*p.Z];
        var offset = Transform(part.Center);
        return new(entity.Id, part.Id,
            [entity.Pose.Position.X + offset[0], entity.Pose.Position.Y + offset[1], entity.Pose.Position.Z + offset[2], (segment.Min + segment.Max) / 2],
            [Transform(part.Rotation.X), Transform(part.Rotation.Y), Transform(part.Rotation.Z), [0, 0, 0, 1]],
            [part.HalfSize.X, part.HalfSize.Y, part.HalfSize.Z, (segment.Max - segment.Min) / 2]);
    }
    internal static Box4 From(TurnableDefinition entity, Pose4 pose, BoxPart part)
    {
        var r = pose.Rotation;
        double[] Transform(Vec3 p) => [r.X.X*p.X+r.Y.X*p.Y+r.Z.X*p.Z, r.X.Y*p.X+r.Y.Y*p.Y+r.Z.Y*p.Z,
            r.X.Z*p.X+r.Y.Z*p.Y+r.Z.Z*p.Z, r.X.W*p.X+r.Y.W*p.Y+r.Z.W*p.Z];
        var offset = Transform(part.Center); var origin = V(pose.Position);
        return new(entity.Id, part.Id, Enumerable.Range(0, 4).Select(i => origin[i] + offset[i]).ToArray(),
            [Transform(part.Rotation.X), Transform(part.Rotation.Y), Transform(part.Rotation.Z), V(r.W)],
            [part.HalfSize.X, part.HalfSize.Y, part.HalfSize.Z, World.Epsilon]);
    }
    internal static Box4 From(OccupancyBox box) => new(default, default,
        V(box.Pose.Position), [V(box.Pose.Rotation.X), V(box.Pose.Rotation.Y), V(box.Pose.Rotation.Z), V(box.Pose.Rotation.W)], V(box.HalfSize));
    internal static double[] V(Vec4 v) => [v.X, v.Y, v.Z, v.W];
    internal static Vec4 Vec(double[] v) => new(v[0], v[1], v[2], v[3]);
    internal static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
    internal static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2], a[3] - b[3]];
    internal double Radius(double[] axis) => Enumerable.Range(0, 4).Sum(i => Half[i] * Math.Abs(Dot(Axes[i], axis)));
    internal IEnumerable<(double[] Normal, double Limit)> Planes()
    {
        for (var i = 0; i < 4; i++)
            foreach (var sign in new[] { -1d, 1d })
            {
                var n = Axes[i].Select(x => sign * x).ToArray();
                yield return (n, Dot(n, Center) + Half[i]);
            }
    }
    internal static IEnumerable<double[]> SeparatingAxes(Box4 a, Box4 b)
    {
        var edges = a.Axes.Concat(b.Axes).ToArray();
        for (var i = 0; i < 6; i++) for (var j = i + 1; j < 7; j++) for (var k = j + 1; k < 8; k++)
                {
                    var x = edges[i]; var y = edges[j]; var z = edges[k];
                    double Det(int p, int q, int r) => x[p] * (y[q] * z[r] - y[r] * z[q]) - x[q] * (y[p] * z[r] - y[r] * z[p]) + x[r] * (y[p] * z[q] - y[q] * z[p]);
                    double[] n = [Det(1, 2, 3), -Det(0, 2, 3), Det(0, 1, 3), -Det(0, 1, 2)];
                    var length = Math.Sqrt(Dot(n, n));
                    if (length > 1e-10) yield return n.Select(v => v / length).ToArray();
                }
    }
    internal static bool Overlaps(Box4 a, Box4 b, double tolerance) =>
        SeparatingAxes(a, b).All(n => a.Radius(n) + b.Radius(n) - Math.Abs(Dot(n, Sub(a.Center, b.Center))) > tolerance);

    internal static double[]? IntersectionPoint(Box4 a, Box4 b)
    {
        var planes = a.Planes().Concat(b.Planes()).ToArray();
        var points = new List<double[]>();
        for (var i = 0; i < 13; i++) for (var j = i + 1; j < 14; j++) for (var k = j + 1; k < 15; k++) for (var l = k + 1; l < 16; l++)
                    {
                        var selected = new[] { planes[i], planes[j], planes[k], planes[l] };
                        var matrix = selected.Select(p => p.Normal.Concat([p.Limit]).ToArray()).ToArray();
                        var valid = true;
                        for (var c = 0; c < 4; c++)
                        {
                            var pivot = Enumerable.Range(c, 4 - c).MaxBy(row => Math.Abs(matrix[row][c]));
                            if (Math.Abs(matrix[pivot][c]) < 1e-10) { valid = false; break; }
                            (matrix[c], matrix[pivot]) = (matrix[pivot], matrix[c]);
                            var d = matrix[c][c]; for (var n = c; n < 5; n++) matrix[c][n] /= d;
                            for (var row = 0; row < 4; row++) if (row != c) { var factor = matrix[row][c]; for (var n = c; n < 5; n++) matrix[row][n] -= factor * matrix[c][n]; }
                        }
                        if (!valid) continue;
                        var p = matrix.Select(row => row[4]).ToArray();
                        if (planes.All(plane => Dot(plane.Normal, p) <= plane.Limit + 1e-9)) points.Add(p);
                    }
        return points.Count == 0 ? null : Enumerable.Range(0, 4).Select(i => points.Average(p => p[i])).ToArray();
    }
    internal bool Representable()
    {
        var corners = new double[16][];
        for (var mask = 0; mask < 16; mask++)
        {
            var v = Center.ToArray();
            for (var i = 0; i < 4; i++) for (var j = 0; j < 4; j++) v[j] += ((mask & (1 << i)) == 0 ? -1 : 1) * Half[i] * Axes[i][j];
            if (v.Any(x => !double.IsFinite(x))) return false;
            corners[mask] = v;
        }
        var rows = new double[4][];
        for (var i = 0; i < 4; i++)
        {
            var row = Enumerable.Range(0, 4).Select(j => corners[1 << i][j] / 2 - corners[0][j] / 2).ToArray();
            var scale = row.Max(Math.Abs); if (scale == 0) return false;
            rows[i] = row.Select(x => x / scale).ToArray();
        }
        for (var column = 0; column < 4; column++)
        {
            var pivot = Enumerable.Range(column, 4 - column).MaxBy(i => Math.Abs(rows[i][column]));
            if (Math.Abs(rows[pivot][column]) < 1e-14) return false;
            (rows[column], rows[pivot]) = (rows[pivot], rows[column]);
            for (var i = column + 1; i < 4; i++)
            {
                var factor = rows[i][column] / rows[column][column];
                for (var j = column; j < 4; j++) rows[i][j] -= factor * rows[column][j];
            }
        }
        return true;
    }
    internal double[] Closest(double[] point)
    {
        var delta = Sub(point, Center); var result = Center.ToArray();
        for (var i = 0; i < 4; i++)
        {
            var value = Math.Clamp(Dot(delta, Axes[i]), -Half[i], Half[i]);
            for (var j = 0; j < 4; j++) result[j] += value * Axes[i][j];
        }
        return result;
    }
}
