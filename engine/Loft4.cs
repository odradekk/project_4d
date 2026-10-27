namespace FourD;

// A convex piece of a lofted device placed in the world. The center is the middle of its axis-aligned bounds, and the
// facet limits and extents are relative to it, so that moving the piece moves only the center. The corners are kept
// where they were placed, because a section may cut exactly through a key of the loft, and a corner rebuilt from the
// center and an offset can differ from it by a rounding error.
internal sealed record Loft4(
    EntityId Entity, PartId Part, double[] Center, LoftPiece Piece, double[][] Placed, double[] Home,
    double[] Extents, double[][] Normals, double[] Limits, double[][] Directions)
    : Solid4(Entity, Part, Center)
{
    internal static Loft4 At(EntityId entity, LoftPiece piece, Pose4 pose)
    {
        double[] Placed(double[] vertex) => Box4.ToArray(World.Add(pose.Position, World.Transform(pose.Rotation, Box4.ToVec4(vertex))));
        double[] Turned(double[] direction) => Box4.ToArray(World.Transform(pose.Rotation, Box4.ToVec4(direction)));

        var corners = piece.Vertices.Select(Placed).ToArray();
        var center = Enumerable.Range(0, 4).Select(i => (corners.Min(corner => corner[i]) + corners.Max(corner => corner[i])) / 2).ToArray();
        var offsets = corners.Select(corner => Box4.Sub(corner, center)).ToArray();
        var extents = Enumerable.Range(0, 4).Select(i => offsets.Max(offset => Math.Abs(offset[i]))).ToArray();
        var normals = piece.Facets.Select(facet => Turned(facet.Normal)).ToArray();
        // The limit of a facet is how far its plane lies from the center along its normal.
        var limits = piece.Facets.Select((facet, i) => facet.Vertices.Max(vertex => Box4.Dot(normals[i], offsets[vertex]))).ToArray();
        return new(entity, piece.Part, center, piece, corners, center, extents, normals, limits, piece.Directions.Select(Turned).ToArray());
    }

    internal double[][] Corners()
    {
        var shift = Enumerable.Range(0, 4).Select(i => Center[i] - Home[i]).ToArray();
        return Placed.Select(corner => corner.Select((value, i) => value + shift[i]).ToArray()).ToArray();
    }

    // Only an upper bound of the half extent along the axis, since the piece is not symmetric about its center. Nothing
    // asks it of a piece of a loft, which is never turned as an item is.
    internal override double Radius(double[] axis) => Corners().Max(corner => Math.Abs(Box4.Dot(Box4.Sub(corner, Center), axis)));

    internal override double Extent(int coordinate) => Extents[coordinate];

    internal override double Reach(double[] pivot, double[] axis) => Corners().Max(corner =>
    {
        var offset = Box4.Sub(corner, pivot);
        var along = Box4.Dot(offset, axis);
        return Math.Sqrt(along * along + offset[3] * offset[3]);
    });

    internal IEnumerable<(double[] Normal, double Limit)> Planes() =>
        Normals.Select((normal, i) => (normal, Box4.Dot(normal, Center) + Limits[i]));

    // How far beyond each facet's plane the point lies: negative inside the facet's half-space.
    private double[] Excess(double[] point)
    {
        var offset = Box4.Sub(point, Center);
        return Normals.Select((normal, i) => Box4.Dot(normal, offset) - Limits[i]).ToArray();
    }

    // Distance from a point to the piece, or minus the distance to its nearest facet for a point inside.
    internal double SignedDistance(double[] point)
    {
        var excess = Excess(point).Max();
        if (excess <= 0) return excess;
        var closest = ConvexContact.ClosestPoint(Corners(), point);
        return Math.Sqrt(Box4.Dot(Box4.Sub(point, closest), Box4.Sub(point, closest)));
    }

    // The nearest point of the piece's surface to `point`, and the outward unit normal of the surface there.
    internal (double[] Point, double[] Normal) Nearest(double[] point)
    {
        var excess = Excess(point);
        var face = Array.IndexOf(excess, excess.Max());
        if (excess[face] > 0)
        {
            var closest = ConvexContact.ClosestPoint(Corners(), point);
            var away = Box4.Sub(point, closest);
            var length = Math.Sqrt(Box4.Dot(away, away));
            return (closest, length > World.RoundingMargin ? Box4.Scale(away, 1 / length) : Normals[face]);
        }
        return (point.Select((value, i) => value - excess[face] * Normals[face][i]).ToArray(), Normals[face]);
    }
}
