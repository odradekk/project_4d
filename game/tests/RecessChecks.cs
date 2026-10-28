using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Characters;
using Game.Debugging;
using Game.Flow;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The recess, scene 4: the class leaves, she flips an eraser and stands a pencil up and lays it down for her desk mate, stands
/// the pencil up in the window rail, and the bell rings before she can lay it down. Also when the walks out of the room are
/// stopped and when she carries the pencil of the window off.
/// </summary>
public partial class RecessChecks : Node
{
    private const string ScenePath = "res://flow/recess.tscn";

    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private Recess scene = null!;
    private int finished;

    private SchoolRoom room => school.Room;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        try
        {
#if DEBUG
            Check(DebugScenes.Find("recess") == ScenePath, "the recess has a debug entry");
#endif
            await Load();
            await CheckClassLeaves();
            await CheckTricks();
            await CheckWindowPencil();
            await Load();
            await CheckStoppedWalks();
            await Load();
            await CheckPencilCarriedOff();
            await Unload();
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("Recess checks passed: class leaves, tricks, window pencil, stopped walks, pencil carried off.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Engine.TimeScale = 1;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _Process(double delta) => school.Listen();

    private async Task Load()
    {
        await Unload();
        school.Forget();
        finished = 0;
        scene = GD.Load<PackedScene>(ScenePath).Instantiate<Recess>();
        scene.Finished += () => finished++;
        AddChild(scene);
        school.Room = SchoolRoom.Of(scene.Region!);
        await play.Frames(3);
    }

    private async Task Unload()
    {
        if (scene is null) return;
        RemoveChild(scene);
        scene.Free();
        scene = null!;
        await play.Frames(10);
    }

    private async Task CheckClassLeaves()
    {
        Check(room.Player.Seated && room.Region.PrismEnabled, "at the bell she sits at her desk, with the prism in her hand");
        Check(room.Classmates.Count == 46 && room.Classmates.All(person => person.Visible), "the whole class is in the room at the bell");
        await school.Fast(() => !room.Door.Visible, 10, "the door must be opened for the class");
        Check(room.Door.CollisionLayer == 0, "an open door stops nobody");
        await school.Fast(() => room.Door.Visible, 40, "the door must shut behind the last of them");
        Check(room.Classmates.All(person => !person.Visible) && !room.Teacher.Visible, "the class and the teacher are out of the room");
        Check(room.Door.CollisionLayer == 1, "a shut door stops her, and cannot be locked yet");
        Check(room.Deskmate.Visible && room.Deskmate.Posture == Posture.Sitting, "her desk mate stays in his seat");
        Check(school.Heard("下课铃") && school.Heard("你不出去玩") && school.Heard("变个魔术"), "the bell, his question and her answer are shown");
    }

    private async Task CheckTricks()
    {
        var region = room.Region;
        // The eraser: R flips it where it lies. Seen from her seat the lettering is a mirror image, upside down.
        school.Aim(school.Where("eraser"));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(room.Player.Hud.HintText.Contains("R  翻面"), $"aiming at the eraser offers to flip it; the hint is '{room.Player.Hud.HintText}'");
        var mark = region.GetNode<Label3D>("Generated/Ⅰ/Dynamic/eraser/Mark");
        var toHer = (room.Player.Position - mark.GlobalPosition) * new Vector3(1, 0, 1);
        Check(mark.GlobalBasis.Determinant() > 0 && mark.GlobalBasis.Y.Dot(toHer.Normalized()) < -.9f, "the lettering reads right and the right way up from her seat");
        Press(Key.R);
        await play.Until(() => room.IsFlipped("eraser"), 4, "the eraser must be flipped");
        mark = region.GetNode<Label3D>("Generated/Ⅰ/Dynamic/eraser/Mark");
        Check(mark.GlobalBasis.Determinant() < 0 && mark.GlobalBasis.Y.Dot(toHer.Normalized()) > .9f, "after the flip the lettering is a mirror image, upside down seen from her seat");
        Check(room.State("eraser").Section == new SectionId("Ⅰ"), "the eraser stays in her section");
        await school.Fast(() => school.Heard("你怎么变的"), 4, "her desk mate must wonder how she did it");
        Check(room.Player.Hud.UnlockedPages.SequenceEqual([2]), "the page of the flip is drawn in the notebook");

        // The pencil: F stands it up, and only a thin plate is left where it lay.
        school.Aim(school.Where("pencilDesk"));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(room.Player.Hud.HintText.Contains("F  竖起"), $"aiming at the pencil offers to stand it up; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.F);
        await play.Until(() => room.IsUpright("pencilDesk"), 4, "the pencil must stand up");
        await school.Fast(() => school.Heard("笔呢"), 4, "her desk mate must ask where the pencil is");
        Check(!room.Player.Hud.UnlockedPages.Contains(3), "the page of standing up waits until the pencil is back");
        school.Aim(school.Where("pencilDesk"));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(room.Player.Hud.HintText.Contains("F  放倒"), $"aiming at the plate offers to lay it down; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.F);
        await play.Until(() => room.IsLying("pencilDesk"), 4, "the pencil must lie down again");
        await school.Fast(() => school.Heard("又回来了"), 4, "her desk mate must see it come back");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([2, 3]), "the pages of the flip and of standing up are drawn");
    }

    private async Task CheckWindowPencil()
    {
        await school.Fast(() => !room.Player.Seated, 20, "she must be free to go to the window after the tricks");
        await school.Fast(() => school.Heard("窗台上还有一支铅笔"), 10, "she must say that there is another pencil on the sill");
        Check(room.Region.PrismEnabled && room.IsLying(SchoolRoom.WindowPencil), "the pencil lies on the sill, and the prism is in her hand");
        await school.Stand(new(-3.1f, .02f, 1.47f), school.Where(SchoolRoom.WindowPencil));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(room.Player.Hud.HintText.Contains("F  竖起"), $"aiming at the pencil on the sill offers to stand it up; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.F);
        await school.Fast(() => finished == 1, 60, "the scene must end when the class is back");
        Check(school.Heard("上课铃"), "the bell is shown");
        Check(!room.Region.PrismEnabled, "the prism goes back in the desk with the bell");
        Check(room.Player.Seated && Apart(room.Player.Position, room.Seat.Origin) < .001f, "she is back in her seat");
        Check(room.Classmates.All(IsSeated) && room.Teacher.Visible && room.Door.Visible, "the class sits again, the teacher is in, and the door is shut");

        // She had no time to lay it down: the pencil stands in the rail, and nothing she presses changes it.
        var rail = room.State(SchoolRoom.WindowPencil);
        Check(rail.UprightOrigin == new SectionId("Ⅰ") && !rail.Rotating && rail.Holder is null, "the pencil of the window stands in the rail");
        Check(Math.Abs(rail.Pose.Position.X - -3.72) < .01 && Math.Abs(rail.Pose.Position.Z - 1.36) < .01, $"it stands where it lay, at the sill; it is at {rail.Pose.Position}");
        school.Aim(school.Where(SchoolRoom.WindowPencil));
        Press(Key.F);
        await play.Frames(30);
        Check(room.IsUpright(SchoolRoom.WindowPencil), "she cannot lay it down once the bell has rung");
    }

    // Someone else gives a classmate and the teacher something to do on their way out: the scene does not wait for them.
    private async Task CheckStoppedWalks()
    {
        var stopped = room.Classmates[10];
        var seat = stopped.Position;
        await school.Fast(() => !room.Door.Visible, 10, "the door must be opened for the class");
        await play.Frames(30);
        stopped.Stop();
        room.Teacher.Stop();
        await school.Fast(() => room.Door.Visible, 40, "the door must shut although two of them never got out");
        Check(stopped.Visible && room.Teacher.Visible, "those who were stopped are still in the room");
        Check(room.Classmates.Count(person => !person.Visible) == 45, "all the others are out");

        await school.DoTheTricks();
        await school.Fast(() => !room.Player.Seated, 20, "she must be free to go to the window");
        await school.Turn(new StandUp(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil), new("Ⅱ")));
        await school.Fast(() => finished == 1, 60, "the scene must end, whoever was stopped on the way");
        Check(room.Classmates.All(IsSeated) && room.Teacher.Visible, "the class is back at the desks, the stopped classmate too");
        Check(stopped.Position.DistanceTo(seat) < .01f, "the stopped classmate is back at the desk, not where it was stopped");
    }

    // She takes the pencil of the window in her hand and stands it up elsewhere: scene 5 finds it in the rail all the same.
    private async Task CheckPencilCarriedOff()
    {
        await school.DoTheTricks();
        await school.Fast(() => !room.Player.Seated, 60, "she must be free to go to the window");
        await school.Stand(new(-3.1f, .02f, 1.47f), school.Where(SchoolRoom.WindowPencil));
        Check(room.Region.Actions!.Hold(new(SchoolRoom.WindowPencil)) is null, "the pencil must come into her hand");
        // Along the lane between two rows of desks to the aisle, and up the aisle.
        await school.Walk(new(-1.26f, 0, 1.47f));
        var middle = new Vector3(-1.26f, 0, 0);
        await school.Walk(middle);
        school.Aim(middle + new Vector3(0, 1.3f, -2));
        await play.Frames(2);
        Check(room.State(SchoolRoom.WindowPencil).Holder is not null, "she still holds the pencil at the other end of the room");
        Press(Key.F);
        await school.Fast(() => finished == 1, 60, "the scene must end");
        var rail = room.State(SchoolRoom.WindowPencil);
        Check(rail.Holder is null && rail.UprightOrigin is not null && !rail.Rotating, "the pencil is let go of and stands");
        Check(Math.Abs(rail.Pose.Position.X - -3.72) < .01 && Math.Abs(rail.Pose.Position.Z - 1.36) < .01, $"it is in the window rail again; it is at {rail.Pose.Position}");
    }

    private static bool IsSeated(Character3D who) => who.Visible && who.Posture == Posture.Sitting;
}
