using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Scenes 3, 4 and 5 played one after the other in one school region, which a node above them loads once: what each leaves
/// is what the next finds, the notebook keeps the pages, and the pencil that scene 4 stands in the window rail is still there
/// for scene 5. Also that the nodes the scenes use sit where the region has its desks and its board.
/// </summary>
public partial class SchoolDayChecks : Node
{
    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private Region3D region = null!;
    private int finished;

    private SchoolRoom room => school.Room;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        try
        {
            region = GD.Load<PackedScene>(SchoolRoom.RegionPath).Instantiate<Region3D>();
            AddChild(region);
            school.Room = SchoolRoom.Of(region);
            CheckLayout();
            await CheckMathClassThenRecess();
            await CheckRecessThenDuty();
            RemoveChild(region);
            region.Free();
            await play.Frames(10);
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("School day checks passed: layout, scene 3 to 4, scene 4 to 5, notebook pages.");
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

    private Vector3 Desk(string id) => school.Where(id);

    // What the scenes put on the desks is laid out by the desks: the boxes for a row of chairs, the chairs of section Ⅱ, the board.
    private void CheckLayout()
    {
        for (var row = 0; row < room.RowBoxes.Count; row++)
            Check(Mathf.Abs(room.RowBoxes[row].Position.Z - (Desk($"deskI{row}0").Z + .5f)) < .001f, $"the box of row {row} lies where the chairs of that row stand");
        foreach (var number in Enumerable.Range(0, 48).Select(n => $"{n / 6}{n % 6}").Append("49"))
        {
            var desk = Desk($"deskII{number}");
            var chair = school.Where($"chairII{number}");
            Check(Mathf.Abs(chair.X - desk.X) < .001f && Mathf.Abs(chair.Y - 1.23f) < .001f && Mathf.Abs(chair.Z - (desk.Z + .12f)) < .001f,
                $"the chair {number} of section Ⅱ is upside down on its desk");
        }
        var board = (SliceBoundsResult.Bounds)room.Region.World!.GetSliceBounds(new EntityId("Blackboard"), new SectionId("Ⅰ"));
        var box = room.Chalkboard.GetNode<CollisionShape3D>("Shape");
        var half = ((BoxShape3D)box.Shape).Size / 2;
        var center = room.Chalkboard.Position + box.Position;
        Check(Mathf.Abs(center.X - half.X - (float)board.Min.X) < .01f && Mathf.Abs(center.X + half.X - (float)board.Max.X) < .01f
            && Mathf.Abs(center.Y - half.Y - (float)board.Min.Y) < .01f && Mathf.Abs(center.Y + half.Y - (float)board.Max.Y) < .01f
            && center.Z - half.Z >= (float)board.Max.Z - .001f && center.Z - half.Z < (float)board.Max.Z + .05f,
            "the box that the crosshair finds the board by lies on the front of the board");
    }

    // Scene 3 ends with her sitting, the teacher where he stopped with the checkpoint of scene 3, and the prism usable.
    private async Task CheckMathClassThenRecess()
    {
        var math = new MathClass { Region = region };
        math.Finished += () => finished++;
        AddChild(math);
        var teacher = room.Teacher.GetNode<Teacher>("Teacher");
        await school.Fast(() => room.Region.PrismEnabled, 150, "the lecture must end");
        foreach (var entity in new[] { "clockII", "deskII49", "chimneyII1" }) room.Player.EmitSignal(FirstPersonPlayer.SignalName.LensMarked, entity);
        await school.Fast(() => finished == 1, 60, "scene 3 must end with the three differences");
        Check(room.Player.Seated && room.Region.PrismEnabled && teacher.Checkpoint is not null && !room.Teacher.Busy, "scene 3 leaves her seated, the prism usable and the teacher standing with its checkpoint");
        Check(math.GetNodeOrNull("Region") is null, "the math class played in the region it was given, and did not load its own");

        var recess = new Recess { Region = region };
        recess.Finished += () => finished++;
        AddChild(recess);
        await play.Frames(3);
        Check(teacher.Checkpoint is null, "scene 4 plays in the same region, and the teacher no longer sends her back to scene 3");
        await school.Fast(() => room.Classmates.All(person => !person.Visible), 40, "the class must leave");
        await school.DoTheTricks();
        await school.Fast(() => !room.Player.Seated, 30, "she must be free to go to the window");
        await school.Turn(new StandUp(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil), new("Ⅱ")));
        await school.Fast(() => finished == 2, 60, "scene 4 must end");
        Check(room.Player.Seated && room.IsUpright(SchoolRoom.WindowPencil), "scene 4 leaves her seated, and the pencil standing in the window rail");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([1, 2, 3]), "the notebook has the pages of scenes 3 and 4");
        RemoveChild(math);
        math.Free();
        RemoveChild(recess);
        recess.Free();
    }

    // Scene 5 takes the pencil as scene 4 left it, without standing it up again.
    private async Task CheckRecessThenDuty()
    {
        var before = room.State(SchoolRoom.WindowPencil).Pose;
        var duty = new Duty { Region = region };
        duty.Finished += () => finished++;
        AddChild(duty);
        await school.Fast(() => room.Broom.CollisionLayer != 0, 40, "the chores must start");
        Check(room.State(SchoolRoom.WindowPencil).Pose == before && room.IsUpright(SchoolRoom.WindowPencil), "the pencil was not stood up again");
        Check(room.Region.PrismEnabled && !room.Player.Seated && room.Classmates.All(person => !person.Visible) && !room.Teacher.Visible, "she is up with the prism, and the class is gone");

        room.Broom.Interact();
        room.Chalkboard.Interact();
        foreach (var row in room.RowBoxes) row.Interact();
        Check(room.Region.World!.DriveDevice(new("sash3"), .69) is DriveDeviceResult.Driven && room.Region.World.DriveDevice(new("sash1"), .69) is DriveDeviceResult.Driven { Position: < .69 },
            "the first window stops at the pencil in the rail");
        await school.Turn(new LayDown(new(SchoolRoom.Actor), new(SchoolRoom.WindowPencil)));
        foreach (var window in SchoolRoom.Windows) room.Region.World.DriveDevice(new(window), .69);
        await school.Fast(() => room.Player.Hud.UnlockedPages.Contains(4), 30, "the page of the glowing things must be drawn");
        room.DoorLock.Interact();
        await school.Fast(() => finished == 3, 20, "scene 5 must end");
        Check(room.Player.Hud.UnlockedPages.OrderBy(page => page).SequenceEqual([1, 2, 3, 4]), "the notebook has the pages of all three scenes");
        RemoveChild(duty);
        duty.Free();
    }
}
