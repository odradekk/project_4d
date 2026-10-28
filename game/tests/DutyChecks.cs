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
    private const string Stuck = "sash3";
    private const double StopsAt = .214;
    // Where she stands to push each window: in the lane between two rows of desks, and where the middle of its pane is.
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
            await Unload();
            CheckExpectedOperations();
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("Duty checks passed: start, chores, stuck window, plate in the lens, last window, door, other order, stalled window, deskmate stopped, expected operations.");
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
        await school.Stand(new(-3f, 0, LaneZ[window]), new(-3.72f, .95f, PaneZ[window] + along));
        Press(Key.E);
    }

    private async Task CheckStart()
    {
        // Played alone, the scene finds the school as scene 3 leaves it, and puts the class away and the pencil in the rail.
        await school.Fast(() => room.IsUpright(Pencil), 10, "the pencil must stand in the rail");
        var rail = room.State(Pencil).Pose.Position;
        Check(Math.Abs(rail.X - -3.72) < .01 && Math.Abs(rail.Z - 1.36) < .01, $"the pencil stands where it lay on the sill; it is at {rail}");
        Check(room.Classmates.All(person => !person.Visible) && !room.Teacher.Visible && room.Deskmate.Visible, "the class and the teacher have gone, and her desk mate is still there");
        Check(room.Door.Visible && room.Door.CollisionLayer == 1, "the door is shut");
        Check(!room.Player.Seated && room.Region.PrismEnabled, "she can walk, with the prism in her hand");
        await school.Fast(() => !room.Deskmate.Visible, 30, "her desk mate must run out");
        Check(school.Heard("帮我扫一下") && room.Door.Visible, "he asks her to sweep, and the door is shut behind him");
        await school.Fast(() => room.Broom.CollisionLayer != 0, 30, "the chores must start");
        Check(room.Door.CollisionLayer == (1 | 4) && room.Rows.All(row => row.CollisionLayer == 4), "the board, the broom, the rows and the door offer themselves");

        // Locking the door comes last.
        await school.Stand(new(-1.26f, 0, 4.5f), new(-1.26f, 1.1f, 5.55f));
        Check(room.Player.Hud.HintText == "E  锁门", $"the door offers to be locked; the hint is '{room.Player.Hud.HintText}'");
        Press(Key.E);
        await school.Fast(() => school.Heard("还没弄完：扫地、擦黑板、倒扣椅子、关窗"), 5, "she must say what is left to do");
        Check(finished == 0, "the door is not locked with the chores undone");
    }

    private async Task CheckChores()
    {
        await school.Stand(new(-2.8f, 0, 4.7f), new(-3.3f, .55f, 5.25f));
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

        // The chairs are put on the desks by the script, a row at a time, to where section Ⅱ has them.
        var chairs = room.Region.Props("Ⅰ")!.GetNode("Chairs");
        var upside = room.Region.Props("Ⅱ")!.GetNode("Chairs");
        Check(chairs.GetChildren().OfType<Node3D>().All(chair => chair.Transform.Origin.Y == 0) && ((Node3D)chairs).Visible, "the chairs stand behind the desks");
        for (var row = 0; row < room.Rows.Count; row++)
        {
            var lane = -2.8f + .95f * row + .5f;
            await school.Stand(new(-1.26f, 0, lane), new(-.8f, .55f, lane));
            Check(room.Player.Hud.HintText == "E  倒扣这排的椅子", $"row {row} offers to have its chairs turned over; the hint is '{room.Player.Hud.HintText}'");
            Press(Key.E);
            await play.Frames(2);
            Check(chairs.GetChildren().OfType<Node3D>().Where(chair => chair.Name.ToString().StartsWith($"ChairI{row}"))
                .All(chair => chair.Transform == ((Node3D)upside.GetNode("ChairII" + chair.Name.ToString()[6..])).Transform), $"the chairs of row {row} are upside down on the desks, where Ⅱ has them");
        }
        await school.Fast(() => school.Heard("一模一样"), 10, "she must see that the room is like the other section");
    }

    private async Task CheckStuckWindow()
    {
        foreach (var window in new[] { 0, 1, 3 })
        {
            await Push(window);
            await school.Fast(() => room.IsShut($"sash{window + 1}"), 5, $"window {window + 1} must shut");
        }
        Check(room.Player.Hud.UnlockedPages.Count == 0, "the page of the glowing things is not drawn while a window is open");

        await Push(2);
        await school.Fast(() => school.Heard("推不动了"), 5, "she must say that the window will not go");
        var way = room.State(Stuck).DevicePosition!.Value;
        Check(Math.Abs(way - StopsAt) < .01 && !room.IsShut(Stuck), $"the window is stopped by the pencil's plate .214 m along; it is at {way}");
        Check(!school.Heard("发光的薄片"), "she has not seen the plate yet");

        // Nothing is to be seen in her section but a plate as thin as a finger; the lens shows it glowing.
        Button(MouseButton.Right, true);
        await school.Stand(new(-3f, 0, 1.475f), school.Where(Pencil));
        Check(room.Prism.SeenEntity() == Pencil, $"the lens sees the plate under the crosshair; it sees {room.Prism.SeenEntity()}");
        await school.Fast(() => school.Heard("发光的薄片"), 5, "she must recognise the plate");
        Check(room.Player.Hud.HintText.Contains("放倒"), $"the notice tells her to lay it down; it says '{room.Player.Hud.HintText}'");

        // F lays it down, in her section, and the window goes on to the end of its way with one more push.
        Press(Key.F);
        await play.Until(() => room.IsLying(Pencil), 4, "the pencil must lie down");
        Button(MouseButton.Right, false);
        await Push(2, (float)StopsAt);
        await school.Fast(() => room.IsShut(Stuck), 5, "the window must shut once the pencil lies");
    }

    private async Task CheckLastWindowAndDoor()
    {
        var other = room.Region.Props("Ⅱ")!.GetNode("Chairs").GetChildren().OfType<Node3D>().ToArray();
        await school.Fast(() => other.All(chair => chair.Transform.Origin.Y == 0), 10, "the chairs of section Ⅱ must be back on the floor");
        await school.Fast(() => school.Heard("最后一扇窗") && school.Heard("放回地上"), 10, "she must look at the other section and say what she sees");
        await school.Fast(() => room.Player.Hud.UnlockedPages.Contains(4), 15, "the page of the glowing things must be drawn");
        Check(other.All(chair => chair.Transform.Basis.IsEqualApprox(Basis.Identity)), "the chairs stand upright");

        await school.Stand(new(-1.26f, 0, 4.5f), new(-1.26f, 1.1f, 5.55f));
        Check(finished == 0, "the scene is not over before the door is locked");
        Press(Key.E);
        await school.Fast(() => finished == 1, 20, "the scene must end when the door is locked");
        Check(school.Heard("门锁好了") && room.Door.CollisionLayer == 1, "the door is locked, and offers nothing more");
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
        var other = room.Region.Props("Ⅱ")!.GetNode("Chairs").GetChildren().OfType<Node3D>().ToArray();
        await school.Fast(() => other.All(chair => chair.Transform.Origin.Y == 0), 10, "the chairs of section Ⅱ must be on the floor after the last window");
        Check(!school.Heard("推不动了") && !school.Heard("发光的薄片"), "she was never stopped, so she says nothing of a plate");
    }

    // The window that something stopped is pushed again while the pencil still stands: it goes back, and one more push sends it to the plate again.
    private async Task CheckStalledWindowGoesBack()
    {
        await school.Fast(() => room.IsUpright(Pencil), 10, "the pencil must stand in the rail");
        await Push(2);
        await school.Fast(() => school.Heard("推不动了"), 10, "the window must stop");
        await Push(2, (float)StopsAt);
        await play.Until(() => room.State(Stuck).DevicePosition < .01, 5, "pushed again against the plate, the window must go back");
        await Push(2);
        await play.Until(() => Math.Abs(room.State(Stuck).DevicePosition!.Value - StopsAt) < .01 && !room.State(Stuck).Rotating, 5, "pushed once more, it must go to the plate again");
        Check(!room.IsShut(Stuck), "it is stopped, not shut");
    }

    // Her desk mate is told to do something else on his way out: the chores start all the same.
    private async Task CheckDeskmateStopped()
    {
        await school.Fast(() => !room.Door.Visible, 20, "the door must open for her desk mate");
        await play.Frames(20);
        room.Deskmate.Stop();
        await school.Fast(() => room.Door.Visible && room.Broom.CollisionLayer != 0, 30, "the door must shut and the chores start");
        Check(room.Deskmate.Visible, "he is still in the room");
    }

    // The engine plays the solutions written in the school region when it loads, and reports a step that ends otherwise.
    private void CheckExpectedOperations()
    {
        var region = GD.Load<PackedScene>("res://regions/school.tscn").Instantiate<Region3D>();
        AddChild(region);
        try
        {
            Check(region.Loaded && new SceneReader(region).Load() is LoadResult.Loaded, $"the school region must load: {region.Diagnostics}");
            var operation = region.GetNode<ExpectedOperationNode>("ExpectedPuzzleOperations/WindowStuck").Operation!;
            var stop = (ExpectedDeviceResource)operation.Steps[1];
            var lay = operation.Steps[2];
            stop.ExpectedPosition = .3;
            var wrong = new SceneReader(region).Load() as LoadResult.Invalid;
            stop.ExpectedPosition = StopsAt;
            Check(wrong is not null && wrong.Errors.Any(error => error.Code == LoadErrorCode.ExpectedOperationFailed && error.StepIndex == 1
                    && error.Source == new Source("ExpectedPuzzleOperations/WindowStuck", "Operation.Steps[1]")),
                "a window that stops somewhere else than the operation says is reported at its step");

            // Without laying the pencil down, the last push does not shut the window.
            operation.Steps.RemoveAt(2);
            var blocked = new SceneReader(region).Load() as LoadResult.Invalid;
            operation.Steps.Insert(2, lay);
            Check(blocked is not null && blocked.Errors.Any(error => error.Code == LoadErrorCode.ExpectedOperationFailed && error.StepIndex == 2),
                "a window pushed while the pencil still stands does not shut, and the operation says so");
            Check(new SceneReader(region).Load() is LoadResult.Loaded, "the operations are as they were");
        }
        finally
        {
            RemoveChild(region);
            region.Free();
        }
    }
}
