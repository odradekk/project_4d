using System.Linq;
using Game.Adapter;
using Godot;

namespace Game.Regions;

/// <summary>
/// A spiral stair at an end of teaching building 2's atrium, from the greybox prototype's TB2_SPIRALS: a column, and one
/// turn of 18 treads up each storey, clockwise seen from above, railed except where the floors' corridors meet it. The node
/// stands at the column's foot on the lowest floor, with the corridors toward its −z, which is where each turn starts and
/// ends; a stair whose corridors are at the prototype's angle e is this node turned by −e about y. The corridor's edge runs
/// 1.7 m from the column, and the atrium's rail is open 1.1 m either side of the column's line, as in the prototype.
/// The treads are only to look at: the player has no step-up, so she walks on a spiral ramp of convex pieces instead. The
/// treads, the rail's posts, the column and the ramp are plain Godot nodes the engine does not know of, as the compound's
/// slopes are; what stops her is a fixed object of the section, <see cref="EntityId"/>, as every such thing must be. It is
/// all built when the node is ready, before the region reads its section, so it belongs to the section whose Props hold it.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class SpiralStair3D : Node3D
{
    internal const float Storey = 3.6f;
    private const int Steps = 18;
    internal const float Going = Mathf.Tau / Steps;  // one tread
    private const float Radius = 1.8f, Edge = 1.7f, Opening = 1.1f, Riser = Storey / Steps;
    // The ramp climbs from the foot of each turn's first riser to the front edge of its last tread, which is level with the
    // floor above; over that tread it is flat, so that where the stair looks level with a corridor it is.
    private const float Ramp = (Steps - 1) * Going, Wedge = Going / 2;
    // The column, and the keep-out round it, an octagon whose sides are Keep from the middle: she comes no nearer the middle
    // than 0.8 m, where the ramp is 37° steep, and stays off the innermost ring of ramp pieces.
    private const float Column = .15f, Keep = .6f;
    // Rings of ramp pieces, each about 1.19 times as wide round as the one inside it. A piece's top is two flat triangles that
    // slope as the helix does at the piece's inner and outer edge, so from 0.75 m out, where she can stand, no piece is
    // steeper than about 39°, under the 45° she can stand on. The innermost ring, up to 51° steep, only closes the gap to the
    // keep-out.
    private static readonly float[] Rings = [.5f, .. Enumerable.Range(0, 6).Select(i => .75f * Mathf.Pow(Radius / .75f, i / 5f))];
    private const float Depth = .3f;  // how far the ramp's and the rail's pieces reach under the surface
    // The rail's posts stand at RailIn, and what stops her runs from there out to RailOut.
    private const float RailIn = 1.75f, RailOut = 1.9f, RailHeight = 1.1f, Panel = .16f;
    private const float Headroom = 1.6f;  // under the lowest turn, where the ramp is lower than this, she is kept out
    private static readonly float RailFrom = Mathf.Asin(Opening / Radius);  // where the rail stops either side of the corridor
    private static readonly Color Concrete = new(0xd9d7d1ff), Iron = new(0x4a4d52ff), Pole = new(0x6d6f72ff), Wall = new(0xe2dccfff);

    private SurfaceTool concrete = null!, iron = null!;
    private StaticBody3D ramp = null!;
    private FixedObject3D blocks = null!;
    private PackedScene part = null!;
    private Material unseen = null!;

    /// <summary>The floors it joins, from the one it stands on; it turns once between each two.</summary>
    [Export] public int Floors { get; set; } = 2;

    /// <summary>
    /// The ID of the fixed object that stops her: the rail, the keep-out round the column, the closed low end under the lowest
    /// turn, and the panels above the top floor.
    /// </summary>
    [Export] public string EntityId { get; set; } = "spiral";

    /// <summary>A point <paramref name="r"/> from the column's axis, <paramref name="angle"/> clockwise from −z.</summary>
    internal static Vector3 At(float r, float angle, float y) => new(r * Mathf.Sin(angle), y, -r * Mathf.Cos(angle));

    /// <summary>How far the walking surface is above the floor a turn starts on, <paramref name="angle"/> into the turn.</summary>
    internal static float Surface(float angle) => Mathf.Min(angle, Ramp) / Ramp * Storey;

    public override void _Ready()
    {
        concrete = Tool(Concrete);
        iron = Tool(Iron);
        ramp = new StaticBody3D { Name = "Ramp", CollisionMask = 0 };
        blocks = new FixedObject3D { Name = "Blocks", EntityId = EntityId };
        part = GD.Load<PackedScene>("res://regions/box_part.tscn");
        unseen = new StandardMaterial3D { AlbedoColor = new(0, 0, 0, 0), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        for (var n = 0; n < Floors - 1; n++) Turn(n);
        for (var f = 0; f < Floors; f++)
            for (var k = 0; k < 8; k++)
                Block(At(Keep - .1f, k * Mathf.Tau / 8, (f + .5f) * Storey), new(2 * Keep * Mathf.Tan(Mathf.Pi / 8), Storey, .2f), -k * Mathf.Tau / 8, unseen);
        Top();
        var mesh = concrete.Commit();
        iron.Commit(mesh);
        AddChild(new MeshInstance3D { Name = "Stair", Mesh = mesh, Layers = 3 });
        var height = Floors * Storey;
        AddChild(new MeshInstance3D
        {
            Name = "Column", Layers = 3, Position = new(0, height / 2, 0),
            Mesh = new CylinderMesh
            {
                TopRadius = Column, BottomRadius = Column, Height = height, RadialSegments = 12, Rings = 0,
                Material = new StandardMaterial3D { AlbedoColor = Pole }
            }
        });
        AddChild(ramp);
        AddChild(blocks);
    }

    // The turn up from floor n: the prototype's treads and rail, the ramp, and the rail's block.
    private void Turn(int n)
    {
        var y = n * Storey;
        for (var i = 0; i < Steps; i++)
        {
            float a = (i + .5f) * Going, h = y + (i + 1) * Riser;
            Box(concrete, At(.98f, a, h - .04f), new(1.65f, .08f, .66f), Mathf.Pi / 2 - a);
            if (a < .6f || a > Mathf.Tau - .6f) continue;
            Box(iron, At(RailIn, a, h + .5f), new(.04f, 1, .04f), 0);
            Box(iron, At(RailIn, a, h + 1), new(.62f, .04f, .04f), -a);
        }
        for (var w = 0; w < 2 * Steps; w++)
        {
            float a0 = w * Wedge, a1 = a0 + Wedge, h0 = y + Surface(a0), h1 = y + Surface(a1);
            for (var k = 0; k + 1 < Rings.Length; k++)
            {
                Vector3[] top = [At(Rings[k], a0, h0), At(Rings[k + 1], a0, h0), At(Rings[k + 1], a1, h1), At(Rings[k], a1, h1)];
                ramp.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = [.. top, .. top.Select(p => p with { Y = p.Y - Depth })] } });
            }
        }
        // Upright boxes round the turn, each from under the ramp at its lower end to above it at its higher end, and long
        // enough at RailOut to meet the next. Under the lowest turn, where the ramp is too low to walk under, they reach down
        // into the ground floor, and a wall across the turn closes that end off from the rest of the space under it.
        const int pieces = 28;
        var span = (Mathf.Tau - 2 * RailFrom) / pieces;
        for (var j = 0; j < pieces; j++)
        {
            float a = RailFrom + (j + .5f) * span, h0 = y + Surface(a - span / 2) - Depth, h1 = y + Surface(a + span / 2) + RailHeight;
            if (n == 0 && h0 < Headroom)
            {
                h0 = -.1f;
                var end = Surface(a + span / 2) - Depth;
                if (end >= Headroom) Block(At((Rings[0] + RailIn) / 2, a + span / 2, (end - .1f) / 2), new(.1f, end + .1f, RailIn - Rings[0]), -(a + span / 2), unseen);
            }
            Block(At((RailIn + RailOut) / 2, a, (h0 + h1) / 2), new(2 * RailOut * Mathf.Tan(span / 2), h1 - h0, RailOut - RailIn), -a, unseen);
        }
    }

    // Above the top floor no turn goes on up: a panel as tall as the atrium's runs along the corridor's edge over the half of
    // the opening where the next turn would start, to the line through the column square to that edge, and on along that line
    // to the column, across the stairwell.
    private void Top()
    {
        var y = (Floors - 1) * Storey + RailHeight / 2;
        var wall = new StandardMaterial3D { AlbedoColor = Wall };
        Block(new(Opening / 2, y, -Edge), new(Opening, RailHeight, Panel), 0, wall);
        Block(new(Panel / 2, y, -(Column + Edge + Panel / 2) / 2), new(Panel, RailHeight, Edge + Panel / 2 - Column), 0, wall);
    }

    private void Block(Vector3 centre, Vector3 size, float yaw, Material material)
    {
        var box = part.Instantiate<BoxPart3D>();
        box.PartId = $"p{blocks.GetChildCount() + 1}";
        box.Mesh = new BoxMesh { Size = size, Material = material };
        box.Transform = new Transform3D(new Basis(Vector3.Up, yaw), centre);
        blocks.AddChild(box);
    }

    private static SurfaceTool Tool(Color color)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetMaterial(new StandardMaterial3D { AlbedoColor = color });
        return tool;
    }

    private static void Box(SurfaceTool tool, Vector3 centre, Vector3 size, float yaw) =>
        tool.AppendFrom(new BoxMesh { Size = size }, 0, new Transform3D(new Basis(Vector3.Up, yaw), centre));
}
