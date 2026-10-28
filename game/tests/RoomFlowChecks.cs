using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Debugging;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Scene 8 played through in one run, on the real physics and the real inputs: the cat leaps into the seam, the wand is
/// flipped over the guard and stood up, the stool and the cabinet bring the board down, the ball bridges the pit, the nut's
/// ring is set up and the cat leaps through it into section III. The cat is picked up in the middle of some of its commands.
/// </summary>
public partial class RoomFlowChecks : Node
{
    private const float Floor = .02f;
    private static readonly Vector3 Seam = new(7.1f, Floor, 4.3f);

    private CatRoom room = null!;
    private FirstPersonPlayer player = null!;
    private PlayerActions actions = null!;
    private RoomQueries ask = null!;
    private Character3D cat = null!;
    private long ticks;
    private CatRoom.Stage? interrupt;
    private bool interrupted;

    public override void _PhysicsProcess(double delta) => ticks++;

    public override async void _Ready()
    {
        try
        {
            room = GD.Load<PackedScene>(DebugScenes.Find("cat-room")!).Instantiate<CatRoom>();
            AddChild(room);
            await Frames(3);
            player = room.Home.Player;
            actions = room.Home.Region.Actions!;
            ask = new RoomQueries(room.Home.Region);
            cat = room.CatBody;
            room.StageEntered += entered =>
            {
                if (entered != interrupt) return;
                interrupt = null;
                interrupted = true;
                cat.GetNode<Interactable3D>("Pickup").Interact();
            };
            await CheckIntro();
            await CheckFlipOverGuard();
            await CheckStandWand();
            await CheckCabinet();
            await CheckBoardAndPit();
            await CheckBall();
            await CheckRing();
            GD.Print("Room flow checks passed: intro, flip over the guard, wand, stool and cabinet, pit, ball, ring and leap into section III, "
                + "with the cat picked up on the way.");
            RemoveChild(room);
            room.Free();
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
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

    private async Task CheckIntro()
    {
        Check(room.Now == CatRoom.Stage.Watching && cat.Posture == Posture.Sitting && cat.Section == "Ⅰ", "the cat sits in the corner of section I at first");
        Check(room.Home.Hud.UnlockedPages.SequenceEqual([1, 2, 3, 4, 8]) && room.Home.Hud.NotebookAvailable, "she has the pages of the earlier scenes");
        Check(Flat(cat.Position - new Vector3(6.4f, 0, 4.3f)).Length() < .2f, $"the cat watches the corner from {cat.Position}");
        var pickup = cat.GetNode<Interactable3D>("Pickup");
        Look(new(4.8f, Floor, 4.9f), new(7.1f, .5f, 4.3f));
        Button(MouseButton.Right, true);
        // Carrying the cat, she can look at the seam as long as she likes; the cat leaps once she has put it down.
        pickup.Interact();
        await Frames(100);
        Check(room.Now == CatRoom.Stage.Watching && cat.GetNode<Cat>("Cat").Held, "the cat in her arms does not leap");
        // Picked up again the moment it leaps, it leaps again when put down. A leap lasts a few frames, so the pick-up is made
        // when the cat enters the stage and not when a poll happens to see it.
        PickUpAt(CatRoom.Stage.Entering);
        player.PutDown!();
        await Until(() => interrupted, 4, "the cat must leap at the seam once she looks at it");
        await Frames(5);
        Check(cat.GetNode<Cat>("Cat").Held && room.Now == CatRoom.Stage.Entering, "the cat is in her arms in the middle of its leap");
        player.PutDown!();
        await Until(() => room.Entries(CatRoom.Stage.Perched) > 0, 6, "the cat must get into the seam all the same");
        Button(MouseButton.Right, false);
        Check(cat.Section == "Ⅱ" && Flat(cat.Position - new Vector3(3.85f, 0, 5.78f)).Length() < .2f && cat.Position.Y > 2.3f,
            $"the cat is on top of the cabinet in section II: {cat.Position}");
        Check(cat.Posture == Posture.Lying && !room.Home.Hud.UnlockedPages.Contains(5), "it lies there; she has not crossed yet");
    }

    private async Task CheckFlipOverGuard()
    {
        // From 0.67 m away, aiming through the gap between two rods at the yarn.
        Look(new(5.44f, Floor, 3.3f), new(5.44f, 1.09f, 2.63f));
        await Frames(3);
        Check(actions.Aimed == "wand", $"the crosshair finds the wand through the rods; it is on {actions.Aimed}");
        Check(ask.State("wand").Handedness == Handedness.Normal, "the wand is as it was made");
        Check(actions.Flip() is null, "the flip over the guard is accepted");
        await Until(() => !ask.State("wand").Rotating, 3, "the flip must end");
        await Until(() => ask.IsResting("wand"), 3, "the wand must come to rest");
        var wand = ask.State("wand");
        Check(wand.Handedness == Handedness.Mirrored && wand.Pose.Position.Z > 2.8f && wand.Pose.Position.Y < .2f && wand.Section == new SectionId("Ⅰ"),
            $"the wand lies mirrored on the floor on her side of the guard: {wand.Pose.Position}");
    }

    private async Task CheckStandWand()
    {
        Look(new(5.44f, Floor, 3.6f), new(5.44f, .06f, 2.89f));
        await Frames(3);
        Check(actions.Aimed == "wand", $"the wand lies where she can aim at it; the crosshair is on {actions.Aimed}");
        Check(actions.Use() is null && actions.Held == "wand", "she picks the wand up");
        await Walk(new(5f, Floor, 4f));
        Face(new(5f, Floor, 4f), new(0, 0, -1));
        await Frames(10);
        Check(actions.StandOrLay() is null, "the wand can be stood up toward section II");
        await Until(() => !ask.State("wand").Rotating, 3, "the stand-up must end");
        var yarn = ask.Bounds("wand", "Ⅱ", "yarn");
        Check(yarn is not null && ask.Bounds("wand", "Ⅰ", "yarn") is null, "only the yarn of the stood-up wand reaches section II");
        Near(yarn!.Value.GetCenter(), new(4.78f, 1f, 3.4f), "the yarn lies in section II at the height of her hand, 0.22 m back from it");
        // The cat on the cabinet looks at the yarn.
        await Frames(10);
        var toYarn = Flat(yarn.Value.GetCenter() - cat.Position).Normalized();
        Check((-cat.GlobalBasis.Z).Dot(toYarn) > .9f, "the cat on the cabinet looks at the yarn");
        // Shaking the handle moves the yarn; after some metres of it the notebook has the page.
        var start = player.Position;
        for (var i = 0; i < 6; i++)
        {
            player.Position = start + new Vector3(i % 2 == 0 ? .4f : 0, 0, 0);
            await Frames(4);
        }
        Check(room.Home.Hud.UnlockedPages.Contains(6), "the page about operating one section from another is in the notebook");
        player.Position = start;
        await Frames(4);
        Check(actions.Use() is null && actions.Held is null && ask.Bounds("wand", "Ⅱ", "yarn") is not null, "she lets the stood-up wand go and it stays");
    }

    private async Task CheckCabinet()
    {
        // The cat is picked up the moment it starts down the board.
        PickUpAt(CatRoom.Stage.Descending);
        // The stool is carried to the door and put down under the cabinet.
        Look(new(6.3f, Floor, 3.9f), new(6.3f, .45f, 3.3f));
        await Frames(3);
        Check(actions.Aimed == "stool" && actions.Use() is null && actions.Held == "stool", $"she picks the stool up; the crosshair is on {actions.Aimed}");
        await Walk(new(4.2f, Floor, 5.4f));
        Face(new(4.2f, Floor, 5.4f), new(0, 0, -1));
        await Frames(10);
        Check(actions.Use() is null && actions.Held is null, "she puts the stool down");
        await Until(() => ask.IsResting("stool"), 3, "the stool must stand on the floor");
        // The flap is out of reach from the floor.
        Look(new(4.2f, Floor, 5.4f), new(3.85f, 2.05f, 5.52f));
        await Frames(3);
        Check(actions.Aimed == "cabinet", $"the crosshair finds the flap; it is on {actions.Aimed}");
        room.Home.Hud.ClearText();
        Press(Key.E);
        await Frames(10);
        Check(ask.DevicePosition("cabinet") == 0 && room.Home.Hud.SubtitleText.Contains("太高了"), "from the floor the flap is too high: she says so and it stays");
        // On the stool it is within reach.
        Press(Key.Space);
        await Frames(10);
        Check(player.Position.Y > .45f && player.IsOnFloor(), $"Space puts her on the stool: she stands at {player.Position}");
        Look(player.Position, new(3.85f, 2.2f, 5.52f));
        await Frames(3);
        Check(actions.Aimed == "cabinet", $"the crosshair finds the flap from the stool; it is on {actions.Aimed}");
        Press(Key.E);
        await Until(() => ask.DevicePosition("cabinet") <= -Math.PI / 2 + 1e-9, 4, "the flap must open all the way");
        Check(ask.Bounds("cabinet", "Ⅱ", "plank") is { } plank && plank.Position.Y < .2f && plank.End.Y > 1.9f,
            "the board of section II lies from the hinge down to the floor");
        Press(Key.Space);
        await Frames(30);
        Check(player.Position.Y < .1f, "Space steps off the stool");
    }

    private async Task CheckBoardAndPit()
    {
        // The cat comes down for the yarn; picked up on its way, it is sent again. She is in section I and the cat in section II, so
        // the pick-up ends at once, the way a command is dropped when the cat is let go.
        await Until(() => interrupted, 6, "the cat must come down once the board is down and the yarn is near");
        await Frames(5);
        player.PutDown?.Invoke();
        await Until(() => room.Entries(CatRoom.Stage.AtPit) > 0, 10, "the cat must come down the board all the same");
        Check(cat.Section == "Ⅱ" && Flat(cat.Position - new Vector3(4.95f, 0, 4.58f)).Length() < .15f && cat.Position.Y < .5f, $"the cat waits at the foot of the board: {cat.Position}");
        // To section II through the seam: the page about seams, and a fall into the pit takes her back to where she stood.
        await Walk(new(7.1f, Floor, 4.3f));
        Look(Seam, new(7.1f, 1.3f, 3.3f));
        Button(MouseButton.Right, true);
        await Frames(3);
        Button(MouseButton.Left, true);
        await Frames(10);
        Button(MouseButton.Left, false);
        Check(room.Home.Region.CurrentSection == "Ⅱ" && room.Home.Hud.UnlockedPages.Contains(5), "she crosses into section II and the notebook has the page about seams");
        await Frames(10);
        player.Position = new(5f, .3f, 4.2f);
        await Until(() => player.Position.Y > -1 && player.IsOnFloor() && Flat(player.Position - Seam).Length() < .6f, 5, $"falling into the pit takes her back to where she stood; she is at {player.Position}");
        // In section II she picks the cat up at the foot of the board, walks off with it and puts it down: it goes back to the foot.
        cat.GetNode<Interactable3D>("Pickup").Interact();
        await Frames(5);
        Check(cat.GetNode<Cat>("Cat").Held && Flat(cat.Position - player.Position).Length() < 1f, $"the cat is in her arms: {cat.Position}");
        await Walk(new(6.1f, Floor, 4.3f));
        await Frames(5);
        Check(Flat(cat.Position - player.Position).Length() < 1f, "it goes with her");
        player.PutDown!();
        await Until(() => Flat(cat.Position - new Vector3(4.95f, 0, 4.58f)).Length() < .3f && room.Now == CatRoom.Stage.AtPit, 6, "put down away from the board, the cat must go back to its foot");
        await Walk(Seam);
        Look(Seam, new(7.1f, 1.3f, 3.3f));
        Button(MouseButton.Left, true);
        await Frames(10);
        Button(MouseButton.Left, false);
        Button(MouseButton.Right, false);
        Check(room.Home.Region.CurrentSection == "Ⅰ", "she crosses back");
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    /// <summary>Has the cat picked up the moment it enters a stage: the leap of a stage may be over between two looks at it.</summary>
    private void PickUpAt(CatRoom.Stage stage)
    {
        interrupt = stage;
        interrupted = false;
    }

    /// <summary>Puts her feet at a place with the eyes level, turned toward a direction.</summary>
    private void Face(Vector3 feet, Vector3 direction) => Look(feet, feet + new Vector3(0, 1.3f, 0) + direction);

    private void Look(Vector3 feet, Vector3 target)
    {
        player.Position = feet;
        player.Velocity = Vector3.Zero;
        var offset = target - (feet + new Vector3(0, 1.3f, 0));
        player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
    }

    // Half a metre per frame, so that what she carries is moved the way a walk moves it.
    private async Task Walk(Vector3 feet)
    {
        for (var frames = 0; Flat(player.Position - feet).Length() > .001f; frames++)
        {
            Check(frames < 200, $"she must be able to stand at {feet} but is at {player.Position}");
            player.Position = player.Position.MoveToward(new(feet.X, player.Position.Y, feet.Z), .5f);
            await Frames(1);
        }
    }

    private async Task Until(Func<bool> condition, double seconds, string message)
    {
        // Seconds of the game: physics frames at 60 a second, however many of them a frame of the process holds.
        var limit = ticks + (long)(seconds * 60);
        while (!condition())
        {
            Check(ticks < limit, $"{message} (not within {seconds} s)");
            await Frames(1);
        }
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
