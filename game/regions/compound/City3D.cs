using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Regions;

/// <summary>
/// The city around the compound and the mountains north and south of it: scenery she sees and never reaches. It is built
/// when it is ready, from the greybox prototype's layout and fixed seed, so it is the same on every load; boxes are merged
/// into one surface per colour, with no collision, and the engine does not know of it. In metres as the compound's
/// scenes, with y from the ground; the node sits on the ground. Placed in a section's Props, it exists in that section only.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class City3D : Node3D
{
    private const uint Wall = 0xf0efeaff, Wall2 = 0xe2dccfff, Slab = 0xd9d7d1ff, Rail = 0x7a8088ff, Furn = 0x8fa3b8ff,
        Ground = 0xa7ad9fff, Asphalt = 0x55585cff, Line = 0xeeeeeaff, Walk = 0xb8b4abff, Bike = 0x6e7174ff,
        Green = 0x8a9a74ff, Sign = 0xc9a77cff, Shutter = 0x8c9196ff, Pole = 0x6d6f72ff, Ballast = 0x8e8a82ff,
        Sleeper = 0x7a6c5cff, Glass = 0x7f8b96ff, Trunk = 0x8a8478ff, Crown = 0x5d7a4aff;
    private static readonly Color Sky = new(0xd4d8ddff), Loess = new(0xa89a84ff);
    private const double X0 = -1300, X1 = 1350, Z0 = -500, Z1 = 700;  // along the valley past the fog, across it to the mountains' foot
    // These copy generate.py's layout, so change them with it: FX and FZ from FENCE_*, Cross from CROSS_ROAD, (57, 105)
    // from WALK, x 78 and 84 from SIDEWALK, x 91 from MAIN_ROAD; the node's y in compound.tscn is GROUND.
    private const double FX = 27, FZ = 92.5;  // the compound's centre
    // Carriageways (z) of the streets north and south of the compound and opposite the gate, and of the road to the gate.
    private static readonly (double, double) R2 = (-31, -22), R3 = (205, 214), E1 = (89, 98), Cross = (90, 97);
    private static readonly (double, double)[] WestCross = [(R2.Item1 - 3, R2.Item2 + 3), BayIn, BayOut, (R3.Item1 - 3, R3.Item2 + 3)],
        EastCross = [(E1.Item1 - 3, E1.Item2 + 3)];

    private readonly Dictionary<uint, List<Aabb>> boxes = [];
    private long seed = 2000;

    private double Rand() => (seed = seed * 16807 % 2147483647) / 2147483647.0;

    private void Box(double x0, double x1, double y0, double y1, double z0, double z1, uint m = Wall)
    {
        if (!boxes.TryGetValue(m, out var list)) boxes.Add(m, list = []);
        list.Add(new Aabb(new Vector3((float)x0, (float)y0, (float)z0), new Vector3((float)(x1 - x0), (float)(y1 - y0), (float)(z1 - z0))));
    }

    public override void _Ready()
    {
        Streets();
        Factory();
        Shops();
        // The farmers' market across the main road from the gate: an open shed with stall tables.
        Box(136, 186, 4.2, 4.4, 58, 86, Slab);
        for (double x = 136; x <= 186; x += 6.25) foreach (var z in new[] { 58, 85.75 }) Box(x - .12, x + .12, 0, 4.2, z, z + .25, Pole);
        foreach (var z in new[] { 62, 70, 78 }) for (double x = 139; x < 183; x += 11) Box(x, x + 9, 0, .8, z, z + 1.6, Furn);
        Block(136, 196, 104, 150, 24, true, "windows");  // the department store at the corner opposite the gate
        Box(150, 182, 24, 28, 103.6, 104, Sign);
        Blocks();
        AddChild(new MeshInstance3D { Name = "Boxes", Mesh = Merged(), Layers = 3 });
        foreach (var (x, z, h) in new[] { (-75.0, -125.0, 60.0), (-112, -150, 45) }) Cylinder(x, z, 0, h, 1.4, 2.8, 10);  // chimneys
        Cylinder(-25, -185, 0, 24, 1.2, 1.2, 8);  // the water tower
        Cylinder(-25, -185, 24, 30, 5, 5, 12);
        AddChild(new MeshInstance3D
        {
            Name = "Ground", Layers = 3, Position = new Vector3((float)FX, -.01f, (float)FZ),
            Mesh = new PlaneMesh { Size = new Vector2(8000, 8000), Material = Material(Ground) }
        });
        AddChild(new MeshInstance3D { Name = "Mountains", Mesh = Mountains(), Layers = 3 });
    }

    private void Streets()
    {
        // The main road: motor lanes, green belts, bicycle lanes and sidewalks, cut where streets cross; the
        // compound's site has the stretch of sidewalk she walks on, from z 57 to 105.
        Run(78, 84, .1, Walk, [WestCross[0], (57, 105), WestCross[^1]]);
        BusBay();
        Run(84, 89, .02, Bike, [R2, Cross, R3]);
        Run(89, 91, .15, Green, WestCross);
        Box(91, 106, 0, .02, Z0, Z1, Asphalt);
        Run(106, 108, .15, Green, EastCross);
        Run(108, 113, .02, Bike, [E1]);
        Run(113, 119, .1, Walk, EastCross);
        Box(98.4, 98.6, .02, .03, Z0, Z1, Line);
        for (var z = Z0; z < Z1; z += 6) foreach (var x in new[] { 94.75, 102.25 }) Box(x - .08, x + .08, .02, .03, z, z + 3, Line);
        foreach (var (z0, z1) in new[] { R2, R3 })
        {
            Box(X0, 91, 0, .02, z0, z1, Asphalt);
            Box(X0, 84, 0, .1, z0 - 3, z0, Walk);
            Box(X0, 84, 0, .1, z1, z1 + 3, Walk);
        }
        Box(106, X1, 0, .02, E1.Item1, E1.Item2, Asphalt);
        Box(108, X1, 0, .1, E1.Item1 - 3, E1.Item1, Walk);
        Box(108, X1, 0, .1, E1.Item2, E1.Item2 + 3, Walk);
        Box(-35, -29, 0, .02, R2.Item2 + 3, R3.Item1 - 3, Asphalt);  // the back alley along the west fence
        Box(X0, X1, 0, .02, Z0 - 12, Z0, Asphalt);  // roads along the mountains' foot
        Box(X0, X1, 0, .02, Z1, Z1 + 12, Asphalt);
        // Street lamps on the green belts, poplars between them, and utility poles along the side streets and the alley.
        static bool Near(double z, (double, double)[] gaps) => gaps.Any(g => z > g.Item1 - 2 && z < g.Item2 + 2);
        for (var z = Z0 + 10; z < Z1; z += 30)
        {
            if (!Near(z, WestCross)) Lamp(90, z, 1);
            if (!Near(z, EastCross)) Lamp(107, z, -1);
        }
        foreach (var (x, gaps) in new[] { (90.0, WestCross), (107, EastCross) })
            for (var z = Z0 + 5; z <= Z1; z += 10)
                if (!gaps.Any(g => z > g.Item1 - 3 && z < g.Item2 + 3))
                {
                    Box(x - .15, x + .15, 0, 5, z - .15, z + .15, Trunk);
                    Box(x - 1.2, x + 1.2, 4, 17, z - 1.2, z + 1.2, Crown);
                }
        PoleLine(true, R2.Item2 + 1.5, X0 + 20, 80);
        PoleLine(true, R3.Item1 - 1.5, X0 + 20, 80);
        PoleLine(false, -35.6, R2.Item2 + 3, R3.Item1 - 3);
        // The factory's railway spur on a low embankment west of the compound; it crosses the streets at grade.
        (double, double)[] railGaps = [WestCross[0], WestCross[^1]];
        Run(-52, -44, .3, Ballast, railGaps, -200);
        foreach (var x in new[] { -48.72, -47.28 }) Box(x - .04, x + .04, 0, .45, -200, Z1, Rail);
        for (double z = -200; z < Z1; z += .7)
            if (!railGaps.Any(g => z > g.Item1 && z < g.Item2)) Box(-49.3, -46.7, .28, .36, z, z + .22, Sleeper);
    }

    // A strip from x0 to x1 along z, h high, from `from` to the city's south edge, cut at the gaps.
    private void Run(double x0, double x1, double h, uint m, (double, double)[] gaps, double from = Z0)
    {
        var z = from;
        foreach (var (a, b) in gaps.Append((Z1, Z1))) { Box(x0, x1, 0, h, z, a, m); z = b; }
    }

    private void Lamp(double x, double z, int dir)
    {
        Box(x - .1, x + .1, 0, 9, z - .1, z + .1, Pole);
        Box(Math.Min(x, x + 2.2 * dir), Math.Max(x, x + 2.2 * dir), 8.8, 9, z - .06, z + .06, Pole);
    }

    // Puts a box from p to q along the run and from w0 to w1 across it, about the line at `at`.
    private void Along(bool alongX, double at, double p, double q, double y0, double y1, double w0, double w1, uint m)
    {
        if (alongX) Box(p, q, y0, y1, at + w0, at + w1, m); else Box(at + w0, at + w1, y0, y1, p, q, m);
    }

    private void PoleLine(bool alongX, double at, double a, double b)
    {
        for (var p = a; p <= b; p += 35)
        {
            Along(alongX, at, p - .14, p + .14, 0, 10, -.14, .14, Pole);
            Along(alongX, at, p - .06, p + .06, 9.2, 9.35, -1.1, 1.1, Pole);
            if (p + 35 <= b) foreach (var w in new[] { -.9, 0, .9 }) Along(alongX, at, p, p + 35, 9.33, 9.36, w - .015, w + .015, Pole);
        }
    }

    // The factory north of the compound: wall and gate on the north street, halls with skylights, the office, and the
    // central-heating pipes on their rack, along the wall, over the street and down behind the back alley.
    private void Factory()
    {
        void WallZ(double z, double a, double b, (double, double)[] gaps)
        {
            var x = a;
            foreach (var (g0, g1) in gaps.Append((b, b))) { Box(x, g0, 0, 2.6, z - .12, z + .12); x = g1; }
        }
        WallZ(-36, -165, -2, [(-90, -80), (-53, -43)]);
        WallZ(-205, -165, -2, [(-53, -43)]);
        foreach (var x in new[] { -165, -2 }) Box(x - .12, x + .12, 0, 2.6, -205, -36);
        foreach (var x in new[] { -91, -80 }) Box(x, x + 1, 0, 3.4, -36.5, -35.5);  // gate pillars
        Box(-99, -93, 0, 3.2, -44, -38);  // gatehouse
        foreach (var (x0, x1, z0, z1, h) in new[] { (-150.0, -100.0, -115.0, -60.0, 14.0), (-40, -8, -170, -60, 16), (-150, -62, -195, -135, 12) })
        {
            Box(x0, x1, 0, h, z0, z1);
            for (var x = x0 + 4; x < x1 - 3; x += 8) Box(x, x + 2.5, h, h + 1.8, z0 + 2, z1 - 2, Glass);
        }
        Box(-78, -56, 0, 13, -58, -45);  // the office facing the gate
        void Pipes(bool alongX, double at, double a, double b, double step = 8)
        {
            foreach (var w in new[] { -.4, .4 }) Along(alongX, at, a, b, 5, 5.5, w - .25, w + .25, Wall2);
            for (var p = a; p <= b; p += step)
            {
                Along(alongX, at, p - .15, p + .15, 0, 4.8, -.15, .15, Pole);
                Along(alongX, at, p - .1, p + .1, 4.8, 5, -.8, .8, Pole);
            }
        }
        Pipes(true, -38, -160, -8);
        Pipes(false, -8, -38, -16, 22);  // one span over the street
        Pipes(true, -16, -38, -8);
        Pipes(false, -38, -16, 200);
    }

    // Rows of two-storey buildings with shops on the ground floor, a few of them five storeys.
    private void Shops()
    {
        void Row(bool alongX, double c0, double c1, double a, double b, char face, double frontage = 16, double gap = 4)
        {
            for (var p = a; p + frontage <= b; p += frontage + gap)
                if (alongX) Shop(p, p + frontage, c0, c1, face); else Shop(c0, c1, p, p + frontage, face);
        }
        Row(true, -46, -34, 0, 78, 's', 14, 2);  // north of the north street
        Row(true, 217, 229, -26, 78, 'n', 14, 2);  // south of the south street
        Row(false, 64, 76, Z0, -48, 'e');  // west of the main road, beyond the side streets
        Row(false, 64, 76, 233, Z1, 'e');
        Row(false, 119, 131, Z0, E1.Item1 - 4, 'w');  // east of the main road
        Row(false, 119, 131, E1.Item2 + 4, Z1, 'w');
        Row(true, E1.Item1 - 15, E1.Item1 - 3, 190, X1, 's');  // both sides of the street opposite the gate
        Row(true, E1.Item2 + 3, E1.Item2 + 15, 200, X1, 'n');
    }

    // face: the street side, 'n' (−z), 's' (+z), 'w' (−x) or 'e' (+x)
    private void Shop(double x0, double x1, double z0, double z1, char face)
    {
        var upper = Rand() < .2 ? 4 : 1;
        double g = 3.9, h = g + upper * 3 + .4;
        Box(x0, x1, 0, h, z0, z1, Rand() < .5 ? Wall : Wall2);
        var alongX = face is 'n' or 's';
        var (a, b) = alongX ? (x0, x1) : (z0, z1);
        var front = face switch { 'n' => z0, 's' => z1, 'w' => x0, _ => x1 };
        var outward = face is 'n' or 'w' ? -1 : 1;
        void OnFront(double p, double q, double y0, double y1, double depth, uint m) =>
            Along(alongX, 0, p, q, y0, y1, outward < 0 ? front - depth : front, outward < 0 ? front : front + depth, m);
        OnFront(a, b, g - .1, g + .8, .25, Sign);  // the signboard band over the shops
        var bays = Math.Max(1, (int)Math.Floor((b - a) / 4 + .5));
        var w = (b - a) / bays;
        for (var i = 0; i < bays; i++)
        {
            var p = a + i * w;
            OnFront(p + .4, p + w - .4, 0, 3, .06, Shutter);
            for (var k = 0; k < upper; k++) OnFront(p + .9, p + w - .9, g + 3 * k + 1.3, g + 3 * k + 2.7, .06, Glass);
        }
        if (upper == 1 && Rand() < .3) OnFront(a + 2, b - 2, h, h + 3, .15, Sign);  // rooftop billboard
    }

    // Blocks of 3–7 storeys, and toward the city centre a few towers, on a grid around the streets; windows close by,
    // bands of windows farther off, plain boxes far away.
    private void Blocks()
    {
        (double, double, double, double)[] reserved =
            [(-40, 80, -36, 220), (60, 136, Z0, Z1), (-56, -36, Z0, Z1), (-168, 2, -208, -34), (X0, 80, -50, -16), (X0, 80, 199, 233), (133, X1, 56, 152)];
        bool Clear(double x0, double x1, double z0, double z1) =>
            !reserved.Any(r => x0 < r.Item2 + 4 && x1 > r.Item1 - 4 && z0 < r.Item4 + 4 && z1 > r.Item3 - 4);
        for (var x = X0; x < X1 - 30; x += 40)
            for (var z = Z0; z < Z1 - 12; z += 34)
            {
                double jx = x + (Rand() - .5) * 4, jz = z + (Rand() - .5) * 4;
                var dist = Math.Sqrt(Math.Pow(jx + 15 - FX, 2) + Math.Pow(jz + 6 - FZ, 2));
                var tower = dist > 250 && Rand() < .08;  // no tower within 250 m of the compound
                var (w, d) = tower ? (18, 18) : (30, 12);
                if (!Clear(jx, jx + w, jz, jz + d)) continue;
                var h = tower ? 45 + Math.Floor(Rand() * 10) * 3 : 9 + Math.Floor(Rand() * 5) * 3;
                Block(jx, jx + w, jz, jz + d, h, tower, dist < 160 ? "windows" : dist < 340 ? "bands" : null);
            }
    }

    private void Block(double x0, double x1, double z0, double z1, double h, bool allSides, string? detail)
    {
        Box(x0, x1, 0, h, z0, z1, Rand() < .5 ? Wall : Wall2);
        if (detail is null) return;
        var faces = new List<(bool AlongX, double At, int Out)> { (true, z0, -1), (true, z1, 1) };
        if (allSides) faces.AddRange([(false, x0, -1), (false, x1, 1)]);
        for (var y = 1.0; y + 1.4 < h; y += 3)
            foreach (var (alongX, at, outward) in faces)
            {
                var (a, b) = alongX ? (x0, x1) : (z0, z1);
                var n = Math.Max(1, (int)Math.Floor((b - a) / 3.3));
                var s = (b - a) / n;
                IEnumerable<(double, double)> spans = detail == "bands" ? [(a + .6, b - .6)] : Enumerable.Range(0, n).Select(i => (a + i * s + .9, a + (i + 1) * s - .9));
                foreach (var (p, q) in spans) Along(alongX, 0, p, q, y, y + 1.4, outward < 0 ? at - .06 : at, outward < 0 ? at : at + .06, Glass);
            }
    }

    private void Cylinder(double x, double z, double y0, double y1, double top, double bottom, int segments) => AddChild(new MeshInstance3D
    {
        Layers = 3, Position = new Vector3((float)x, (float)(y0 + y1) / 2, (float)z),
        Mesh = new CylinderMesh { TopRadius = (float)top, BottomRadius = (float)bottom, Height = (float)(y1 - y0), RadialSegments = segments, Rings = 0, Material = Material(Wall) }
    });

    private static StandardMaterial3D Material(uint color) => new() { AlbedoColor = new Color(color) };

    // Each face: its axis and side, the axis its quad runs along first and which way, and the axis it runs along next.
    private static readonly (int Axis, int Side, int U, int USide, int V)[] Faces =
        [(0, 1, 2, -1, 1), (0, -1, 2, 1, 1), (1, 1, 2, 1, 0), (1, -1, 0, 1, 2), (2, 1, 0, 1, 1), (2, -1, 0, -1, 1)];

    private ArrayMesh Merged()
    {
        var mesh = new ArrayMesh();
        foreach (var (color, list) in boxes)
        {
            var vertices = new Vector3[list.Count * 24];
            var normals = new Vector3[vertices.Length];
            var indices = new int[list.Count * 36];
            int v = 0, i = 0;
            foreach (var box in list)
                foreach (var (axis, side, u, uSide, next) in Faces)
                {
                    var normal = Vector3.Zero;
                    normal[axis] = side;
                    foreach (var (du, dv) in new[] { (0, 0), (1, 0), (1, 1), (0, 1) })
                    {
                        var corner = box.Position;
                        if (side > 0) corner[axis] += box.Size[axis];
                        if ((du == 1) == (uSide > 0)) corner[u] += box.Size[u];
                        if (dv == 1) corner[next] += box.Size[next];
                        normals[v] = normal;
                        vertices[v++] = corner;
                    }
                    // The quad runs counter-clockwise seen from outside; Godot's front faces are clockwise.
                    foreach (var k in new[] { 0, 2, 1, 0, 3, 2 }) indices[i++] = v - 4 + k;
                }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.Index] = indices;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, Material(color));
        }
        return mesh;
    }

    // Two ridges on each side of the east–west valley, about 1.2 km and 2.3 km out. They ignore the fog and are blended
    // toward the sky by hand, the far ridge more, so they stay visible as hazy ridges past the fogged city.
    private ArrayMesh Mountains()
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var sun = new Vector3(10, 35, 8).Normalized();
        foreach (var (side, dist, height, haze) in new[] { (-1, 1200.0, 520.0, .6f), (-1, 2300, 820, .78f), (1, 1250, 480, .6f), (1, 2400, 780, .78f) })
        {
            const double span = 8000, step = 160;
            var xs = Enumerable.Range(0, (int)(2 * span / step) + 1).Select(i => -span + i * step).ToArray();
            var crest = xs.Select(x => height * (1 - Math.Pow(x / span, 2))
                * (.6 + .25 * Math.Sin(x / 530 + side) + .1 * Math.Sin(x / 170 - side) + .15 * Rand())).ToArray();
            // the valley floor, the foothills and the crest
            var rows = new[] { (dist - 500, 0.0), (dist - 250, .35), (dist, 1) }.Select(row => xs.Select((x, i) =>
            {
                var px = FX + x + (Rand() - .5) * step * .4;
                return new Vector3((float)px, row.Item2 > 0 ? (float)(crest[i] * row.Item2) : -20, (float)(FZ + side * (row.Item1 + (Rand() - .5) * 120)));
            }).ToArray()).ToArray();
            void Triangle(Vector3 a, Vector3 b, Vector3 d)
            {
                var n = (b - a).Cross(d - a).Normalized();
                if (n.Z * side > 0) n = -n;  // facing the compound
                var c = Loess.SrgbToLinear() * (.75f + .25f * Math.Max(0, n.Dot(sun)));
                c = c.Lerp(Sky.SrgbToLinear(), haze);
                vertices.AddRange([a, b, d]);
                colors.AddRange([c, c, c]);
            }
            for (var r = 0; r < 2; r++)
                for (var i = 0; i + 1 < xs.Length; i++)
                {
                    Triangle(rows[r][i], rows[r][i + 1], rows[r + 1][i + 1]);
                    Triangle(rows[r][i], rows[r + 1][i + 1], rows[r + 1][i]);
                }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            DisableFog = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        return mesh;
    }
}
