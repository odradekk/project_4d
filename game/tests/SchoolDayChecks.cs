using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Characters;
using Game.Flow;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Scenes 3, 4 and 5 played one after the other in the school region of scene 3: what each leaves is what the next finds,
/// the notebook keeps the pages, and the pencil that scene 4 stands in the window rail is still there for scene 5.
/// </summary>
public partial class SchoolDayChecks : Node
{
    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private MathClass math = null!;
    private int finished;

    private SchoolRoom room => school.Room;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        try
        {
            await CheckMathClassThenRecess();
            await CheckRecessThenDuty();
            RemoveChild(math);
            math.Free();
            await play.Frames(10);
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("School day checks passed: scene 3 to 4, scene 4 to 5, notebook pages.");
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

    // Scene 3 ends with her sitting, the teacher where he stopped with the checkpoint of scene 3, and the prism usable.
    private async Task CheckMathClassThenRecess()
    {
        math = GD.Load<PackedScene>("res://flow/math_class.tscn").Instantiate<MathClass>();
        math.Finished += () => finished++;
        AddChild(math);
        school.Room = SchoolRoom.Of(math.Region);
        var teacher = room.Teacher.GetNode<Teacher>("Teacher");
        await school.Fast(() => room.Region.PrismEnabled, 150, "the lecture must end");
        foreach (var entity in new[] { "clockII", "deskII49", "chimneyII1" }) room.Player.EmitSignal(FirstPersonPlayer.SignalName.LensMarked, entity);
        await school.Fast(() => finished == 1, 60, "scene 3 must end with the three differences");
        Check(room.Player.Seated && room.Region.PrismEnabled && teacher.Checkpoint is not null && !room.Teacher.Busy, "scene 3 leaves her seated, the prism usable and the teacher standing with its checkpoint");

        var recess = new Recess { Region = math.Region };
        recess.Finished += () => finished++;
        AddChild(recess);
        await play.Frames(3);
        Check(recess.Region == math.Region && teacher.Checkpoint is null, "scene 4 plays in the same region, and the teacher no longer sends her back to scene 3");
        await school.Fast(() => room.Classmates.All(person => !person.Visible), 40, "the class must leave");
        await school.DoTheTricks();
        await school.Fast(() => !room.Player.Seated, 30, "she must be free to go to the window");
        await school.Turn(new StandUp(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil), new("Ⅱ")));
        await school.Fast(() => finished == 2, 60, "scene 4 must end");
        Check(room.Player.Seated && room.IsUpright(SchoolRoom.WindowPencil), "scene 4 leaves her seated, and the pencil standing in the window rail");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([1, 2, 3]), "the notebook has the pages of scenes 3 and 4");
        RemoveChild(recess);
        recess.Free();
    }

    // Scene 5 takes the pencil as scene 4 left it, without standing it up again.
    private async Task CheckRecessThenDuty()
    {
        var before = room.State(SchoolRoom.WindowPencil).Pose;
        var duty = new Duty { Region = math.Region };
        duty.Finished += () => finished++;
        AddChild(duty);
        await school.Fast(() => room.Broom.CollisionLayer != 0, 40, "the chores must start");
        Check(room.State(SchoolRoom.WindowPencil).Pose == before && room.IsUpright(SchoolRoom.WindowPencil), "the pencil was not stood up again");
        Check(room.Region.PrismEnabled && !room.Player.Seated && room.Classmates.All(person => !person.Visible) && !room.Teacher.Visible, "she is up with the prism, and the class is gone");

        room.Broom.Interact();
        room.Chalkboard.Interact();
        foreach (var row in room.Rows) row.Interact();
        Check(room.Region.World!.DriveDevice(new("sash1"), .69) is DriveDeviceResult.Driven && room.Region.World.DriveDevice(new("sash3"), .69) is DriveDeviceResult.Driven { Position: < .69 },
            "a window stops at the pencil in the rail");
        await school.Turn(new LayDown(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil)));
        foreach (var window in SchoolRoom.Windows) room.Region.World.DriveDevice(new(window), .69);
        await school.Fast(() => room.Player.Hud.UnlockedPages.Contains(4), 30, "the page of the glowing things must be drawn");
        room.Door.Interact();
        await school.Fast(() => finished == 3, 20, "scene 5 must end");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([1, 2, 3, 4]), "the notebook has the pages of all three scenes");
        RemoveChild(duty);
        duty.Free();
    }
}
