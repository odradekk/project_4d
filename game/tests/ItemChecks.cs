using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The ball and the nut on the item test region: what each section shows of them, the glowing outline, carrying the ball
/// through a block that only the second section has, the dots under it, a ball that cannot be taken through a seam, a nut
/// carried into a block that no section shows, and the errors in authoring them. Expected values are worked out by hand
/// from the radius of a section of a ball (the square root of R squared less d squared) and from the nut's ring.
/// </summary>
public partial class ItemChecks : Node
{
    internal const string Scene = "res://regions/item_test.tscn";

    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;
    private PlayerActions actions => play.Actions;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(null, Scene);
            CheckSectionsOfItems();
            await CheckBallCarriedAndDropped();
            await play.Load(r => AddBlock(r, "BlockII", new(-1.2f, 1f, .9f), new(.4f, .6f, .4f), .3), Scene);
            await CheckBallStoppedInOtherSection();
            await play.Load(null, Scene);
            await CheckBallStaysOutOfBackpack();
            await play.Load(r => AddBlock(r, "BlockBetween", new(1.1f, 1.9f, 2.4f), new(.1f, .6f, .6f), .1, .3), Scene);
            await CheckNutCarriedIntoBlock();
            await play.Unload();
            CheckDiagnostics();
            GD.Print("Item checks passed: sections, outline, ball carried and dropped, dots, not packed, carried nut, diagnostics.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // A block among the building's elements that exists from W = wMin to wMax.
    internal static void AddBlock(Region3D region, string id, Vector3 position, Vector3 size, double wMin, double wMax = 1)
    {
        var block = (BuildingElement3D)region.GetNode<BuildingElement3D>("Buildings/Home/FloorNorth").Duplicate();
        block.Name = id;
        block.EntityId = id;
        block.WMin = wMin;
        block.WMax = wMax;
        block.Position = position;
        block.GetNode<BoxPart3D>("Part").Mesh = new BoxMesh { Size = size };
        region.GetNode("Buildings/Home").AddChild(block);
    }

    private MeshInstance3D Dots(string section) => play.Region.GetNode<MeshInstance3D>($"Generated/{section}/Overlay/Shadows/ball");

    private void CheckSectionsOfItems()
    {
        // A ball of radius .25 whose center is .21 deep shows a ball of radius sqrt(.25^2 - .21^2) = .1356 in each section.
        foreach (var section in new[] { "Ⅰ", "Ⅱ" })
        {
            var size = play.SliceBounds(section, "ball").Size;
            Check(Mathf.Abs(size.X / 2 - .1356f) < .002f && Mathf.Abs(size.Y / 2 - .1356f) < .002f, $"the ball must show a .1356 m ball in {section}; its size is {size}");
            Check(play.Region.HasNode($"Generated/{section}/Dynamic/ball/Collision"), $"the ball must have a collision body in {section}");
            Check(play.Mask(section, "ball") != Colors.Black, $"the ball must glow in the lens of {section}");
        }
        Check(play.HasSlice("Ⅰ", "nut") && !play.HasSlice("Ⅱ", "nut"), "the nut shows its body in its section, and its ring in none");
        Check(play.Region.HasNode("Generated/Ⅰ/Dynamic/nut/Collision"), "the nut must have a collision body");
        Check(play.Mask("Ⅰ", "nut") != Colors.Black, "the nut must glow in the lens, since its ring reaches out of the section");
    }

    private async Task CheckBallCarriedAndDropped()
    {
        play.LookAt(new(-1.2f, .02f, 2.8f), new(-1.2f, .25f, 1.8f));
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("拿起"), $"aiming at the ball must offer picking it up; the hint is '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held == "ball", "E must pick up the ball");

        // Over the hole of the second section, the first section has floor under the ball and the second has none.
        player.Camera.Rotation = Vector3.Zero;
        await play.Walk(new(1.5f, 0, .6f));
        await play.Frames(1);
        Check(Mathf.Abs(Dots("Ⅰ").Position.X - 1.5f) < .02f && Mathf.Abs(Dots("Ⅰ").Position.Z) < .02f, $"the dots must follow the ball at once; they are at {Dots("Ⅰ").Position}");
        await play.Frames(10);
        var hand = new Vector3(1.5f, 1f, 0f);
        Near(play.Position("ball"), hand, "a carried ball must be where the hand is");
        Check(actions.GetNode("Carrier").GetChildCount() == 0, "the host physics must not check a held ball, which the engine checks in every section");
        Check(Dots("Ⅰ").Visible && !Dots("Ⅱ").Visible, "dots must lie under the ball in the first section and not over the hole in the second");
        Near(Dots("Ⅰ").Position, new(1.5f, .004f, 0), "the dots must lie on the floor under the ball");

        // Let go over the hole: the floor of the first section, which the ball's center reaches, holds it, and the second section's
        // ball hangs over the hole with no dots under it.
        Press(Key.E);
        await play.Frames(90);
        Check(play.State("ball").Section is null && Mathf.Abs(play.Position("ball").Y - .25f) < .005f, $"a ball let go over the hole must rest on the floor of the first section; y = {play.Position("ball").Y}");
        Check(play.HasSlice("Ⅱ", "ball") && Dots("Ⅰ").Visible && !Dots("Ⅱ").Visible, "a ball at rest over the hole shows in the second section, with dots in the first only");
        play.LookAt(new(1.5f, .02f, 1.2f), play.Position("ball"));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held == "ball", "the ball must be picked up again");
        player.Camera.Rotation = Vector3.Zero;

        await play.Walk(new(-1.2f, 0, 2.8f));
        await play.Frames(10);
        Check(Dots("Ⅰ").Visible && Dots("Ⅱ").Visible, "dots must lie in both sections where the floor is there");

        // Let go: the ball falls until the floor under its center holds it, hanging above the floor in each section.
        Press(Key.E);
        await play.Frames(90);
        Check(actions.Held is null, "E must let go of the ball");
        Check(Mathf.Abs(play.Position("ball").Y - .25f) < .005f, $"the ball must come to rest with its center .25 m up; y = {play.Position("ball").Y}");
        var hang = play.SliceBounds("Ⅰ", "ball").Position.Y;
        Check(Mathf.Abs(hang - .1144f) < .003f, $"the ball must hang .114 m above the floor in the first section; its lowest point is {hang}");

        // Turning a ball changes nothing, and it never counts as stood up, so there is nothing to lay down.
        var ball = play.Position("ball");
        play.LookAt(new(ball.X, .02f, ball.Z + 1f), ball);
        await play.Frames(2);
        var before = play.SliceBounds("Ⅰ", "ball");
        Press(Key.F);
        await play.Frames(TurnFrames);
        Check(play.State("ball").UprightOrigin is null && !actions.IsUpright("ball"), "a ball must not count as upright after F");
        Check(play.SliceBounds("Ⅰ", "ball").IsEqualApprox(before), "turning a ball must not change what it shows");
        Check(player.Hud.HintText.Contains("竖起") && !player.Hud.HintText.Contains("放倒"), $"a ball offers standing up again, never laying down; the hint is '{player.Hud.HintText}'");
    }

    private async Task CheckBallStoppedInOtherSection()
    {
        // The block exists from W = .3 on, so only the second section shows it. A ball of radius .25 centered at W = .21 is
        // sqrt(.25^2 - .09^2) = .2332 wide at W = .3, so carried toward the block it stops with its center .2332 m from the face
        // at z = 1.1, which is z = 1.3332; in the second section alone, its slice, .1356 wide, would stop at z = 1.2356.
        play.LookAt(new(-1.2f, .02f, 2.8f), new(-1.2f, .25f, 1.8f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        player.Camera.Rotation = Vector3.Zero;
        await play.Walk(new(-1.2f, 0, 1.9f));
        await play.Walk(new(-1.2f, 0, .4f));
        await play.Frames(3);
        var stopped = play.Position("ball").Z;
        Check(Mathf.Abs(stopped - 1.3332f) < .01f, $"a carried ball must stop where its cross-section at W = .3 meets the block; z = {stopped}");
        Check(play.Region.GetNode("Generated/Ⅰ/Overlay/Contacts").GetChildCount() > 0, "the lens must mark where the ball was stopped");
        Check(actions.GetNode<AudioStreamPlayer>("Scratch").Playing, "a stopped ball must scrape");
    }

    private async Task CheckBallStaysOutOfBackpack()
    {
        // A ball cannot be packed, so crossing the seam at x = -3, z = 1.5 with it in the hand is refused.
        play.LookAt(new(-1.2f, .02f, 2.8f), new(-1.2f, .25f, 1.8f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        await play.Walk(new(-3f, 0, 1.65f));
        player.Hud.ClearText();
        player.Rotation = Vector3.Zero;
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Button(MouseButton.Left, true);
        Button(MouseButton.Left, false);
        await play.Frames(3);
        Button(MouseButton.Right, false);
        Check(play.State("xiaoman").Section?.Value == "Ⅰ", "she must not cross with a ball in her hand");
        Check(!play.State("ball").InBackpack && actions.Held == "ball", "the ball must stay in her hand");
        Check(player.Hud.SubtitleText.Contains("带不过缝"), $"refusing to take the ball through a seam is explained; saw '{player.Hud.SubtitleText}'");
    }

    private async Task CheckNutCarriedIntoBlock()
    {
        // A block that exists from W = .1 to .3 is in no section. The ring of the nut rests at W = .2, so a nut lying in the hand
        // runs its ring into the block: the ring, 25 cm in tube radius .05, is .41 m ahead of the nut, and the block's face is
        // at x = 1.05, so the nut stops with its origin at x = 1.05 - .05 - .41 = .59.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held == "nut", "E must pick up the nut");
        player.Camera.Rotation = Vector3.Zero;
        // The hand is at chest height before the nut is pushed on, as the ring is then level with the block.
        await play.Frames(3);
        await play.Walk(new(1.0f, 0, 3f));
        await play.Frames(3);
        var x = play.Position("nut").X;
        Check(Mathf.Abs(x - .59f) < .01f, $"the nut must stop where its ring meets the block; x = {x}");
        Check(play.Region.GetNode("Generated/Ⅰ/Overlay/Contacts").GetChildCount() > 0, "the lens must mark the contact between the sections");
    }

    // Errors in the authoring of a ball, a nut and a device are reported against the node and property that hold them.
    private static void CheckDiagnostics()
    {
        var region = GD.Load<PackedScene>(Scene).Instantiate<Region3D>();
        Check(new SceneReader(region).Load() is LoadResult.Loaded, "the item test region must be valid");

        // A ring that rests .02 m from the first section is too close to it: it needs its tube, the layer and .02 m more.
        var nut = region.GetNode<HiddenRingItem3D>("Items/Nut");
        var ring = (RingResource)nut.Ring!.Duplicate();
        ring.Center = [.41, .87, 0, .02];
        nut.Ring = ring;
        // A nut that is turned and has a Rotation4 as well says two things about its rotation.
        nut.Rotation4 = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        nut.Rotation = new(0, .3f, 0);
        region.GetNode<SphereItem3D>("Items/Ball").Rotation4 = [1, 2, 3];
        // A face of a part of the cabinet that lists two vertices.
        var cabinet = region.GetNode<LoftDevice3D>("Devices/Cabinet");
        var segment = (LoftSegmentResource)cabinet.Segments[0].Duplicate();
        var key = (LoftKeyResource)segment.Keys[0].Duplicate();
        var part = (ConvexPartResource)key.Parts[0].Duplicate();
        var face = (ConvexFaceResource)part.Faces[0].Duplicate();
        face.Vertices = [0, 1];
        part.Faces[0] = face;
        key.Parts[0] = part;
        segment.Keys[0] = key;
        cabinet.Segments[0] = segment;
        region.GetNode<LoftDevice3D>("Devices/Window").Motion = new DeviceRotationResource { Pivot = [0, 0] };

        var errors = (new SceneReader(region).Load() as LoadResult.Invalid)?.Errors.Select(e => (e.Code, e.Source)).ToArray() ?? [];
        var report = string.Join(", ", errors);
        Check(errors.Contains((LoadErrorCode.HiddenStructureTooClose, new Source("Items/Nut", "Ring"))), $"a ring too close to a section must name the nut's Ring; got {report}");
        Check(errors.Any(e => e.Source == new Source("Items/Ball", "Rotation4")), $"a rotation of the wrong length must name Rotation4; got {report}");
        Check(errors.Any(e => e.Source == new Source("Items/Nut", "Rotation4")), $"a Rotation4 on a turned node must be refused; got {report}");
        Check(errors.Any(e => e.Code == LoadErrorCode.InvalidGeometry && e.Source == new Source("Devices/Cabinet", "Segments")), $"a bad face must name the Segments of the device; got {report}");
        Check(errors.Any(e => e.Source == new Source("Devices/Window", "Motion")), $"a pivot of the wrong length must name Motion; got {report}");
        region.Free();
    }
}
