using System;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Game.Rendering;
using Godot;
using static Game.Tests.SeamPlay;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Crossing seams on the seam test region: the plain seam and the mirror seam, the long pole that stands up in a seam, the
/// seam that is nearest, and the reasons she gives when the engine refuses.
/// </summary>
public partial class CrossingChecks : Node
{
    private TurningFixture play = null!;
    private SeamPlay seam = null!;
    private FirstPersonPlayer player => play.Player;
    private FourD.World world => seam.World;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        seam = new SeamPlay(this, play);
        try
        {
            await seam.Load(r => AddBlock(r, "slab", new(-3, .5f, 0), new(1, 1, 1)));
            await CheckPlainCrossing();
            await seam.Load();
            await CheckMirrorCrossing();
            await seam.Load();
            await CheckLongPole();
            await seam.Load(PrepareRefusals);
            await CheckRefusals();
            await seam.Load(r => r.GetNode<Node3D>("Seams/Glass").Position = new(-.5f, 0, 1.5f));
            await CheckNearestSeam();
            await seam.Load(r => AddBlock(r, "post", new(-1.1f, .5f, 1.5f), new(.2f, 1, 1)));
            await CheckBlockedStandUp();
            await play.Unload();
            GD.Print("Crossing checks passed: plain and mirror seams, long pole, refusals, nearest seam.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.ActionRelease("move_right");
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // A plug in the second section right where she lands from the plain seam, and the crate by the seam to the shallower section.
    private static void PrepareRefusals(Region3D region)
    {
        AddBlock(region, "plug", new(-1.5f, .5f, 1.5f), new(1, 1, 1));
        region.GetNode<Node3D>("Sections/Ⅰ/Turnables/Crate").Position = new(.9f, .25f, -1f);
    }

    private async Task CheckPlainCrossing()
    {
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        Check(Hit(player.GetWorld3D(), new(-2, .5f, 0), new(-4, .5f, 0)) is null, "the slab must not exist in I");
        Check(!player.Hud.HintText.Contains("穿缝"), "with the lens down she is not told about a seam");
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && seam.Said == "", $"with the lens down a click crosses nothing and says nothing; she said \"{seam.Said}\"");
        await seam.RaiseLens();
        Check(player.Hud.HintText.Contains("左键  穿缝"), "near an open seam with the lens raised she is told she can cross");

        var start = player.Position;
        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        Near(player.Position with { Y = 0 }, start with { Y = 0 }, "a plain seam leaves her where she stood");
        Check(!play.Actions.Mirrored, "a plain seam does not turn her into a mirror image");
        Check(Hit(player.GetWorld3D(), new(-2, .5f, 0), new(-4, .5f, 0)) == "slab", "after crossing she must collide with what is in II");
        await play.Frames(30);
        Check(player.IsOnFloor(), "she must stand on the floor of the section she arrived in");

        await seam.Click();
        seam.CheckArrived("Ⅰ", "Ⅱ");
        Check(Hit(player.GetWorld3D(), new(-2, .5f, 0), new(-4, .5f, 0)) is null, "back in I the slab is gone again");
    }

    private async Task CheckMirrorCrossing()
    {
        seam.Pack("rubber");
        await seam.Stand(new(1.65f, .02f, 1.5f), new(1.5f, 1, 1.5f));
        await seam.RaiseLens();
        await CheckControlsFollowTheView(mirrored: false);
        await seam.Stand(new(1.65f, .02f, 1.5f), new(1.5f, 1, 1.5f));
        Check(play.State("rubber").Handedness == Handedness.Normal, "before the seam what she carries is as it was");

        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        Check(play.Actions.Mirrored && play.State("xiaoman").Handedness == Handedness.Mirrored, "a mirror seam turns her into a mirror image");
        Near(player.Position with { Y = 0 }, new(1.35f, 0, 1.5f), "the mirror seam puts her on the other side of its plane");
        Check(play.State("rubber").Handedness == Handedness.Mirrored, "what is in her backpack is turned with her");
        var mirror = play.Region.GetNode<MirrorView>("Generated/Travel/Mirror");
        Check(mirror.Flipped && seam.Prism.Flipped, "the picture, the lens included, must be flipped while she is a mirror image");
        Check(mirror.Layer < seam.Prism.Layer && seam.Prism.Layer < player.Hud.Layer, "the flip must lie under the lens and the interface, which stay as they are");
        await CheckControlsFollowTheView(mirrored: true);

        await seam.Stand(new(1.35f, .02f, 1.5f), new(1.5f, 1, 1.5f));
        await seam.Click();
        seam.CheckArrived("Ⅰ", "Ⅱ");
        Check(!play.Actions.Mirrored && !mirror.Flipped && !seam.Prism.Flipped, "the second half turn gives her back, and the picture with her");
        Check(play.State("rubber").Handedness == Handedness.Normal, "and what she carries");
        await CheckControlsFollowTheView(mirrored: false);
    }

    // Moving the mouse to the right turns the camera to the right, and the right key steps to the right of the camera. Mirrored,
    // the picture is the other way round, so the camera turns and steps to its own left to look the same.
    private async Task CheckControlsFollowTheView(bool mirrored)
    {
        var yaw = player.Rotation.Y;
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(30, 0), ScreenRelative = new(30, 0) });
        await play.Frames(2);
        var turn = player.Rotation.Y - yaw;
        Check(mirrored ? turn > 0 : turn < 0, $"mouse turning must follow the view; the camera turned by {turn} with mirrored = {mirrored}");
        var start = player.Position;
        var right = player.Basis.X;
        Input.ActionPress("move_right");
        await play.Frames(8);
        Input.ActionRelease("move_right");
        var step = (player.Position - start).Dot(right);
        Check(mirrored ? step < -.05f : step > .05f, $"the right key must step with the view; it moved {step} along the camera's right");
    }

    private async Task CheckLongPole()
    {
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await seam.Hold("pole");
        await seam.RaiseLens();
        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        var pole = play.State("pole");
        Check(pole.UprightOrigin?.Value == "Ⅰ" && pole.Holder == SeamPlay.Xiaoman && !pole.InBackpack,
            "a pole long enough for II stands up in the seam, hinged in I, and stays in her hand");
        Check(play.Actions.Held == "pole" && play.Actions.HeldIsUpright, "she holds its slice");
        await play.Frames(5);
        Check(play.HasSlice("Ⅱ", "pole") && play.HasSlice("Ⅰ", "pole"), "its slice shows in the section she arrived in, and in the one it is hinged in");
        var standing = play.Position("pole");
        await play.Frames(30);
        Near(play.Position("pole"), standing, "the slice she holds must stay in the seam, not follow her hand");

        Press(Key.F);
        await play.Frames(2);
        Check(seam.Said.Contains("原来那一页"), $"it can be laid down only in the section it was stood up in; she said \"{seam.Said}\"");
        player.Hud.ClearText();
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅱ" && seam.Said.Contains("带不过缝"), $"holding the standing pole she cannot cross; she said \"{seam.Said}\"");
        player.Hud.ClearText();
        Press(Key.E);
        await play.Frames(3);
        Check(play.Actions.Held is null, "she lets go of the slice");
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅱ" && seam.Said.Contains("缝口这边有东西挡着"),
            $"the pole in the seam blocks her way back, in the section she is in; she said \"{seam.Said}\"");
    }

    private async Task CheckRefusals()
    {
        await seam.RaiseLens();
        await seam.Stand(new(-.5f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        Check(!player.Hud.HintText.Contains("穿缝"), "a seam beyond her reach is not offered");
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && seam.Said.Contains("再靠近"), $"a seam beyond her reach is not crossed; she said \"{seam.Said}\"");

        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        world.SetSeamOpen(new("needle"), false);
        await play.Frames(2);
        Check(!player.Hud.HintText.Contains("穿缝"), "a closed seam is not offered");
        await seam.Click();
        Check(seam.Said.Contains("合上了"), $"she says a closed seam is shut; she said \"{seam.Said}\"");
        world.SetSeamOpen(new("needle"), true);

        player.Hud.ClearText();
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && seam.Said.Contains("那一页的缝口有东西挡着"),
            $"the plug in II blocks the landing, and she says it is in that section; she said \"{seam.Said}\"");

        await seam.Stand(new(3.35f, .02f, -1.5f), new(3.5f, 1, -1.5f));
        Check(!player.Hud.HintText.Contains("穿缝"), "a seam that was cut is not offered");
        await seam.Click();
        Check(seam.Said.Contains("咬断"), $"a seam that was cut is not crossed; she said \"{seam.Said}\"");

        // No seam to the section the lens shows is within reach: the one at z = -1.5 joins I and III, not II.
        await seam.Stand(new(.15f, .02f, -1.5f), new(0, 1, -1.5f));
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && seam.Said == "", $"no seam to that section within reach is no refusal; she said \"{seam.Said}\"");
        Button(MouseButton.WheelDown, true);
        await play.Frames(2);
        Check(seam.Prism.TargetSection == "Ⅲ", "the wheel moves the lens to III");
        await seam.Hold("crate");
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && seam.Said.Contains("带不过缝"),
            $"a crate too big for the backpack and too short for III does not cross; she said \"{seam.Said}\"");
        Press(Key.E);
        await play.Frames(3);
        await seam.Click();
        seam.CheckArrived("Ⅲ", "Ⅰ");
    }

    // The mirror seam stands 1 m from the plain one, and a click crosses the one she stands at, whichever is listed first.
    private async Task CheckNearestSeam()
    {
        await seam.Stand(new(-.55f, .02f, 1.5f), new(-.5f, 1, 1.5f));
        await seam.RaiseLens();
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅱ" && play.Actions.Mirrored, "she crosses the nearest seam, which is the mirror seam");
    }

    // The post in II, in the way of the pole's quarter turn, keeps the pole out of the seam, and nothing is changed.
    private async Task CheckBlockedStandUp()
    {
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await seam.Hold("pole");
        await seam.RaiseLens();
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && play.Actions.Held == "pole" && play.State("pole").UprightOrigin is null,
            "a pole that cannot stand up in the seam stays in her hand, lying, and she stays where she is");
        Check(seam.Said.Contains("那一页有东西挡着"), $"she says what blocks the pole is in the other section; she said \"{seam.Said}\"");
    }
}
