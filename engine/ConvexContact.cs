namespace FourD;

// Contact between convex polytopes, boxes and pieces of lofts, through their vertices, edge directions and facets.
internal static class ConvexContact
{
    // Unit directions closer than this to parallel count as one.
    private const double Parallel = 1e-12;

    // The search for the nearest point of a polytope stops when the nearest point of the simplex found so far is
    // this close, in relative terms, to being the nearest of all the vertices, or after this many steps.
    private const double Converged = 1e-12;
    private const int MaxSteps = 64;

    // The nearest point of a simplex is at the origin to within this squared distance.
    private const double AtOrigin = 1e-24;

    private sealed record Hull(double[][] Vertices, double[][] Directions, (double[] Normal, double Limit)[] Planes);

    private static Hull Of(Solid4 solid) => solid switch
    {
        Box4 box => new(Enumerable.Range(0, 16).Select(box.Corner).ToArray(), box.Axes, box.Planes().ToArray()),
        Loft4 loft => new(loft.Corners(), loft.Directions, loft.Planes().ToArray()),
        _ => throw new ArgumentException("Not a convex polytope.")
    };

    // How deep `moving` embeds into `obstacle`: zero when they just touch, negative when they are apart.
    internal static double Depth(Solid4 moving, Solid4 obstacle) => Least(Of(moving), Of(obstacle)).Depth;

    // Where polytopes that touch or overlap meet: the middle of the region they share, and the unit normal from
    // `obstacle` toward `moving` along the axis of least depth. Null when they have no point in common.
    internal static Contact? ContactOf(Solid4 moving, Solid4 obstacle)
    {
        var (first, second) = (Of(moving), Of(obstacle));
        if (Box4.IntersectionPoint([.. first.Planes, .. second.Planes]) is not { } point) return null;
        var normal = Least(first, second).Normal;
        return new(moving.Entity, moving.Part, obstacle.Entity, obstacle.Part, Box4.ToVec4(point), Box4.ToVec4(normal), null);
    }

    // The least distance along an axis by which `moving` must be pushed to clear `obstacle`, and the direction of that
    // push. The axes are those orthogonal to three edge directions of the two polytopes: these include the normals of
    // all their facets, and with them the depth is the exact distance of least push.
    private static (double Depth, double[] Normal) Least(Hull moving, Hull obstacle)
    {
        var directions = DistinctDirections(moving.Directions.Concat(obstacle.Directions));
        var least = (Depth: double.PositiveInfinity, Normal: new double[4]);
        for (var i = 0; i < directions.Count; i++)
            for (var j = i + 1; j < directions.Count; j++)
                for (var k = j + 1; k < directions.Count; k++)
                {
                    var axis = Box4.Cross(directions[i], directions[j], directions[k]);
                    var length = Math.Sqrt(Box4.Dot(axis, axis));
                    if (length <= World.RoundingMargin) continue;
                    axis = Box4.Scale(axis, 1 / length);
                    var (movingLow, movingHigh) = Span(moving.Vertices, axis);
                    var (obstacleLow, obstacleHigh) = Span(obstacle.Vertices, axis);
                    var forward = obstacleHigh - movingLow;
                    var backward = movingHigh - obstacleLow;
                    if (Math.Min(forward, backward) < least.Depth)
                        least = (Math.Min(forward, backward), forward <= backward ? axis : Box4.Scale(axis, -1));
                }
        return least;
    }

    private static (double Low, double High) Span(double[][] vertices, double[] axis)
    {
        double low = double.PositiveInfinity, high = double.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            var along = Box4.Dot(vertex, axis);
            low = Math.Min(low, along);
            high = Math.Max(high, along);
        }
        return (low, high);
    }

    internal static List<double[]> DistinctDirections(IEnumerable<double[]> directions)
    {
        var distinct = new List<double[]>();
        foreach (var direction in directions)
            if (!distinct.Any(known => Math.Abs(Box4.Dot(known, direction)) > 1 - Parallel))
                distinct.Add(direction);
        return distinct;
    }

    // The point of the convex hull of the vertices nearest to `point` (the algorithm of Gilbert, Johnson and Keerthi):
    // it keeps a simplex of vertices whose nearest point to `point` it knows, and adds the vertex that lies furthest
    // toward `point` from that nearest point until no vertex does.
    internal static double[] ClosestPoint(double[][] vertices, double[] point)
    {
        var offsets = vertices.Select(vertex => Box4.Sub(vertex, point)).ToArray();
        var simplex = new List<double[]> { offsets[0] };
        var closest = offsets[0];
        for (var step = 0; step < MaxSteps; step++)
        {
            var squared = Box4.Dot(closest, closest);
            if (squared <= AtOrigin) break;
            var support = offsets.MinBy(vertex => Box4.Dot(vertex, closest))!;
            if (squared - Box4.Dot(closest, support) <= Converged * squared) break;
            simplex.Add(support);
            (closest, simplex) = NearestOnSimplex(simplex);
        }
        return closest.Select((value, i) => value + point[i]).ToArray();
    }

    // The point of the simplex nearest the origin, and the vertices of the face it lies in.
    private static (double[] Point, List<double[]> Support) NearestOnSimplex(List<double[]> vertices)
    {
        var best = (Point: new double[4], Support: vertices, Squared: double.PositiveInfinity);
        for (var mask = 1; mask < 1 << vertices.Count; mask++)
        {
            var face = vertices.Where((_, i) => (mask & (1 << i)) != 0).ToList();
            if (Weights(face) is not { } weights) continue;
            var point = Enumerable.Range(0, 4).Select(c => face.Select((vertex, i) => weights[i] * vertex[c]).Sum()).ToArray();
            var squared = Box4.Dot(point, point);
            if (squared < best.Squared) best = (point, face, squared);
        }
        return (best.Point, best.Support);
    }

    // The weights, none negative, with which the vertices give the point of their affine hull nearest the origin,
    // or null when there are none because the vertices are affinely dependent or that point lies outside the face.
    // They minimise the squared length of the weighted sum for weights that add up to 1, which is the linear system
    // of the Gram matrix of the vertices bordered by the constraint.
    private static double[]? Weights(List<double[]> vertices)
    {
        var size = vertices.Count + 1;
        var system = new double[size][];
        for (var row = 0; row < size; row++)
        {
            system[row] = new double[size + 1];
            for (var column = 0; column < size; column++)
                system[row][column] = row < vertices.Count && column < vertices.Count ? Box4.Dot(vertices[row], vertices[column])
                    : row == column ? 0 : 1;
        }
        system[size - 1][size] = 1;

        var scale = Math.Max(1, system.Max(row => row.Max(Math.Abs)));
        for (var column = 0; column < size; column++)
        {
            var pivot = Enumerable.Range(column, size - column).MaxBy(row => Math.Abs(system[row][column]));
            if (Math.Abs(system[pivot][column]) <= Converged * scale) return null;
            (system[column], system[pivot]) = (system[pivot], system[column]);
            for (var row = 0; row < size; row++)
            {
                if (row == column) continue;
                var factor = system[row][column] / system[column][column];
                for (var k = column; k <= size; k++) system[row][k] -= factor * system[column][k];
            }
        }
        var weights = Enumerable.Range(0, vertices.Count).Select(i => system[i][size] / system[i][i]).ToArray();
        return weights.All(weight => weight >= -Converged) ? weights : null;
    }
}
