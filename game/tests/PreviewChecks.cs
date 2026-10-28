using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Godot;
using static Game.Tests.ItemChecks;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// What the lens draws for a nut and a ball on the item test region: the dashed ring of every nut, where the ring will stand
/// and where it is blocked, the sweep that waits for the nut to be still and goes when it moves, the same for laying a standing
/// nut down, no preview for a ball, and the dashes themselves. Expected values are worked out by hand from the nut's ring.
/// </summary>
public partial class PreviewChecks : Node
{
    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;
    private PlayerActions actions => play.Actions;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(null, Scene);
            await CheckHiddenRingPreview();
            await CheckRingFollowsNut();
            await play.Load(r => r.GetNode<HiddenRingItem3D>("Items/Nut").Position = new(-1.5f, .03f, -3.85f), Scene);
            await CheckBlockedRingPreview();
            await play.Load(r => AddBlock(r, "BlockSweep", new(.6f, .9f, 2.2f), new(1f, .8f, .8f), .45, .7), Scene);
            await CheckBlockedSweepTurnsRed();
            await play.Load(null, Scene);
            await CheckSweepWaitsForStillness();
            await play.Load(r => AddBlock(r, "LowWall", new(.5f, .2f, 2.7f), new(1.2f, .4f, .1f), -1, .1), Scene);
            await CheckFoldPlaneWhileMoving();
            await play.Load(r => r.GetNode("Items/Nut").Free(), Scene);
            await CheckBallHasNoPreview();
            await play.Unload();
            CheckDashes();
            GD.Print("Preview checks passed: ring preview, ring follows, blocked ring, blocked sweep, still nut, fold plane, ball without preview, dashes.");
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

    // The path of the lines drawn in the lens of a section, of a color.
    private IEnumerable<Vector3> PreviewLines(string section, Color color)
    {
        var preview = play.Region.GetNodeOrNull<MeshInstance3D>($"Generated/{section}/Overlay/Preview");
        for (var i = 0; preview?.Mesh is not null && i < preview.Mesh.GetSurfaceCount(); i++)
            if (((StandardMaterial3D)preview.Mesh.SurfaceGetMaterial(i)).AlbedoColor == color)
                foreach (var vertex in preview.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()) yield return vertex;
    }

    // The ring of a nut is some hundreds of line ends; its sweep is tens of thousands.
    private bool SweepShown() => PreviewLines("Ⅱ", LensOverlay.Clear).Count() > 2000;

    private async Task CheckHiddenRingPreview()
    {
        // The lens shows the ring of a nut lying in the section whether or not the player aims at the nut.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, 1.3f, 0f));
        Button(MouseButton.Right, true);
        await play.Frames(2);
        Check(PreviewLines("Ⅱ", LensOverlay.Clear).Any(v => v.Y > 1.1f), "the dashed ring of a nut must show with the lens up even when the nut is not aimed at");
        Button(MouseButton.Right, false);
        await play.Frames(2);
        Check(!play.Region.HasNode("Generated/Ⅱ/Overlay/Preview"), "lowering the lens must clear the ring");

        // Turning a quarter about the hinge at x = .49 takes the ring's center, .42 ahead and .2 deep, to W = .42 and .2 behind the
        // hinge, so it stands at x = .29, y = .03 + .87 = .9 and z = 2.2, 50 cm across: the lens shows where, before the nut is stood up.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Check(play.Mask("Ⅰ", "nut") != Colors.Black, "the nut glows in the lens");
        Button(MouseButton.Right, true);
        await play.Frames(2);
        // At first only the ring is drawn: the sweep of a nut is slow to cut, and waits for the nut and the player to be still.
        var lines = PreviewLines("Ⅱ", LensOverlay.Clear).ToArray();
        Check(lines.Length > 0 && !PreviewLines("Ⅱ", LensOverlay.Blocked).Any(), "a clear stand-up draws pale lines and no red");
        Check(!SweepShown(), "the sweep of a nut waits for it to be still");
        var ring = lines.Where(v => v.Y > .6f).ToArray();
        var top = ring.Max(v => v.Y);
        Check(ring.Length > 0 && Mathf.Abs(top - 1.15f) < .01f, $"the dashed ring must reach y = 1.15; its top is {top}");
        Check(Mathf.Abs(ring.Average(v => v.Z) - 2.2f) < .03f, $"the dashed ring must be centered at z = 2.2; it is at {ring.Average(v => v.Z)}");

        // Once they have been still, the sweep and the fold plane come; and nothing changes after that, so it is kept.
        await play.Frames(40);
        Check(SweepShown(), "a still nut must show its sweep");
        var drawn = play.Region.GetNode<MeshInstance3D>("Generated/Ⅱ/Overlay/Preview").GetInstanceId();
        await play.Frames(60);
        Check(play.Region.GetNode<MeshInstance3D>("Generated/Ⅱ/Overlay/Preview").GetInstanceId() == drawn, "an unchanged preview must be kept");

        Press(Key.F);
        await play.Frames(TurnFrames);
        Button(MouseButton.Right, false);
        Check(play.State("nut").UprightOrigin?.Value == "Ⅰ", "F must stand the nut up");
        Check(play.Region.HasNode("Generated/Ⅰ/Dynamic/nut/Collision") && play.Region.HasNode("Generated/Ⅱ/Dynamic/nut/Collision"),
            "the nut and its ring must have collision bodies where they show");
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
        // Laying it down is previewed as standing it up is: the sweep comes once it is still, and goes when it moves.
        Button(MouseButton.Right, true);
        await play.Frames(45);
        Check(SweepShown(), "a standing nut that is still must show the sweep of laying it down");
        var before = play.SliceBounds("Ⅱ", "nut").GetCenter();
        await play.Walk(player.Position + new Vector3(.5f, 0, 0));
        await play.Frames(3);
        Check(!SweepShown(), "the sweep of laying a nut down must be dropped when it moves");
        Button(MouseButton.Right, false);
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

    private async Task CheckBlockedSweepTurnsRed()
    {
        // A block that exists from W = .45 on lies in the way of the ring, which leans out to W = .465 + .05 on its way to the
        // second section. At first the ring is drawn red with a mark where it is stopped; once the nut is still, the sweep comes,
        // pale up to the block and red from it.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(2);
        Check(PreviewLines("Ⅱ", LensOverlay.Blocked).Any() && !PreviewLines("Ⅱ", LensOverlay.Clear).Any(), "a blocked ring is drawn red alone at first");
        Check(play.Region.GetNode("Generated/Ⅱ/Overlay/Preview").GetChildCount() > 0, "the lens must mark where the ring is stopped");
        await play.Frames(40);
        Check(PreviewLines("Ⅱ", LensOverlay.Clear).Any() && PreviewLines("Ⅱ", LensOverlay.Blocked).Any(), "a still nut shows its sweep pale up to the block and red from it");
        Button(MouseButton.Right, false);
    }

    private async Task CheckSweepWaitsForStillness()
    {
        // Carried about with the lens up, a nut shows its ring and not its sweep, which is dropped again when it moves.
        play.LookAt(new(.5f, .02f, 3f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        player.Camera.Rotation = Vector3.Zero;
        Button(MouseButton.Right, true);
        for (var i = 0; i < 60; i++)
        {
            player.Position += new Vector3(i % 30 < 15 ? .02f : -.02f, 0, 0);
            await play.Frames(1);
            Check(!SweepShown(), "a moving nut must not show its sweep");
        }
        Check(PreviewLines("Ⅱ", LensOverlay.Clear).Any(), "a moving nut still shows its ring");
        await play.Frames(40);
        Check(SweepShown(), "a nut that is still shows its sweep");
        player.Position += new Vector3(.02f, 0, 0);
        await play.Frames(2);
        Check(!SweepShown(), "the sweep must be dropped as soon as the nut moves");
        Button(MouseButton.Right, false);
    }

    private async Task CheckFoldPlaneWhileMoving()
    {
        // A low wall .5 m from the nut, between her and it, is the plane that R would flip the nut about, and the flip sweeps
        // about .5 m deep into W, past the second section; the wall exists only up to W = .1, so it is not in the way. The fold
        // plane is cheap to make, so it is drawn at once and while she moves, without the sweep. She moves a millimetre at a
        // time, since the nut is 6 cm wide and she must keep aiming at it.
        play.LookAt(new(.5f, .02f, 3.5f), new(.5f, .03f, 2.2f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(2);
        Check(PreviewLines("Ⅱ", LensOverlay.Fold).Any() && !SweepShown(), "a nut must show the fold plane of R at once, without its sweep");
        for (var i = 0; i < 30; i++)
        {
            player.Position += new Vector3(0, 0, i % 30 < 15 ? .001f : -.001f);
            await play.Frames(1);
        }
        Check(PreviewLines("Ⅱ", LensOverlay.Fold).Any() && !SweepShown(), "a nut must show the fold plane of R while she moves, without its sweep");
        Button(MouseButton.Right, false);
    }

    private async Task CheckBallHasNoPreview()
    {
        // A ball is the same after any turn, so the lens draws nothing for it, in the hand or not. The nut is left out of the room.
        play.LookAt(new(-1.2f, .02f, 2.8f), new(-1.2f, .25f, 1.8f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(40);
        Check(!play.Region.HasNode("Generated/Ⅱ/Overlay/Preview"), "an aimed ball must have no sweep and no fold plane in the lens");
        Press(Key.E);
        await play.Frames(40);
        Check(actions.Held == "ball" && !play.Region.HasNode("Generated/Ⅱ/Overlay/Preview"), "a held ball must have no preview");
        Button(MouseButton.Right, false);
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
