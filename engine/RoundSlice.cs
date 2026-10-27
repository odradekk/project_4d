namespace FourD;

// Slices of balls and rings, and the outline drawn for them.
internal static class RoundSlice
{
    // A ball's surface is cut into this many meridians and parallels, and a ring's into this many segments
    // along its circle and around its tube (docs/frame/contract-views.md).
    private const int Meridians = 64;
    private const int Parallels = 32;
    private const int CircleSegments = 64;
    private const int TubeSegments = 24;

    // A ring that does not lie in a section is meshed in cubes of this edge, with the same fixed split into six
    // tetrahedra as the metaballs, but no more than MaxCells along a side, so a large ring stays cheap to mesh.
    // Where the surface crosses an edge of a cube or of a square, the vertex is put on it by halving the edge
    // this many times.
    private const double CellSize = 0.02;
    private const int MaxCells = 64;
    private const int SurfaceSteps = 12;

    // A plane normal with at least this much of its length along Y is taken for vertical when a plane is spanned.
    private const double NearlyVertical = .9;

    internal static void Append(Round4 round, double w, SliceBuffer buffer)
    {
        var radius = ThickestRadius(round, w);
        if (radius == 0) return;
        if (round.Major == 0) Ball(round, radius, buffer);
        else if (LiesInSection(round)) FlatRing(round, radius, buffer);
        else TiltedRing(round, w, radius, buffer);
    }

    // Whether all of the circle is at one W, so that its slice is a torus.
    private static bool LiesInSection(Round4 ring) =>
        Math.Abs(ring.U[3]) <= World.RoundingMargin && Math.Abs(ring.V[3]) <= World.RoundingMargin;

    // The radius of the tube where it is cut at its thickest: the distance from the section to the circle
    // leaves this much of the tube radius. Zero when the section only touches the solid or misses it; rounding
    // can put a touching section a hair inside, which leaves no slice worth keeping.
    private static double ThickestRadius(Round4 round, double w)
    {
        var gap = round.GapTo(w);
        return gap >= round.Tube - World.RoundingMargin ? 0 : Math.Sqrt(round.Tube * round.Tube - gap * gap);
    }

    // How far a point of the section lies within the tube: positive inside the slice, negative outside.
    private static Func<double[], double> FieldAt(Round4 round, double w) =>
        point => round.Tube - round.CoreDistance(point[0], point[1], point[2], w);

    private static void Ball(Round4 ball, double radius, SliceBuffer buffer)
    {
        // The vertices of the surface; the last meridian is the first again.
        var points = new double[Parallels + 1, Meridians + 1][];
        for (var parallel = 0; parallel <= Parallels; parallel++)
            for (var meridian = 0; meridian <= Meridians; meridian++)
            {
                var latitude = Math.PI * (parallel / (double)Parallels - .5);
                var longitude = 2 * Math.PI * (meridian % Meridians) / Meridians;
                // The poles are exact, so the triangles that meet there have no area and are left out.
                var around = parallel == 0 || parallel == Parallels ? 0 : radius * Math.Cos(latitude);
                points[parallel, meridian] =
                [
                    ball.Center[0] + around * Math.Cos(longitude),
                    ball.Center[1] + radius * Math.Sin(latitude),
                    ball.Center[2] + around * Math.Sin(longitude)
                ];
            }

        void Triangle(double[] a, double[] b, double[] c) => buffer.AddFacingAwayFrom(ball.Part, a, b, c, ball.Center);
        for (var parallel = 0; parallel < Parallels; parallel++)
            for (var meridian = 0; meridian < Meridians; meridian++)
            {
                double[] a = points[parallel, meridian], b = points[parallel, meridian + 1];
                double[] c = points[parallel + 1, meridian + 1], d = points[parallel + 1, meridian];
                Triangle(a, b, c);
                Triangle(a, c, d);
            }
    }

    // A torus of the given tube radius around the circle, as a sweep along the circle and around the tube.
    private static void FlatRing(Round4 ring, double radius, SliceBuffer buffer)
    {
        var normal = Box4.Cross3(ring.U, ring.V);
        var points = new double[CircleSegments + 1, TubeSegments + 1][];
        for (var i = 0; i <= CircleSegments; i++)
            for (var j = 0; j <= TubeSegments; j++)
            {
                double angle = 2 * Math.PI * (i % CircleSegments) / CircleSegments, turn = 2 * Math.PI * (j % TubeSegments) / TubeSegments;
                // Out from the ring's axis, and along the normal of its plane.
                var distance = ring.Major + radius * Math.Cos(turn);
                var height = radius * Math.Sin(turn);
                points[i, j] = Enumerable.Range(0, 3)
                    .Select(k => ring.Center[k] + distance * (Math.Cos(angle) * ring.U[k] + Math.Sin(angle) * ring.V[k]) + height * normal[k])
                    .ToArray();
            }

        for (var i = 0; i < CircleSegments; i++)
        {
            var middle = ring.CorePoint(2 * Math.PI * (i + .5) / CircleSegments);
            for (var j = 0; j < TubeSegments; j++)
            {
                double[] a = points[i, j], b = points[i + 1, j], c = points[i + 1, j + 1], d = points[i, j + 1];
                buffer.AddFacingAwayFrom(ring.Part, a, b, c, middle);
                buffer.AddFacingAwayFrom(ring.Part, a, c, d, middle);
            }
        }
    }

    private static void TiltedRing(Round4 ring, double w, double radius, SliceBuffer buffer)
    {
        // The slice lies within the radius of the thickest part of the tube around the circle's projection.
        var half = Enumerable.Range(0, 3)
            .Select(i => ring.Major * Math.Sqrt(ring.U[i] * ring.U[i] + ring.V[i] * ring.V[i]) + radius)
            .ToArray();
        var cell = Math.Max(Math.Min(CellSize, radius / 2), 2 * half.Max() / MaxCells);
        var counts = half.Select(extent => (int)Math.Ceiling(2 * extent / cell) + 1).ToArray();
        var field = FieldAt(ring, w);

        // Node (x, y, z) of the grid has the index (x * counts[1] + y) * counts[2] + z.
        double[] Position(int node) =>
        [
            ring.Center[0] - half[0] + node / (counts[1] * counts[2]) * cell,
            ring.Center[1] - half[1] + node / counts[2] % counts[1] * cell,
            ring.Center[2] - half[2] + node % counts[2] * cell
        ];
        var values = Enumerable.Range(0, counts[0] * counts[1] * counts[2]).Select(node => field(Position(node))).ToArray();

        Marching.Surface(ring.Part, counts, values, Position, (inside, outside) => OnSurface(Position(inside), Position(outside), field), buffer);
    }

    // The point of the edge from a point inside to one outside where the field is zero, by halving the edge.
    private static double[] OnSurface(double[] inside, double[] outside, Func<double[], double> field)
    {
        double[] Along(double t) =>
        [
            inside[0] + t * (outside[0] - inside[0]),
            inside[1] + t * (outside[1] - inside[1]),
            inside[2] + t * (outside[2] - inside[2])
        ];
        double low = 0, high = 1;
        for (var step = 0; step < SurfaceSteps; step++)
        {
            var middle = (low + high) / 2;
            if (field(Along(middle)) > 0) low = middle;
            else high = middle;
        }
        return Along((low + high) / 2);
    }

    // The segments where the surface of the slice crosses the plane of the ring's circle, through the circle's
    // center, found on a grid of squares in that plane. A ball, which has no circle, is cut by the horizontal
    // plane through its center.
    internal static IReadOnlyList<Polyline> Outline(Round4 round, double w)
    {
        var radius = ThickestRadius(round, w);
        if (radius == 0) return [];
        var normal = PlaneNormal(round);
        // The first axis of the plane is horizontal unless the normal is nearly vertical, and the second completes them.
        var helper = Math.Abs(normal[1]) < NearlyVertical ? new double[] { 0, 1, 0 } : new double[] { 1, 0, 0 };
        var across = Box4.Cross3(normal, helper);
        var first = Box4.Scale(across, 1 / Math.Sqrt(Box4.Dot3(across, across)));
        var second = Box4.Cross3(normal, first);

        var half = round.Major + radius;
        var cell = Math.Max(Math.Min(CellSize, radius / 2), 2 * half / MaxCells);
        var count = (int)Math.Ceiling(2 * half / cell) + 1;
        var field = FieldAt(round, w);
        double[] Position(double i, double j)
        {
            double along = i * cell - half, aside = j * cell - half;
            return
            [
                round.Center[0] + along * first[0] + aside * second[0],
                round.Center[1] + along * first[1] + aside * second[1],
                round.Center[2] + along * first[2] + aside * second[2]
            ];
        }
        var values = new double[count, count];
        for (var i = 0; i < count; i++)
            for (var j = 0; j < count; j++)
                values[i, j] = field(Position(i, j));

        var segments = new List<Polyline>();
        for (var i = 0; i < count - 1; i++)
            for (var j = 0; j < count - 1; j++)
            {
                bool[] inside = [values[i, j] > 0, values[i + 1, j] > 0, values[i + 1, j + 1] > 0, values[i, j + 1] > 0];
                if (inside[0] == inside[1] && inside[1] == inside[2] && inside[2] == inside[3]) continue;
                // The corners of the square in order around it, and the point where the surface crosses the edge
                // that leaves each, if it does.
                (int I, int J)[] corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)];
                var crossings = Enumerable.Range(0, 4).Select(edge =>
                {
                    var next = (edge + 1) % 4;
                    if (inside[edge] == inside[next]) return null;
                    var (from, to) = inside[edge] ? (corners[edge], corners[next]) : (corners[next], corners[edge]);
                    return OnSurface(Position(from.I, from.J), Position(to.I, to.J), field);
                }).ToArray();
                var crossed = crossings.OfType<double[]>().ToArray();
                if (crossed.Length == 2) segments.Add(Segment(crossed[0], crossed[1]));
                else if (crossed.Length == 4)
                {
                    // The surface passes the square in two places: around the corners that lie on the other side
                    // of the surface from the middle of the square.
                    var middleInside = field(Position(i + .5, j + .5)) > 0;
                    for (var corner = 0; corner < 4; corner++)
                        if (inside[corner] != middleInside) segments.Add(Segment(crossings[(corner + 3) % 4]!, crossings[corner]!));
                }
            }
        return Array.AsReadOnly(segments.ToArray());

        static Polyline Segment(double[] start, double[] end) => new(Array.AsReadOnly([.. start, .. end]), false);
    }

    // The unit normal of the circle's plane as seen in XYZ, or up when that plane is seen edge-on or there is no circle.
    private static double[] PlaneNormal(Round4 round)
    {
        var normal = Box4.Cross3(round.U, round.V);
        var length = Math.Sqrt(Box4.Dot3(normal, normal));
        return round.Major == 0 || length <= World.RoundingMargin ? [0, 1, 0] : Box4.Scale(normal, 1 / length);
    }
}
