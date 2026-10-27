using System;
using System.Collections.Generic;
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
/// through a block that only the second section has, the dots under it, the dashed ring a nut promises before it is stood up,
/// the ring that follows a nut, and a nut carried into a block that no section shows. Expected values are worked out by hand
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
            await play.Load(null, Scene);
            await CheckHiddenRingPreview();
            await CheckRingFollowsNut();
            await play.Load(r => r.GetNode<HiddenRingItem3D>("Items/Nut").Position = new(-1.5f, .03f, -3.85f), Scene);
            await CheckBlockedRingPreview();
            await play.Load(r => AddBlock(r, "BlockBetween", new(1.1f, 1.9f, 2.4f), new(.1f, .6f, .6f), .1, .3), Scene);
            await CheckNutCarriedIntoBlock();
            await play.Unload();
            CheckDashes();
            CheckDiagnostics();
            GD.Print("Item checks passed: sections, outline, ball carried and dropped, dots, not packed, ring preview, ring follows, blocked ring, carried nut, dashes, diagnostics.");
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
        await play.Frames(10);
        var hand = new Vector3(1.5f, 1f, 0f);
        Near(play.Position("ball"), hand, "a carried ball must be where the hand is");
        Check(actions.GetNode("Carrier").GetChildCount() == 0, "the host physics must not check a held ball, which the engine checks in every section");
        Check(Dots("Ⅰ").Visible && !Dots("Ⅱ").Visible, "dots must lie under the ball in the first section and not over the hole in the second");
        Near(Dots("Ⅰ").Position, new(1.5f, .004f, 0), "the dots must lie on the floor under the ball");

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

    // The path of the lines drawn in the lens of a section, of a color.
    private IEnumerable<Vector3> PreviewLines(string section, Color color)
    {
        var preview = play.Region.GetNodeOrNull<MeshInstance3D>($"Generated/{section}/Overlay/Preview");
        for (var i = 0; preview?.Mesh is not null && i < preview.Mesh.GetSurfaceCount(); i++)
            if (((StandardMaterial3D)preview.Mesh.SurfaceGetMaterial(i)).AlbedoColor == color)
                foreach (var vertex in preview.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()) yield return vertex;
    }

    private async Task CheckHiddenRingPreview()
    {
        // Turning a quarter about the hinge at x = .49 takes the ring's center, .42 ahead and .2 deep, to W = .42 and .2 behind the
        // hinge, so it stands at x = .29, y = .03 + .87 = .9 and z = 2.2, 50 cm across: the lens shows where, before the nut is stood up.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Check(play.Mask("Ⅰ", "nut") != Colors.Black, "the nut glows in the lens");
        Button(MouseButton.Right, true);
        await play.Frames(20);
        var lines = PreviewLines("Ⅱ", LensOverlay.Clear).ToArray();
        Check(lines.Length > 0 && !PreviewLines("Ⅱ", LensOverlay.Blocked).Any(), "a clear stand-up draws pale lines and no red");
        var ring = lines.Where(v => v.Y > .6f).ToArray();
        var top = ring.Max(v => v.Y);
        Check(ring.Length > 0 && Mathf.Abs(top - 1.15f) < .01f, $"the dashed ring must reach y = 1.15; its top is {top}");
        Check(Mathf.Abs(ring.Average(v => v.Z) - 2.2f) < .03f, $"the dashed ring must be centered at z = 2.2; it is at {ring.Average(v => v.Z)}");

        // Nothing changes while the nut and the player stay as they are, so the drawing is not made again.
        var drawn = play.Region.GetNode<MeshInstance3D>("Generated/Ⅱ/Overlay/Preview").GetInstanceId();
        await play.Frames(60);
        Check(play.Region.GetNode<MeshInstance3D>("Generated/Ⅱ/Overlay/Preview").GetInstanceId() == drawn, "an unchanged preview must be kept");

        Press(Key.F);
        await play.Frames(TurnFrames);
        Button(MouseButton.Right, false);
        Check(play.State("nut").UprightOrigin?.Value == "Ⅰ", "F must stand the nut up");
        var bounds = play.SliceBounds("Ⅱ", "nut");
        Near(bounds.GetCenter(), new(.29f, .9f, 2.2f), "the ring must stand where the preview showed");
        Check(Mathf.Abs(bounds.Size.Y - .5f) < .01f && Mathf.Abs(bounds.Size.Z - .5f) < .01f, $"the ring must be 50 cm across; its size is {bounds.Size}");
    }

    private async Task CheckRingFollowsNut()
    {
        // The standing nut is a slice in the first section; grip it and move it half a metre, and the ring moves with it.
        var slice = play.SliceBounds("Ⅰ", "nut").GetCenter();
        play.LookAt(new(slice.X, .02f, slice.Z + .8f), slice);
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held == "nut" && actions.HeldIsUpright, "E must grip the standing nut");
        var before = play.SliceBounds("Ⅱ", "nut").GetCenter();
        await play.Walk(player.Position + new Vector3(.5f, 0, 0));
        await play.Frames(3);
        var after = play.SliceBounds("Ⅱ", "nut").GetCenter();
        Near(after, before + new Vector3(.5f, 0, 0), "the ring must move as far as the nut");
    }

    private async Task CheckBlockedRingPreview()
    {
        // The nut stands at z = -3.85 against the wall, whose face is at z = -4. The ring, 25 cm out from its center, would stand
        // inside it.
        play.LookAt(new(-1.5f, .02f, -2.8f), new(-1.5f, .03f, -3.85f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(20);
        Check(PreviewLines("Ⅱ", LensOverlay.Blocked).Any(), "the dashed ring of a blocked stand-up must be red");
        Button(MouseButton.Right, false);
        player.Hud.ClearText();
        Press(Key.F);
        await play.Frames(3);
        Check(!play.State("nut").Rotating, "a blocked stand-up must not start");
        Check(player.Hud.SubtitleText.Contains("转不"), $"a blocked stand-up is explained; saw '{player.Hud.SubtitleText}'");
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
        Check(errors.Any(e => e.Code == LoadErrorCode.InvalidGeometry && e.Source == new Source("Devices/Cabinet", "Segments")), $"a bad face must name the Segments of the device; got {report}");
        Check(errors.Any(e => e.Source == new Source("Devices/Window", "Motion")), $"a pivot of the wrong length must name Motion; got {report}");
        region.Free();
    }

    // A circle cut into 64 pieces of two points each, in any order and direction, as the engine gives the outline of a ring.
    private static void CheckDashes()
    {
        var pieces = new List<Polyline>();
        for (var i = 0; i < 64; i++)
        {
            double[] Point(int k) => [0, .25 * Math.Cos(Math.Tau * k / 64), .25 * Math.Sin(Math.Tau * k / 64)];
            var (a, b) = ((i * 37) % 64, (i * 37) % 64 + 1);
            pieces.Add(new Polyline(i % 3 == 0 ? [.. Point(b), .. Point(a)] : [.. Point(a), .. Point(b)], false));
        }
        var vertices = new List<Vector3>();
        DashedOutline.AddDashes(vertices, pieces);
        var circumference = Mathf.Tau * .25f;
        var dashed = Enumerable.Range(0, vertices.Count / 2).Sum(i => vertices[2 * i].DistanceTo(vertices[2 * i + 1]));
        // The dash is .04 and the gap .03, so 4/7 of the circle is drawn when the pieces are chained; each piece alone is shorter
        // than a dash, and drawn alone it would be solid.
        Check(dashed > circumference * .5f && dashed < circumference * .65f, $"a chained outline is drawn in dashes; {dashed} of {circumference} m is drawn");
    }
}
