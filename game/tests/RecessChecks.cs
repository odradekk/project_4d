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
    // Where she stands to reach the pencil on the sill of the first window, and the place of its hinge: z = -4.12 + .92.
    private static readonly Vector3 WindowSpot = new(-3.1f, 0, -3.4f);
    private const double HingeZ = -3.2;

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
            await CheckWindowPencilBeforeTheTricks();
            await Load();
            await CheckPencilCannotBeTaken();
            await Unload();
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("Recess checks passed: class leaves, tricks, window pencil, stopped walks, window pencil before the tricks, pencil that cannot be taken.");
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
        await school.Stand(new(room.DoorIn.X, 0, 4.5f), new(room.DoorIn.X, 1.1f, 5.55f));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(room.Player.Hud.HintText == "", $"the shut door offers nothing before the cleaning; the hint is '{room.Player.Hud.HintText}'");
        var ray = PhysicsRayQueryParameters3D.Create(new(room.DoorIn.X, 1, 5), new(room.DoorIn.X, 1, 6), 1);
        Check(room.Player.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count > 0, "a shut door stops whoever walks");
        room.Player.Transform = room.Seat;
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
        Check(mark.GlobalBasis.Determinant() > 0 && mark.GlobalBasis.Y.Dot(toHer.Normalized()) < -.7f, "the lettering reads right and the right way up from her seat");
        Press(Key.R);
        await play.Until(() => room.IsFlipped("eraser"), 4, "the eraser must be flipped");
        mark = region.GetNode<Label3D>("Generated/Ⅰ/Dynamic/eraser/Mark");
        Check(mark.GlobalBasis.Determinant() < 0 && mark.GlobalBasis.Y.Dot(toHer.Normalized()) > .7f, "after the flip the lettering is a mirror image, upside down seen from her seat");
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
        await school.Stand(WindowSpot, school.Where(SchoolRoom.WindowPencil));
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
        Check(Math.Abs(rail.Pose.Position.X - -3.72) < .01 && Math.Abs(rail.Pose.Position.Z - HingeZ) < .01, $"it stands where it lay, at the sill; it is at {rail.Pose.Position}");
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

    // The pencil on the sill is out of reach of her seat, and whatever is done to it before the tricks are done, the bell waits.
    private async Task CheckWindowPencilBeforeTheTricks()
    {
        school.Aim(school.Where(SchoolRoom.WindowPencil));
        room.Player.Hud.ClearText();
        await play.Frames(2);
        Check(!room.Player.Hud.HintText.Contains("竖起"), $"from her seat she cannot reach the pencil on the sill; the hint is '{room.Player.Hud.HintText}'");
        var actor = new EntityId(SchoolRoom.Actor);
        await school.Turn(new Flip(actor, new(SchoolRoom.WindowPencil)));
        await school.Turn(new StandUp(actor, new(SchoolRoom.WindowPencil), new("Ⅱ")));
        await school.Fast(() => room.Classmates.All(person => !person.Visible), 40, "the class must leave");
        await play.Frames(180);
        Check(finished == 0 && room.Region.PrismEnabled && room.Player.Seated, "the bell does not ring before the eraser and the pencil on the desk have been shown");
        await school.DoTheTricks();
        await school.Fast(() => finished == 1, 60, "the scene must end once the tricks are done");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([2, 3]), "the pages of the tricks are drawn");
    }

    // E on the pencil of the window picks it up, and it is put back at once: it would not come back from outside the window.
    private async Task CheckPencilCannotBeTaken()
    {
        await school.DoTheTricks();
        await school.Fast(() => !room.Player.Seated, 60, "she must be free to go to the window");
        await school.Stand(WindowSpot, school.Where(SchoolRoom.WindowPencil));
        var lay = room.State(SchoolRoom.WindowPencil).Pose;
        Check(lay == room.State(SchoolRoom.WindowPencil).Definition.Pose, "the pencil lies where the scene put it, to the last digit");
        Press(Key.E);
        await play.Frames(5);
        // The scene looks at her hand once a frame, after the frame has begun.
        await play.Frames(2);
        var pencil = room.State(SchoolRoom.WindowPencil);
        Check(pencil.Holder is null && pencil.UprightOrigin is null && pencil.Pose == lay, $"the pencil is on the sill where it lay, not in her hand; it is at {pencil.Pose.Position}");

        // R flips it across the inner face of the sill, into the room, where it would fall to the floor; it is put back.
        Press(Key.R);
        await play.Until(() => room.State(SchoolRoom.WindowPencil).Rotating, 3, "the flip of the pencil must start");
        await play.Until(() => !room.State(SchoolRoom.WindowPencil).Rotating, 3, "the flip of the pencil must end");
        await play.Frames(10);
        pencil = room.State(SchoolRoom.WindowPencil);
        Check(pencil.UprightOrigin is null && pencil.Pose == lay && pencil.Handedness == Handedness.Normal,
            $"the pencil is on the sill where it lay, not flipped into the room; it is at {pencil.Pose.Position} and {pencil.Handedness}");
        Press(Key.F);
        await school.Fast(() => finished == 1, 60, "the scene must end");
        Check(room.IsUpright(SchoolRoom.WindowPencil), "the pencil can still be stood up");
    }

    private static bool IsSeated(Character3D who) => who.Visible && who.Posture == Posture.Sitting;
}
