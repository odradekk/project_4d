using System.Globalization;

namespace FourD;

// Computes the boundary edges of the union of the current box slices.
internal static class BoxUnionOutline
{
    // A face whose normal has less XYZ length than this faces along W.
    private const double WFacing = 1e-14;

    // A face of a sliced box: points p with Normal . p <= Limit are inside.
    private sealed record Plane(double[] Normal, double Limit);
    private sealed record Face(int Solid, Plane Plane);

    private static double Length(double[] vector) => Math.Sqrt(Box4.Dot3(vector, vector));
    private static double[] AddScaled(double[] a, double[] b, double scale) =>
        [a[0] + b[0] * scale, a[1] + b[1] * scale, a[2] + b[2] * scale];

    internal static IReadOnlyList<Polyline> At(IEnumerable<Box4> boxes, double w)
    {
        var solids = boxes.Select(box => CutPlanes(box, w)).OfType<Plane[]>().ToArray();
        var faces = solids.SelectMany((planes, index) => planes.Select(plane => new Face(index, plane))).ToArray();
        var edges = new Dictionary<string, Polyline>();
        for (var i = 0; i < faces.Length; i++)
            for (var j = i + 1; j < faces.Length; j++)
                foreach (var (start, end) in OutlineEdges(solids, faces[i], faces[j]))
                    edges.TryAdd(Key(start, end), new Polyline(Array.AsReadOnly(start.Concat(end).ToArray()), false));
        return Array.AsReadOnly(edges.OrderBy(edge => edge.Key, StringComparer.Ordinal).Select(edge => edge.Value).ToArray());
    }

    // The faces of the box's slice at w; null when the section misses the box.
    private static Plane[]? CutPlanes(Box4 box, double w)
    {
        if (!box.IsCutBy(w)) return null;
        var planes = new List<Plane>();
        foreach (var (normal, limit) in box.Planes())
        {
            double[] xyz = [normal[0], normal[1], normal[2]];
            var length = Length(xyz);
            var offset = limit - normal[3] * w;
            if (length <= WFacing)
            {
                // A face along W has no XYZ part: the slice lies inside it unless the section is beyond it.
                if (offset < -World.RoundingMargin) return null;
                continue;
            }
            planes.Add(new(Box4.Scale(xyz, 1 / length), offset / length));
        }
        return planes.ToArray();
    }

    // The pieces of the line where two faces meet that lie on the boundary of the union.
    private static IEnumerable<(double[] Start, double[] End)> OutlineEdges(Plane[][] solids, Face first, Face second)
    {
        var cross = Box4.Cross3(first.Plane.Normal, second.Plane.Normal);
        var length = Length(cross);
        if (length <= World.RoundingMargin) yield break;
        var direction = Box4.Scale(cross, 1 / length);
        var towardFirst = Box4.Scale(Box4.Cross3(second.Plane.Normal, cross), first.Plane.Limit);
        var point = Box4.Scale(
            AddScaled(towardFirst, Box4.Cross3(cross, first.Plane.Normal), second.Plane.Limit),
            1 / (length * length));
        if (Clip(solids[first.Solid], point, direction) is not { } firstRange
            || Clip(solids[second.Solid], point, direction) is not { } secondRange)
            yield break;
        var low = Math.Max(firstRange.Low, secondRange.Low);
        var high = Math.Min(firstRange.High, secondRange.High);
        if (high - low <= World.RoundingMargin) yield break;

        // Cut the shared line wherever another solid starts or ends along it.
        var cuts = new List<double> { low, high };
        foreach (var solid in solids)
        {
            if (Clip(solid, point, direction) is not { } range) continue;
            if (range.Low > low + World.RoundingMargin && range.Low < high - World.RoundingMargin) cuts.Add(range.Low);
            if (range.High > low + World.RoundingMargin && range.High < high - World.RoundingMargin) cuts.Add(range.High);
        }
        cuts.Sort();
        for (var k = 0; k + 1 < cuts.Count; k++)
        {
            var left = cuts[k];
            var right = cuts[k + 1];
            if (right - left <= World.RoundingMargin) continue;
            if (IsEdge(solids, AddScaled(point, direction, (left + right) / 2), direction))
                yield return (AddScaled(point, direction, left), AddScaled(point, direction, right));
        }
    }

    // The range of the line point + t * direction that lies inside the solid; null when it misses.
    private static (double Low, double High)? Clip(Plane[] solid, double[] point, double[] direction)
    {
        var low = double.NegativeInfinity;
        var high = double.PositiveInfinity;
        foreach (var plane in solid)
        {
            var denominator = Box4.Dot3(plane.Normal, direction);
            var distance = plane.Limit - Box4.Dot3(plane.Normal, point);
            if (Math.Abs(denominator) <= World.RoundingMargin)
            {
                if (distance < -World.RoundingMargin) return null;
                continue;
            }
            var t = distance / denominator;
            if (denominator > 0) high = Math.Min(high, t);
            else low = Math.Max(low, t);
            if (low > high + World.RoundingMargin) return null;
        }
        return (low, high);
    }

    // Whether the union has an edge at the point of the line: its surface bends there instead of being flat.
    private static bool IsEdge(Plane[][] solids, double[] point, double[] direction)
    {
        var active = new List<Plane[]>();
        foreach (var solid in solids)
        {
            if (solid.Any(plane => Box4.Dot3(plane.Normal, point) > plane.Limit + World.RoundingMargin)) continue;
            var boundary = solid.Where(plane => Math.Abs(Box4.Dot3(plane.Normal, point) - plane.Limit) <= World.RoundingMargin).ToArray();
            if (boundary.Length == 0) return false;
            active.Add(boundary);
        }
        if (active.Count == 0) return false;

        var reference = Math.Abs(direction[0]) < .9 ? new double[] { 1, 0, 0 } : new double[] { 0, 1, 0 };
        var u = Box4.Cross3(direction, reference);
        u = Box4.Scale(u, 1 / Length(u));
        var v = Box4.Cross3(direction, u);
        var angles = TurningAngles(active.SelectMany(boundary => boundary), u, v);
        if (angles.Count < 2) return false;

        bool Covered(double angle)
        {
            var ray = AddScaled(Box4.Scale(u, Math.Cos(angle)), v, Math.Sin(angle));
            return active.Any(solid => solid.All(plane => Box4.Dot3(plane.Normal, ray) < 0));
        }
        var inside = new bool[angles.Count];
        for (var i = 0; i < angles.Count; i++)
        {
            var next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + 2 * Math.PI;
            inside[i] = Covered((angles[i] + next) / 2);
        }
        var transitions = new List<double>();
        for (var i = 0; i < angles.Count; i++)
            if (inside[(i + angles.Count - 1) % angles.Count] != inside[i]) transitions.Add(angles[i]);
        return transitions.Count > 2
            || (transitions.Count == 2 && Math.Abs(Math.Sin(transitions[0] - transitions[1])) > World.RoundingMargin);
    }

    // Each face changes between inside and outside only at its two tangent directions around the line.
    // Classifying the intervals between those directions also covers narrow wedges.
    private static List<double> TurningAngles(IEnumerable<Plane> planes, double[] u, double[] v)
    {
        var angles = new List<double>();
        foreach (var plane in planes)
        {
            var x = Box4.Dot3(plane.Normal, u);
            var y = Box4.Dot3(plane.Normal, v);
            if (x * x + y * y <= World.RoundingMargin * World.RoundingMargin) continue;
            var angle = Math.Atan2(y, x) + Math.PI / 2;
            for (var i = 0; i < 2; i++)
            {
                var turn = (angle + i * Math.PI) % (2 * Math.PI);
                if (turn < 0) turn += 2 * Math.PI;
                angles.Add(turn);
            }
        }
        angles.Sort();
        var unique = new List<double>();
        foreach (var angle in angles)
            if (unique.Count == 0 || angle - unique[^1] > World.RoundingMargin) unique.Add(angle);
        if (unique.Count > 1 && unique[0] + 2 * Math.PI - unique[^1] <= World.RoundingMargin) unique.RemoveAt(unique.Count - 1);
        return unique;
    }

    // The same text for an edge whichever end comes first, so an edge found twice is kept once.
    private static string Key(double[] a, double[] b)
    {
        string Point(double[] point) => string.Join(",",
            point.Select(value => Math.Round(value, 10)).Select(value => (value == 0 ? 0 : value).ToString("R", CultureInfo.InvariantCulture)));
        var first = Point(a);
        var second = Point(b);
        return string.CompareOrdinal(first, second) < 0 ? first + ";" + second : second + ";" + first;
    }
}
