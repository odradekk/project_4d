namespace FourD;

// Four-dimensional oriented boxes are shared by occupancy, placement and support.
internal sealed record Box4(EntityId Entity, PartId Part, double[] Center, double[][] Axes, double[] Half)
{
    // Relative pivot below which the box is flat in double precision.
    private const double DegenerateVolume = 1e-14;

    // A solved vertex may lie this far outside a face plane; elimination error exceeds the rounding margin.
    private const double VertexSlack = 1e-9;

    internal static Box4 From(StaticEntity entity, BoxPart part, Interval segment)
    {
        var center = PartCenter(entity.Pose, part);
        center[3] = (segment.Min + segment.Max) / 2;
        return new(entity.Id, part.Id, center,
            [.. PartAxes(entity.Pose.Rotation, part), [0, 0, 0, 1]],
            [.. ToArray(part.HalfSize), (segment.Max - segment.Min) / 2]);
    }

    internal static Box4 From(TurnableDefinition entity, Pose4 pose, BoxPart part) =>
        new(entity.Id, part.Id, PartCenter(pose, part),
            [.. PartAxes(pose.Rotation, part), ToArray(pose.Rotation.W)],
            [.. ToArray(part.HalfSize), World.Epsilon]);

    internal static Box4[] From(TurnableDefinition entity, Pose4 pose) =>
        entity.Shape.Parts.Select(part => From(entity, pose, part)).ToArray();

    internal static Box4 From(OccupancyBox box) =>
        new(default, default, ToArray(box.Pose.Position), AxesOf(box.Pose.Rotation), ToArray(box.HalfSize));

    private static double[] PartCenter(Pose4 pose, BoxPart part)
    {
        var origin = ToArray(pose.Position);
        var offset = ToArray(World.Transform(pose.Rotation, part.Center));
        return Enumerable.Range(0, 4).Select(i => origin[i] + offset[i]).ToArray();
    }

    private static double[][] PartAxes(Rotation4 rotation, BoxPart part) =>
    [
        ToArray(World.Transform(rotation, part.Rotation.X)),
        ToArray(World.Transform(rotation, part.Rotation.Y)),
        ToArray(World.Transform(rotation, part.Rotation.Z))
    ];

    internal static double[] ToArray(Vec4 vector) => [vector.X, vector.Y, vector.Z, vector.W];
    internal static double[] ToArray(Vec3 vector) => [vector.X, vector.Y, vector.Z];
    internal static Vec4 ToVec4(double[] vector) => new(vector[0], vector[1], vector[2], vector[3]);
    internal static double[][] AxesOf(Rotation4 rotation) =>
        [ToArray(rotation.X), ToArray(rotation.Y), ToArray(rotation.Z), ToArray(rotation.W)];

    internal static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
    internal static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2], a[3] - b[3]];
    internal static double[] Scale(double[] vector, double factor) => vector.Select(value => factor * value).ToArray();

    // The three-dimensional dot and cross product ignore the fourth component.
    internal static double Dot3(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    internal static double[] Cross3(double[] a, double[] b) =>
        [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];

    internal double Radius(double[] axis) => Enumerable.Range(0, 4).Sum(i => Half[i] * Math.Abs(Dot(Axes[i], axis)));

    // Radius along a coordinate axis, without building the axis vector.
    internal double Extent(int coordinate) => Enumerable.Range(0, 4).Sum(i => Half[i] * Math.Abs(Axes[i][coordinate]));

    internal Interval WSpan
    {
        get
        {
            var extent = Extent(3);
            return new(Center[3] - extent, Center[3] + extent);
        }
    }

    // A section at w cuts the box inside its W range. At the very edge it cuts only a box with a single
    // W-facing axis, whose flat face then lies in the section.
    internal bool IsCutBy(double w)
    {
        var span = WSpan;
        if (w < span.Min || w > span.Max) return false;
        return (w != span.Min && w != span.Max) || Axes.Count(axis => axis[3] != 0) == 1;
    }

    // Corner `mask` of the box: bit i selects the positive side of axis i.
    internal double[] Corner(int mask)
    {
        var corner = Center.ToArray();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                corner[j] += ((mask & (1 << i)) == 0 ? -1 : 1) * Half[i] * Axes[i][j];
        return corner;
    }

    internal IEnumerable<(double[] Normal, double Limit)> Planes()
    {
        for (var i = 0; i < 4; i++)
            foreach (var sign in new[] { -1d, 1d })
            {
                var normal = Scale(Axes[i], sign);
                yield return (normal, Dot(normal, Center) + Half[i]);
            }
    }

    // Face normals plus the normals of every triple of edge directions; their projections decide whether two boxes overlap.
    internal static IEnumerable<double[]> SeparatingAxes(Box4 a, Box4 b)
    {
        var edges = a.Axes.Concat(b.Axes).ToArray();
        for (var i = 0; i < 6; i++)
            for (var j = i + 1; j < 7; j++)
                for (var k = j + 1; k < 8; k++)
                {
                    var normal = Cross(edges[i], edges[j], edges[k]);
                    var length = Math.Sqrt(Dot(normal, normal));
                    if (length > World.RoundingMargin) yield return normal.Select(value => value / length).ToArray();
                }
    }

    // The four-dimensional vector orthogonal to three vectors.
    private static double[] Cross(double[] x, double[] y, double[] z)
    {
        double Minor(int p, int q, int r) =>
            x[p] * (y[q] * z[r] - y[r] * z[q]) - x[q] * (y[p] * z[r] - y[r] * z[p]) + x[r] * (y[p] * z[q] - y[q] * z[p]);
        return [Minor(1, 2, 3), -Minor(0, 2, 3), Minor(0, 1, 3), -Minor(0, 1, 2)];
    }

    // How far the projections of two boxes onto an axis overlap; delta is the offset between their centers.
    private static double Depth(Box4 a, Box4 b, double[] axis, double[] delta) =>
        a.Radius(axis) + b.Radius(axis) - Math.Abs(Dot(axis, delta));

    // Whether the boxes embed deeper than the contact tolerance along every separating axis.
    internal static bool Overlaps(Box4 a, Box4 b)
    {
        var delta = Sub(a.Center, b.Center);
        return SeparatingAxes(a, b).All(axis => Depth(a, b, axis, delta) > World.ContactTolerance);
    }

    // Cheap rejection on the axis-aligned bounds: false when they are already apart by the contact tolerance.
    internal bool BoundsMayOverlap(Box4 other) =>
        Enumerable.Range(0, 4).All(i => Math.Abs(Center[i] - other.Center[i]) < Extent(i) + other.Extent(i) - World.ContactTolerance);

    // The contact where `moving` embeds into `obstacle` deeper than the contact tolerance, or null.
    // The normal points from the obstacle toward `moving`, and Section is left for the caller.
    internal static Contact? Touch(Box4 moving, Box4 obstacle)
    {
        if (!Overlaps(moving, obstacle) || IntersectionPoint(moving, obstacle) is not { } point) return null;
        var delta = Sub(moving.Center, obstacle.Center);
        var normal = SeparatingAxes(moving, obstacle).MinBy(axis => Depth(moving, obstacle, axis, delta))!;
        if (Dot(normal, delta) < 0) normal = Scale(normal, -1);
        return new(moving.Entity, moving.Part, obstacle.Entity, obstacle.Part, ToVec4(point), ToVec4(normal), null);
    }

    // The average of the vertices of the overlap, or null when the boxes do not intersect.
    internal static double[]? IntersectionPoint(Box4 a, Box4 b)
    {
        var planes = a.Planes().Concat(b.Planes()).ToArray();
        var vertices = new List<double[]>();
        for (var i = 0; i < 13; i++)
            for (var j = i + 1; j < 14; j++)
                for (var k = j + 1; k < 15; k++)
                    for (var l = k + 1; l < 16; l++)
                    {
                        var vertex = Solve(planes[i], planes[j], planes[k], planes[l]);
                        if (vertex is not null && planes.All(plane => Dot(plane.Normal, vertex) <= plane.Limit + VertexSlack))
                            vertices.Add(vertex);
                    }
        return vertices.Count == 0 ? null : Enumerable.Range(0, 4).Select(i => vertices.Average(vertex => vertex[i])).ToArray();
    }

    // The point where four planes meet, by Gauss-Jordan elimination with partial pivoting; null when they do not meet in one point.
    private static double[]? Solve(params (double[] Normal, double Limit)[] planes)
    {
        var matrix = planes.Select(plane => plane.Normal.Concat([plane.Limit]).ToArray()).ToArray();
        for (var column = 0; column < 4; column++)
        {
            var pivot = Enumerable.Range(column, 4 - column).MaxBy(row => Math.Abs(matrix[row][column]));
            if (Math.Abs(matrix[pivot][column]) < World.RoundingMargin) return null;
            (matrix[column], matrix[pivot]) = (matrix[pivot], matrix[column]);
            var divisor = matrix[column][column];
            for (var k = column; k < 5; k++) matrix[column][k] /= divisor;
            for (var row = 0; row < 4; row++)
            {
                if (row == column) continue;
                var factor = matrix[row][column];
                for (var k = column; k < 5; k++) matrix[row][k] -= factor * matrix[column][k];
            }
        }
        return matrix.Select(row => row[4]).ToArray();
    }

    // Whether all corners are finite and the edges still span four dimensions in double precision.
    internal bool Representable()
    {
        var corners = Enumerable.Range(0, 16).Select(Corner).ToArray();
        if (corners.Any(corner => corner.Any(value => !double.IsFinite(value)))) return false;
        var rows = new double[4][];
        for (var i = 0; i < 4; i++)
        {
            var edge = Enumerable.Range(0, 4).Select(j => corners[1 << i][j] / 2 - corners[0][j] / 2).ToArray();
            var scale = edge.Max(Math.Abs);
            if (scale == 0) return false;
            rows[i] = edge.Select(value => value / scale).ToArray();
        }
        for (var column = 0; column < 4; column++)
        {
            var pivot = Enumerable.Range(column, 4 - column).MaxBy(i => Math.Abs(rows[i][column]));
            if (Math.Abs(rows[pivot][column]) < DegenerateVolume) return false;
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
        var delta = Sub(point, Center);
        var closest = Center.ToArray();
        for (var i = 0; i < 4; i++)
        {
            var distance = Math.Clamp(Dot(delta, Axes[i]), -Half[i], Half[i]);
            for (var j = 0; j < 4; j++) closest[j] += distance * Axes[i][j];
        }
        return closest;
    }
}
