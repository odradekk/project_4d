using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Game.UI;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// What the hand's actions tell the player: the slice that slides and scrapes against an obstacle, the marks
/// and sweep lines in the lens, and the spoken reason when a turn is refused.
/// </summary>
public partial class FeedbackChecks : Node
{
    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load();
            await CheckGrabbedSlice();
            await play.Load();
            await CheckClearPreview();
            await play.Load();
            await CheckBlockedTurns();
            await play.Load(MoveBoxBetweenSections);
            await CheckBlockedBetweenSections();
            GD.Print("Feedback checks passed: grabbed slice, marks, scraping, preview, refusals.");
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

    private async Task CheckGrabbedSlice()
    {
        // Stand the pole on the floor, then grab its slice and push it into a box that exists only in II.
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(2);
        Press(Key.F);
        await play.Frames(TurnFrames);
        Press(Key.E);
        await play.Frames(90);
        Check(play.Actions.Held is null && play.State("pole").UprightOrigin is not null && play.Position("pole").Y < .04f, "the pole must stand on the floor");

        var slice = play.SliceBounds("Ⅰ", "pole").GetCenter();
        play.LookAt(new(slice.X, .02f, slice.Z + .7f), slice);
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("抓住"), "aiming at a slice must offer grabbing it");
        Press(Key.E);
        await play.Frames(3);
        Check(play.Actions.Held == "pole" && play.Actions.HeldIsUpright, "E on a slice must grab it");
        Near(play.SliceBounds("Ⅰ", "pole").GetCenter(), slice, "grabbing a slice must not move it");
        Press(Key.E);
        await play.Frames(3);
        Check(play.Actions.Held is null, "E must let go of a slice at any time");
        Press(Key.E);
        await play.Frames(3);
        Check(play.Actions.Held == "pole", "a slice that was let go can be grabbed again");

        // Bring the slice to x = -1.3, z = -1.0, clear of everything, then push it west into the box at x = -1.5.
        var start = play.SliceBounds("Ⅰ", "pole").GetCenter();
        var feet = player.Position + new Vector3(-1.3f - start.X, 0, -1.0f - start.Z);
        await play.Walk(feet);
        await play.Frames(3);
        Near(play.SliceBounds("Ⅰ", "pole").GetCenter(), new(-1.3f, start.Y, -1.0f), "the grabbed slice must follow the hand to a free place");

        // The box spans z from -1.5 to -0.5, so the slice slides along its face while it moves from z = -1.0 to -0.7.
        player.Position = feet + new Vector3(-.8f, 0, .3f);
        await play.Frames(3);
        var blocked = play.SliceBounds("Ⅰ", "pole").GetCenter();
        Check(Mathf.Abs(blocked.X - -1.494f) < .01f, $"the slice must stop against the box in the other section; x = {blocked.X}");
        Check(Mathf.Abs(blocked.Z - -.7f) < .01f, $"the slice must slide along the box; z = {blocked.Z}");
        Near(play.SliceBounds("Ⅱ", "pole").GetCenter(), blocked, "the slice in the other section stops at the same place");
        Check(play.Actions.GetNode<AudioStreamPlayer>("Scratch").Playing, "a blocked slice must scrape");
        Check(play.Region.GetNode("Generated/Ⅱ/Overlay/Contacts").GetChildCount() > 0, "the lens must mark where the slice is blocked");
        await play.Frames(45);
        Check(play.Region.GetNode("Generated/Ⅱ/Overlay/Contacts").GetChildCount() == 0, "a mark must fade away when the hand stops pushing");

        // The hand walks away while the slice stays against the box; far enough, the slice is let go.
        player.Position += new Vector3(4f, 0, 0);
        await play.Frames(5);
        Check(play.Actions.Held is null, "a slice left behind the hand must be let go");
    }

    private async Task CheckClearPreview()
    {
        // Aimed at but not held, the pole lying on the floor shows the sweep of its stand-up.
        play.LookAt(new(3f, .02f, 1.5f), new(1.9f, .03125f, 1.5f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(20);
        var aimed = PreviewColors("Ⅱ").ToArray();
        Check(aimed.Contains(LensOverlay.Clear) && !aimed.Contains(LensOverlay.Blocked), "the clear sweep of an aimed object must be drawn pale");
        Button(MouseButton.Right, false);
        await play.Frames(20);

        // Held east of its middle, flipping the pole also sweeps through the other section.
        Press(Key.E);
        await play.Frames(3);
        Check(!PreviewColors("Ⅱ").Any(), "without the lens raised nothing is drawn");
        Button(MouseButton.Right, true);
        await play.Frames(20);
        var colors = PreviewColors("Ⅱ").ToArray();
        Check(colors.Contains(LensOverlay.Clear), "a clear sweep must be drawn pale");
        Check(!colors.Contains(LensOverlay.Blocked), "a clear sweep has no red");
        Check(colors.Contains(LensOverlay.Fold), "a flip must draw its fold plane");
        Button(MouseButton.Right, false);
        await play.Frames(20);
        Check(!PreviewColors("Ⅱ").Any(), "lowering the lens must clear the preview");
    }

    private async Task CheckBlockedTurns()
    {
        // The post, which shows only in the second section, stops the stand-up near its end: pale until the post, red from it.
        // The pole is picked up from the east, then carried south along the cabinet to stand in front of the post.
        play.LookAt(new(3f, .02f, 1.5f), new(1.9f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        await play.Walk(new(3f, 0, .9f));
        await play.Frames(3);
        Button(MouseButton.Right, true);
        await play.Frames(20);
        var colors = PreviewColors("Ⅱ").ToArray();
        Check(colors.Contains(LensOverlay.Clear) && colors.Contains(LensOverlay.Blocked), "a blocked sweep is pale up to the obstacle and red from it");
        // The slice moves toward the hinge as the turn goes on, so what the turn passes first lies farther out in x.
        var pale = PreviewLines("Ⅱ", LensOverlay.Clear).Select(vertex => vertex.X).ToArray();
        var red = PreviewLines("Ⅱ", LensOverlay.Blocked).Select(vertex => vertex.X).ToArray();
        Check(pale.Average() > red.Average(), "the red of a blocked sweep must come after the pale");
        Button(MouseButton.Right, false);
        Press(Key.F);
        await play.Frames(3);
        Check(!play.State("pole").Rotating, "a blocked turn must not start");
        Check(player.Hud.SubtitleText == "小满：转不过去……那一页有东西挡着。", $"a blocked turn is explained; saw '{player.Hud.SubtitleText}'");
        Press(Key.E);
        await play.Frames(3);

        // The crate stands next to a box that shows in the second section, where the lens shows it too.
        player.Hud.ClearText();
        play.LookAt(new(-2.6f, .02f, .4f), new(-2.6f, .25f, -.9f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(20);
        var crate = PreviewColors("Ⅱ").ToArray();
        Check(crate.Contains(LensOverlay.Blocked) && !crate.Contains(LensOverlay.Clear), "the box stops the crate before it reaches the section: all red");
        Button(MouseButton.Right, false);
        Press(Key.F);
        await play.Frames(3);
        Check(!play.State("crate").Rotating, "a blocked turn must not start");
        Check(player.Hud.SubtitleText == "小满：转不过去……那一页有东西挡着。", $"an obstruction that shows in another section is named; saw '{player.Hud.SubtitleText}'");

        var home = play.Actions.CurrentSection;
        Check(RefusalLines.For(new(FailureCode.RotationBlocked, home), home) == "转不开……这边太挤了。", "an obstruction in this section is crowding");
        Check(RefusalLines.For(new(FailureCode.OutOfRange, null), home) is { } outside && outside.Contains("转出屋子"), "a turn that leaves the building is explained");
        Check(RefusalLines.For(new(FailureCode.WrongSection, null), home) is { } line && line.Contains("原来那一页"), "laying down elsewhere is explained");
        Check(RefusalLines.For(new(FailureCode.NotHoldable, null), home) is { } heavy && heavy.Contains("拿不动"), "an object that cannot be held is explained");
        Check(RefusalLines.For(new(FailureCode.InvalidInput, null), home) is null, "other refusals stay silent");
    }

    // The box next to the crate is moved to W = 0.1 to 0.3, between the sections, where no section shows it.
    private static void MoveBoxBetweenSections(Region3D region)
    {
        var box = region.GetNode<BuildingElement3D>("Buildings/Home/OnlyII");
        box.WMin = .1;
        box.WMax = .3;
    }

    private async Task CheckBlockedBetweenSections()
    {
        play.LookAt(new(-2.6f, .02f, .4f), new(-2.6f, .25f, -.9f));
        await play.Frames(2);
        Press(Key.F);
        await play.Frames(3);
        Check(!play.State("crate").Rotating, "a blocked turn must not start");
        Check(player.Hud.SubtitleText == "小满：转不过去……好像卡在两页中间了。", $"an obstruction that no section shows is named so; saw '{player.Hud.SubtitleText}'");
    }

    private IEnumerable<Vector3> PreviewLines(string section, Color color)
    {
        var preview = play.Region.GetNodeOrNull<MeshInstance3D>($"Generated/{section}/Overlay/Preview");
        for (var i = 0; preview?.Mesh is not null && i < preview.Mesh.GetSurfaceCount(); i++)
            if (((StandardMaterial3D)preview.Mesh.SurfaceGetMaterial(i)).AlbedoColor == color)
                foreach (var vertex in preview.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()) yield return vertex;
    }

    private IEnumerable<Color> PreviewColors(string section)
    {
        var preview = play.Region.GetNodeOrNull<MeshInstance3D>($"Generated/{section}/Overlay/Preview");
        for (var i = 0; preview?.Mesh is not null && i < preview.Mesh.GetSurfaceCount(); i++)
            yield return ((StandardMaterial3D)preview.Mesh.SurfaceGetMaterial(i)).AlbedoColor;
    }
}
