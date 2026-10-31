using System.Collections.Generic;
using System.Linq;
using Game.Interaction;
using Godot;

namespace Game.Regions;

/// <summary>
/// The bus's greybox body, built when it is ready: a single-deck city bus of about 2000, 11 m long, 2.5 m wide and 3 m
/// high, with a ring of windows, wheels, the route sign over the windscreen, and inside seats and grab rails. Its two
/// folding doors, at the front and in the middle, are on the right side, toward the sidewalk.
/// </summary>
public partial class Bus3D
{
    private const uint Paint = 0xe9e4d4ff, Stripe = 0xb35a4aff, Under = 0x3a3c3fff, Tire = 0x26272aff, Floor = 0x6f6a60ff,
        Seat = 0x5a7290ff, Rail = 0xc4c8ccff, Leaf = 0xd8d2c0ff, Sign = 0xf4f2eaff, Lamp = 0xf2e7b0ff;
    private const float Half = 1.25f, End = 5.5f, Skin = .06f, Deck = .9f, Ceiling = 2.75f;
    private const float FrontAxle = 2.9f, RearAxle = -2.7f, Wheel = .45f;
    // The doorways (z) in the right side: the front door ahead of the front wheels, the middle door between the axles.
    private static readonly (float, float)[] Doors = [(-.9f, .3f), (3.75f, 4.85f)];
    private static readonly (float, float)[] Arches = [(RearAxle - .6f, RearAxle + .6f), (FrontAxle - .6f, FrontAxle + .6f)];

    private readonly Dictionary<uint, StandardMaterial3D> materials = [];

    private void Build()
    {
        Box(this, -Half + Skin, Half - Skin, .35f, Deck - .04f, -End + Skin, End - Skin, Under);
        Box(this, -Half + Skin, Half - Skin, Deck - .04f, Deck, -End + Skin, End - Skin, Floor);
        Box(this, -Half, Half, Ceiling, 3, -End, End, Paint);  // roof
        foreach (var side in new[] { -1, 1 })
        {
            var (x0, x1) = side < 0 ? (-Half, -Half + Skin) : (Half - Skin, Half);
            (float, float)[] doors = side < 0 ? Doors : [];
            Strip(x0, x1, .3f, Deck, Arches, Paint);
            Strip(x0, x1, Deck, 1.05f, doors, Paint);
            Strip(x0, x1, 1.05f, 1.3f, doors, Stripe);
            Strip(x0, x1, 1.3f, 2.3f, doors, null);
            Box(this, x0, x1, 2.3f, Ceiling, -End + Skin, End - Skin, Paint);
            for (var z = -End + .15f; z < End - Skin; z += 1.35f)  // window pillars
                if (!doors.Any(d => z + .1f > d.Item1 && z < d.Item2)) Box(this, x0, x1, 1.3f, 2.3f, z, z + .1f, Paint);
            foreach (var axle in new[] { FrontAxle, RearAxle })
                AddChild(new MeshInstance3D
                {
                    Layers = 3, Position = new(side * (Half - .17f), Wheel, axle), Rotation = new(0, 0, Mathf.Pi / 2),
                    Mesh = new CylinderMesh { TopRadius = Wheel, BottomRadius = Wheel, Height = .28f, RadialSegments = 10, Rings = 0, Material = Material(Tire) }
                });
        }
        // The front: windscreen, the band with the route sign over it, bumper and lamps; and the back with its window.
        Box(this, -Half, Half, .3f, 1.25f, End - Skin, End, Paint);
        Box(this, -Half, Half, 1.25f, 2.35f, End - Skin, End, null);
        Box(this, -Half, Half, 2.35f, Ceiling, End - Skin, End, Under);
        Box(this, -.85f, .85f, 2.4f, 2.7f, End, End + .03f, Sign);
        AddChild(new Label3D
        {
            Text = "15路", FontSize = 96, PixelSize = .0025f, Modulate = new Color(Stripe), OutlineSize = 0, Layers = 3,
            Position = new(0, 2.55f, End + .04f)
        });
        Box(this, -Half, Half, .3f, .5f, End, End + .1f, Under);
        foreach (var x in new[] { -.95f, .95f }) Box(this, x - .15f, x + .15f, .65f, .8f, End, End + .02f, Lamp);
        Box(this, -Half, Half, .3f, 1.4f, -End, -End + Skin, Paint);
        Box(this, -Half + .3f, Half - .3f, 1.4f, 2.2f, -End, -End + Skin, null);
        Box(this, -Half, Half, 2.2f, Ceiling, -End, -End + Skin, Paint);
        Box(this, -Half, -Half + .3f, 1.4f, 2.2f, -End, -End + Skin, Paint);
        Box(this, Half - .3f, Half, 1.4f, 2.2f, -End, -End + Skin, Paint);
        Box(this, -Half, Half, .3f, .5f, -End - .1f, -End, Under);
        Inside();
        foreach (var (z0, z1) in Doors) FoldingDoor(z0, z1);
    }

    // Seats facing forward in pairs on the left and singly on the right between the doors, the driver's seat and dashboard,
    // grab rails along the ceiling and poles by the aisle.
    private void Inside()
    {
        const float Cushion = Deck + .42f;
        void Chair(float x0, float x1, float z)
        {
            Box(this, x0, x1, Cushion - .08f, Cushion, z, z + .45f, Seat);
            Box(this, x0, x1, Cushion, Cushion + .6f, z, z + .06f, Seat);
            Box(this, (x0 + x1) / 2 - .03f, (x0 + x1) / 2 + .03f, Deck, Cushion - .08f, z + .2f, z + .26f, Rail);
        }
        for (var z = -4.7f; z < 3.4f; z += .8f)
        {
            Chair(.2f, Half - Skin, z);
            if (!Doors.Any(d => z + .55f > d.Item1 && z - .3f < d.Item2)) Chair(-Half + Skin, -.4f, z);
        }
        Chair(.3f, .95f, 4.1f);  // the driver's
        Box(this, -Half + Skin, Half - Skin, Deck, 1.3f, 5.1f, End - Skin, Under);
        Box(this, .45f, .8f, 1.3f, 1.62f, 4.82f, 4.88f, Under);  // steering wheel
        foreach (var x in new[] { -.45f, .45f }) Box(this, x - .02f, x + .02f, 2.48f, 2.52f, -End + .3f, End - .5f, Rail);
        foreach (var (x, z) in new[] { (-.35f, 3.55f), (-.35f, .45f), (-.35f, -1.05f), (-.35f, -3.6f), (.12f, 2), (.12f, -2) })
            Box(this, x - .02f, x + .02f, Deck, Ceiling, z - .02f, z + .02f, Rail);
        PassengerSpot = new Marker3D { Name = "PassengerSpot", Transform = new Transform3D(Basis.Identity, new(.9f, 2.02f, -1.25f)).LookingAt(new(-Half, 1.5f, -.2f)) };
        AddChild(PassengerSpot);
    }

    // Two halves, each a pair of leaves that fold inward to their jamb, and the doorway that E finds while they stand open.
    // The doorway reaches 0.5 m out past the side, over the sidewalk's unseen wall at x 84 to 84.1, which would otherwise
    // catch the crosshair first.
    private void FoldingDoor(float z0, float z1)
    {
        var width = (z1 - z0) / 4;
        foreach (var (jamb, along) in new[] { (z0, 1), (z1, -1) })
        {
            var first = new Node3D { Name = $"Leaf{2 * leaves.Count}", Position = new(-Half + .05f, 0, jamb) };
            var second = new Node3D { Name = $"Leaf{2 * leaves.Count + 1}", Position = new(0, 0, along * width) };
            AddChild(first);
            first.AddChild(second);
            foreach (var leaf in new[] { first, second })
            {
                var (a, b) = along > 0 ? (0f, width) : (-width, 0f);
                Box(leaf, -.02f, .02f, Deck + .02f, 1.55f, a + .01f, b - .01f, Leaf);
                Box(leaf, -.02f, .02f, 1.55f, 2.28f, a + .01f, b - .01f, null);
            }
            leaves.Add((first, second, along));
        }
        var doorway = new Interactable3D { Prompt = "上车", CollisionLayer = 0, CollisionMask = 0, Position = new(-Half - .2f, 1.6f, (z0 + z1) / 2) };
        doorway.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.6f, 1.4f, z1 - z0) } });
        doorway.Interacted += () => EmitSignal(SignalName.Boarding);
        AddChild(doorway);
        doorways.Add(doorway);
    }

    // A side's band from y0 to y1 between the front and the back, cut at the gaps (z); glass where m is null.
    private void Strip(float x0, float x1, float y0, float y1, (float, float)[] gaps, uint? m)
    {
        var z = -End + Skin;
        foreach (var (a, b) in gaps.Append((End - Skin, End - Skin))) { Box(this, x0, x1, y0, y1, z, a, m); z = b; }
    }

    private void Box(Node3D parent, float x0, float x1, float y0, float y1, float z0, float z1, uint? m) => parent.AddChild(new MeshInstance3D
    {
        Layers = 3, Position = new((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
        Mesh = new BoxMesh { Size = new(x1 - x0, y1 - y0, z1 - z0), Material = m is { } color ? Material(color) : glass }
    });

    private StandardMaterial3D Material(uint color)
    {
        if (!materials.TryGetValue(color, out var material)) materials.Add(color, material = new() { AlbedoColor = new Color(color) });
        return material;
    }

    private readonly StandardMaterial3D glass = new()
    {
        AlbedoColor = new Color(.55f, .63f, .7f, .3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };
}
