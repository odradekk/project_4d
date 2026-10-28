using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Debugging;
using Game.Flow;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The cleaning after school, scene 5: the chores, the window that stops at the pencil standing in its rail, the glowing plate
/// in the lens, the pencil laid down, the window shut, the chairs of section Ⅱ back on the floor, and the door locked. Also
/// what she does out of order, and that the expected operations of the region are checked by the engine.
/// </summary>
public partial class DutyChecks : Node
{
    private const string ScenePath = "res://flow/duty.tscn";
    private const string Pencil = SchoolRoom.WindowPencil;
    // The window the pencil is in. Its pane has gone .214 m of its way when the pencil's plate stops it.
    private const string Stuck = "sash1";
    private const double StopsAt = SchoolOperationChecks.StopsAt;
    // Where she stands to push each window: in the lane beside the chairs, and where the middle of its pane is.
    private static readonly float[] LaneZ = [-3.6f, -1.375f, .525f, 3.375f];
    private static readonly float[] PaneZ = [-3.765f, -1.485f, .795f, 3.075f];

    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private Duty scene = null!;
    private int finished;

    private SchoolRoom room => school.Room;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        try
        {
#if DEBUG
            Check(DebugScenes.Find("duty") == ScenePath, "the cleaning has a debug entry");
#endif
            await Load();
            await CheckStart();
            await CheckChores();
            await CheckStuckWindow();
            await CheckLastWindowAndDoor();
            await Load();
            await CheckInOtherOrder();
            await Load();
            await CheckStalledWindowGoesBack();
            await Load();
            await CheckDeskmateStopped();
            await Load();
            await CheckRowWithAThingOnADesk();
            await Unload();
            SchoolOperationChecks.Run(this);
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("Duty checks passed: start, chores, stuck window, plate in the lens, last window, door, other order, stalled window, deskmate stopped, row with a thing on a desk, expected operations.");
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
        scene = GD.Load<PackedScene>(ScenePath).Instantiate<Duty>();
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

    // Pushes the window by the lower bar of its frame, which is what she can aim at; the pane has gone `along` metres of its way.
    private async Task Push(int window, float along = 0)
    {
        await school.Stand(new(-3.35f, 0, LaneZ[window]), new(-3.72f, .95f, PaneZ[window] + along));
        Press(Key.E);
    }

    private bool Exists(string entity) => room.Region.World!.ReadEntity(new(entity)) is EntityReadResult.Found;

    private async Task CheckStart()
    {
        // Played alone, the scene finds the school as scene 3 leaves it, and puts the class away and the pencil in the rail.
        await school.Fast(() => room.IsUpright(Pencil), 10, "the pencil must stand in the rail");
        var rail = room.State(Pencil).Pose.Position;
        Check(Math.Abs(rail.X - -3.72) < .01 && Math.Abs(rail.Z - -3.2) < .01, $"the pencil stands where it lay on the sill; it is at {rail}");
        Check(room.Classmates.All(person => !person.Visible) && !room.Teacher.Visible && room.Deskmate.Visible, "the class and the teacher have gone, and her desk mate is still there");
        Check(room.Door.Visible && room.Door.CollisionLayer == 1 && room.DoorLock.CollisionLayer == 0, "the door is shut, and offers nothing yet");
        Check(!room.Player.Seated && room.Region.PrismEnabled, "she can walk, with the prism in her hand");
        Check(!Exists("chairI00"), "there are no chairs in the room while her desk mate sits on his");
        await school.Fast(() => !room.Deskmate.Visible, 30, "her desk mate must run out");
        Check(school.Heard("帮我扫一下") && room.Door.Visible, "he asks her to sweep, and the door is shut behind him");
        await school.Fast(() => room.Broom.CollisionLayer != 0, 30, "the chores must start");
        Check(room.DoorLock.CollisionLayer == 4 && room.RowBoxes.All(row => row.CollisionLayer == 4), "the board, the broom, the rows and the door offer themselves");
        Check(Exists("chairI00") && Exists("chairI75"), "the chairs stand behind the desks");
        Check(!HitsAChair(room.Player.Position), "she does not stand in a chair");

        // Locking the door comes last.
        await school.Stand(new(-1.26f, 0, 4.5f), new(-1.26f, 1.1f, 5.55f));
        Check(room.Player.Hud.HintText == "E  锁门", $"the door offers to be locked; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.E);
        await school.Fast(() => school.Heard("还没弄完：扫地、擦黑板、倒扣椅子、关窗"), 5, "she must say what is left to do");
        Check(finished == 0, "the door is not locked with the chores undone");
    }

    // Whether anything of a chair is in the way of her body where her feet are.
    private bool HitsAChair(Vector3 feet)
    {
        var body = new PhysicsShapeQueryParameters3D
        {
            Shape = new CapsuleShape3D { Radius = .2f, Height = 1.4f },
            Transform = new Transform3D(Basis.Identity, feet + new Vector3(0, .7f, 0)),
            CollisionMask = 1
        };
        return room.Player.GetWorld3D().DirectSpaceState.IntersectShape(body)
            .Any(hit => ((Node)hit["collider"].AsGodotObject()).GetParent().Name.ToString().StartsWith("chair"));
    }

    private async Task CheckChores()
    {
        await school.Stand(new(-2.4f, 0, 4.85f), new(-3.3f, .55f, 5.25f));
        Check(room.Player.Hud.HintText == "E  扫地", $"the broom offers to sweep; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(2);
        Check(room.Broom.CollisionLayer == 0, "the broom is used once");
        await school.Fast(() => school.Heard("地扫干净"), 10, "she must say that the floor is swept");

        await school.Stand(new(-0.7f, 0, -4.4f), new(-0.7f, 1.6f, -5.62f));
        Check(room.Player.Hud.HintText == "E  擦黑板" && room.BoardText.Visible, $"the board offers to be wiped; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(2);
        Check(!room.BoardText.Visible && room.Chalkboard.CollisionLayer == 0, "the writing is gone, and the board is wiped once");

        // The chairs are put on the desks by the script, a row at a time, where section Ⅱ has them.
        for (var row = 0; row < room.RowBoxes.Count; row++)
        {
            var lane = -2.8f + .95f * row + .5f;
            await school.Stand(new(-1.26f, 0, lane), new(-.8f, .55f, lane));
            Check(room.Player.Hud.HintText == "E  倒扣这排的椅子", $"row {row} offers to have its chairs turned over; the hint is '{room.Player.Hud.HintText}'");
            Press(Key.E);
            await play.Frames(2);
            for (var column = 0; column < 6; column++)
            {
                Check(!Exists($"chairI{row}{column}"), $"the chair {row}{column} is no longer on the floor");
                var turned = room.State($"chairI{row}{column}U").Pose;
                var like = room.State($"chairII{row}{column}").Pose;
                Check(Math.Abs(turned.Position.X - like.Position.X) < 1e-4 && Math.Abs(turned.Position.Y - like.Position.Y) < 1e-4
                    && Math.Abs(turned.Position.Z - like.Position.Z) < 1e-4 && turned.Rotation == like.Rotation,
                    $"the chair {row}{column} is upside down on its desk, where section Ⅱ has it");
            }
        }
        await school.Fast(() => school.Heard("一模一样"), 10, "she must see that the room is like the other section");

        // The chair on her desk does not swallow what lies on it, and does not glow in the lens.
        var eraser = school.Where("eraser");
        var near = new OccupancyBox(SceneReader.PoseOf(new Transform3D(Basis.Identity, eraser), 0), new(.03, .03, .03, .006));
        var hits = ((OccupancyResult.Occupancy)room.Region.World!.QueryOccupancy(near, [])).Hits.Select(hit => hit.Entity.Value);
        Check(hits.All(entity => !entity.StartsWith("chair")), "the eraser on the desk is not inside a chair");
        var mask = (StandardMaterial3D)room.Region.GetNode<MeshInstance3D>("Generated/Ⅰ/Geometry/chairI41U/PrismMask").MaterialOverride;
        Check(mask.AlbedoColor == Colors.Black && room.Region.HasNode("Generated/Ⅰ/Geometry/chairI41U/Collision"), "a chair has no outline in the lens, and stops what walks");
    }

    private async Task CheckStuckWindow()
    {
        foreach (var window in new[] { 1, 2, 3 })
        {
            await Push(window);
            await school.Fast(() => room.IsShut($"sash{window + 1}"), 5, $"window {window + 1} must shut");
        }
        Check(room.Player.Hud.UnlockedPages.Count == 0, "the page of the glowing things is not drawn while a window is open");

        await Push(0);
        await school.Fast(() => school.Heard("动不了"), 5, "the window must say that it is stopped");
        var way = room.State(Stuck).DevicePosition!.Value;
        Check(Math.Abs(way - StopsAt) < .01 && !room.IsShut(Stuck), $"the window is stopped by the pencil's plate .214 m along; it is at {way}");
        Check(room.Player.Hud.HintText.Contains("举起棱镜"), $"a notice tells her to look with the prism; it says '{room.Player.Hud.HintText}'");
        Check(!school.Heard("发光的薄片"), "she has not seen the plate yet");

        // Nothing is to be seen in her section but a plate as thin as a finger; the lens shows it glowing.
        Button(MouseButton.Right, true);
        await school.Stand(new(-3.36f, 0, -2.9f), school.Where(Pencil));
        Check(room.Prism.SeenEntity() == Pencil, $"the lens sees the plate under the crosshair; it sees {room.Prism.SeenEntity()}");
        await school.Fast(() => school.Heard("发光的薄片"), 5, "she must recognise the plate");
        Check(room.Player.Hud.HintText.Contains("放倒"), $"the notice tells her to lay it down; it says '{room.Player.Hud.HintText}'");

        // F lays it down, in her section, and the window goes on to the end of its way with one more push.
        Press(Key.F);
        await play.Until(() => room.IsLying(Pencil), 4, "the pencil must lie down");
        Button(MouseButton.Right, false);
        await Push(0, (float)StopsAt);
        await school.Fast(() => room.IsShut(Stuck), 5, "the window must shut once the pencil lies");
    }

    private async Task CheckLastWindowAndDoor()
    {
        await school.Fast(() => !Exists("chairII00") && Exists("chairII00F"), 10, "the chairs of section Ⅱ must be back on the floor");
        await school.Fast(() => school.Heard("最后一扇窗") && school.Heard("放回地上"), 10, "she must look at the other section and say what she sees");
        for (var row = 0; row < 8; row++)
            for (var column = 0; column < 6; column++)
            {
                var floor = room.State($"chairII{row}{column}F").Pose;
                Check(!Exists($"chairII{row}{column}") && floor.Position.Y == 0 && floor.Rotation == SceneReader.Identity, $"the chair {row}{column} of section Ⅱ stands on the floor");
            }
        Check(!Exists("chairII49") && Exists("chairII49F"), "so does the chair on the extra desk");
        await school.Fast(() => room.Player.Hud.UnlockedPages.Contains(4), 15, "the page of the glowing things must be drawn");

        await school.Stand(new(-1.26f, 0, 4.5f), new(-1.26f, 1.1f, 5.55f));
        Check(finished == 0, "the scene is not over before the door is locked");
        Press(Key.E);
        await school.Fast(() => finished == 1, 20, "the scene must end when the door is locked");
        Check(school.Heard("门锁好了") && room.Door.CollisionLayer == 1 && room.DoorLock.CollisionLayer == 0, "the door is locked, and offers nothing more");
        Check(room.Player.Hud.UnlockedPages.SequenceEqual([4]), "the page is the only one drawn in this scene when it is played alone");
    }

    // She lays the pencil down before she tries any window: nothing stops them, and the room is the same at the end.
    private async Task CheckInOtherOrder()
    {
        await school.Fast(() => room.IsUpright(Pencil), 10, "the pencil must stand in the rail");
        await play.Frames(3);
        await school.Turn(new LayDown(new(SchoolRoom.Actor), new(Pencil)));
        foreach (var window in SchoolRoom.Windows)
            Check(room.Region.World!.DriveDevice(new(window), .69) is DriveDeviceResult.Driven { Contacts.Count: 0 }, $"{window} must shut with no pencil in its rail");
        await school.Fast(() => Exists("chairII00F"), 10, "the chairs of section Ⅱ must be on the floor after the last window");
        Check(!school.Heard("动不了") && !school.Heard("发光的薄片"), "she was never stopped, so she says nothing of a plate");
    }

    // The window that something stopped is pushed again while the pencil still stands: it goes back, and one more push sends it to the plate again.
    private async Task CheckStalledWindowGoesBack()
    {
        await school.Fast(() => room.IsUpright(Pencil), 10, "the pencil must stand in the rail");
        await Push(0);
        await school.Fast(() => school.Heard("动不了"), 10, "the window must stop");
        await Push(0, (float)StopsAt);
        await play.Until(() => room.State(Stuck).DevicePosition < .01, 5, "pushed again against the plate, the window must go back");
        await Push(0);
        await play.Until(() => Math.Abs(room.State(Stuck).DevicePosition!.Value - StopsAt) < .01 && !room.State(Stuck).Rotating, 5, "pushed once more, it must go to the plate again");
        Check(!room.IsShut(Stuck), "it is stopped, not shut");
    }

    // Her desk mate is told to do something else on his way out: the chores start all the same, with the door shut and lockable.
    private async Task CheckDeskmateStopped()
    {
        await school.Fast(() => !room.Door.Visible, 20, "the door must open for her desk mate");
        await play.Frames(20);
        room.Deskmate.Stop();
        await school.Fast(() => room.Door.Visible && room.Broom.CollisionLayer != 0, 30, "the door must shut and the chores start");
        Check(room.Deskmate.Visible && room.DoorLock.CollisionLayer == 4, "he is still in the room, and the door can be locked");
    }

    // Something lies where a chair of a row would be put on its desk: that chair stays on the floor, the others go up, and the row
    // is not done until the thing is taken away.
    private async Task CheckRowWithAThingOnADesk()
    {
        await school.Fast(() => room.Broom.CollisionLayer != 0, 40, "the chores must start");
        var eraser = room.State("eraser");
        var desk = school.Where("deskI11");
        var seat = eraser.Pose with { Position = new(desk.X, .77, desk.Z + .12, 0) };
        Check(room.Region.World!.PlaceEntity(new("eraser"), seat) is UpdateResult.Updated, "the eraser must be put where the seat of a chair would be");
        var lane = -2.8f + .95f + .5f;
        await school.Stand(new(-1.26f, 0, lane), new(-.8f, .55f, lane));
        Press(Key.E);
        await play.Frames(3);
        for (var column = 0; column < 6; column++)
        {
            var stays = column == 1;
            Check(Exists($"chairI1{column}") == stays && Exists($"chairI1{column}U") == !stays,
                $"the chair 1{column} is {(stays ? "still on the floor, since the eraser is in its way" : "upside down on its desk")}");
        }
        await school.Fast(() => school.Heard("桌上有东西"), 10, "she must say that something is on a desk");
        Check(room.RowBoxes[1].CollisionLayer == 4, "the row is not done, and still offers itself");

        // The thing is taken away, and the chair that was left goes up.
        Check(room.Region.World.PlaceEntity(new("eraser"), eraser.Definition.Pose) is UpdateResult.Updated, "the eraser must be put back");
        Press(Key.E);
        await play.Frames(3);
        Check(!Exists("chairI11") && Exists("chairI11U") && room.RowBoxes[1].CollisionLayer == 0, "the last chair of the row goes up, and the row is done");
    }
}
