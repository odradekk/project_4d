namespace FourD;

internal enum FacetCap { None, Start, End }

// One facet of a piece: a convex three-dimensional cell of its boundary. A cap is the part's shape at one key. The
// others join the two keys' copies of one face. The normal is the facet's outward unit normal.
internal sealed record LoftFacet(int[] Vertices, (int First, int Second)[] Edges, FaceUv? Uv, double[] Normal, FacetCap Cap);

// The convex hull of one part's vertices at two consecutive keys of a loft segment, in the device's own coordinates.
// A face of the lower key and the same face of the upper key are parallel (validation sees to that), so the hull is
// exactly what the linear interpolation of the vertices sweeps out, and the points of a face stay in one hyperplane.
internal sealed class LoftPiece
{
    // Whether the next or the previous piece of the same segment starts or ends at this piece's cap, so that the
    // cap lies inside the loft.
    internal bool CoveredAtStart { get; }
    internal bool CoveredAtEnd { get; }

    internal PartId Part { get; }

    // The part's vertices at the lower key followed by those at the upper key, as x, y, z and W.
    internal double[][] Vertices { get; }

    internal LoftFacet[] Facets { get; }
    internal (int First, int Second)[] Edges { get; }

    // The distinct unit directions of the edges, from which the axes that separate this piece from other solids come.
    internal double[][] Directions { get; }

    internal LoftPiece(LoftKey lower, LoftKey upper, int index, bool coveredAtStart, bool coveredAtEnd)
    {
        var below = lower.Parts[index];
        var above = upper.Parts[index];
        var count = below.Vertices.Count;
        (Part, CoveredAtStart, CoveredAtEnd) = (below.Id, coveredAtStart, coveredAtEnd);
        Vertices =
        [
            .. below.Vertices.Select(vertex => new[] { vertex.X, vertex.Y, vertex.Z, lower.W }),
            .. above.Vertices.Select(vertex => new[] { vertex.X, vertex.Y, vertex.Z, upper.W })
        ];

        // Each edge of the part's shape is in two faces, running one way in each.
        var shapeEdges = below.Faces
            .SelectMany(face => Loop(face.Vertices))
            .Select(edge => (Math.Min(edge.First, edge.Second), Math.Max(edge.First, edge.Second)))
            .Distinct()
            .ToArray();
        (int First, int Second)[] Raised(IEnumerable<(int First, int Second)> edges) => [.. edges.Select(edge => (edge.First + count, edge.Second + count))];
        Edges = [.. shapeEdges, .. Raised(shapeEdges), .. Enumerable.Range(0, count).Select(i => (i, i + count))];

        var middle = Enumerable.Range(0, 4).Select(i => Vertices.Average(vertex => vertex[i])).ToArray();
        var facets = new List<LoftFacet>
        {
            new([.. Enumerable.Range(0, count)], shapeEdges, null, [0, 0, 0, -1], FacetCap.Start),
            new([.. Enumerable.Range(count, count)], Raised(shapeEdges), null, [0, 0, 0, 1], FacetCap.End)
        };
        foreach (var face in below.Faces)
        {
            var loop = Loop(face.Vertices).ToArray();
            int[] corners = [.. face.Vertices, .. face.Vertices.Select(vertex => vertex + count)];
            var edges = loop.Concat(Raised(loop)).Concat(face.Vertices.Select(vertex => (vertex, vertex + count))).ToArray();
            facets.Add(new(corners, edges, face.Uv, OutwardNormal(face.Vertices, count, middle), FacetCap.None));
        }
        Facets = [.. facets];
        Directions = [.. ConvexContact.DistinctDirections(Edges.Select(edge => Unit(Box4.Sub(Vertices[edge.Second], Vertices[edge.First]))))];
    }

    // The pieces of a loft: one for each part and each pair of consecutive keys of a segment.
    internal static LoftPiece[] Of(LoftShape shape)
    {
        var pieces = new List<LoftPiece>();
        foreach (var segment in shape.Segments)
            for (var key = 0; key + 1 < segment.Keys.Count; key++)
                for (var part = 0; part < segment.Keys[key].Parts.Count; part++)
                    pieces.Add(new(segment.Keys[key], segment.Keys[key + 1], part, key > 0, key + 2 < segment.Keys.Count));
        return [.. pieces];
    }

    private static IEnumerable<(int First, int Second)> Loop(IReadOnlyList<int> face) =>
        face.Select((vertex, i) => (vertex, face[(i + 1) % face.Count]));

    // The unit vector orthogonal to the hyperplane through three points of a face at one key and a point of the same
    // face at the other.
    private static double[] FacetNormal(double[] first, double[] second, double[] third, double[] across) =>
        Unit(Box4.Cross(Box4.Sub(second, first), Box4.Sub(third, first), Box4.Sub(across, first)));

    // The normal of the lateral facet of a face (given by its vertices at the lower key), pointing away from the middle.
    private double[] OutwardNormal(IReadOnlyList<int> face, int count, double[] middle)
    {
        var normal = FacetNormal(Vertices[face[0]], Vertices[face[1]], Vertices[face[2]], Vertices[face[0] + count]);
        return Box4.Dot(normal, Box4.Sub(middle, Vertices[face[0]])) > 0 ? Box4.Scale(normal, -1) : normal;
    }

    private static double[] Unit(double[] vector) => Box4.Scale(vector, 1 / Math.Sqrt(Box4.Dot(vector, vector)));
}
