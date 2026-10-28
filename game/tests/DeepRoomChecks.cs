using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Audio;
using Game.Characters;
using Game.Debugging;
using Game.Flow;
using Game.Interaction;
using Game.Rendering;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Scene 9 played through on the real inputs, twice: once as a player who looks round, picks the cat up and puts it down,
/// looks at her own room and carries the cat when the grazer comes, and once as a player who keeps running away from it. Time
/// is sped up while nothing is being watched, which the checks count in the seconds the game counts.
/// </summary>
public partial class DeepRoomChecks : Node
{
    private const float Floor = .02f;
    private static readonly Vector3 S2 = new(6.25f, Floor, 4.3f);
    private static readonly string[] Meals = [DeepGrazer.Door, DeepGrazer.Corner, DeepGrazer.Frame, DeepGrazer.Window];

    private TurningFixture play = null!;
    private DeepRoom room = null!;
    private RoomQueries ask = null!;
    private Cat cat = null!;
    private PrismView prism = null!;
    private readonly List<string> eaten = [];
    private double elapsed;
    private bool finished;

    private string Said => room.Home.Hud.SubtitleText;
    private Character3D CatBody => room.CatBody;

    public override void _PhysicsProcess(double delta)
    {
        // The seconds of the game: a physics frame is as long as the speed of time makes it.
        elapsed += delta;
        if (ask is null) return;
        foreach (var meal in Meals.Where(meal => !eaten.Contains(meal) && !ask.Exists(meal))) eaten.Add(meal);
    }

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await CheckHandedOverRegion();
            await CheckMainRoute();
            await CheckRunningAway();
            GD.Print("Deep room checks passed: the region as it is handed over, the way in, the lens, the seam cut, the grazer's bites in order, "
                + "the cat torn from her arms, the scream, the black screen and the title, and a player who runs.");
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

    // A region that an earlier scene hosts and has left in its state is used as it is: nothing is loaded or moved.
    private async Task CheckHandedOverRegion()
    {
        var region = GD.Load<PackedScene>(HomeScene.RoomPath).Instantiate<Region3D>();
        AddChild(region);
        Check(region.Loaded, $"the room must load: {region.Diagnostics}");
        var scene = new DeepRoom { Region = region };
        AddChild(scene);
        await play.Frames(3);
        Check(scene.Region == region && scene.Home.Region == region && GetChildren().OfType<Region3D>().Count() == 1, "the scene uses the region it is given");
        Check(scene.Now == DeepRoom.Stage.Following && region.Character("cat").Section == "Ⅰ" && region.CurrentSection == "Ⅰ",
            "it leaves where she and the cat are as the earlier scene had them");
        RemoveChild(scene);
        scene.Free();
        RemoveChild(region);
        region.Free();
        await Settle();
    }

    private async Task CheckMainRoute()
    {
        await Start();
        await Until(() => play.Region.CurrentSection == "Ⅱ", 3, "she must have followed the cat into section II");
        Check(CatBody.Section == "Ⅲ" && CatBody.Mirrored && room.Now == DeepRoom.Stage.Following, "the cat sits in section III, a mirror image, as scene 8 leaves it");
        Check(!ask.IsSevered("s2") && ((SeamReadResult.Found)play.Region.World!.ReadSeam(new("s2"))).Seam.Open, "the seam to it is open");
        // Left alone she is told where to go.
        Engine.TimeScale = 5;
        await Until(() => Said.Contains("滚轮切到第Ⅲ页"), 60, "she must be told how to get through");
        Engine.TimeScale = 1;
        room.Home.Hud.ClearText();
        await Cross();
        await CheckArrival();
        await CheckPanic();
        await CheckGrazer();
        await CheckEnd();
        await End();
    }

    private async Task Cross()
    {
        play.LookAt(S2, new(7, 1, 4.3f));
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Button(MouseButton.WheelDown, true);
        await play.Frames(2);
        Check(prism.TargetSection == "Ⅲ", $"the lens shows section III; it shows {prism.TargetSection}");
        Button(MouseButton.Left, true);
        await play.Frames(3);
        Button(MouseButton.Left, false);
        Button(MouseButton.Right, false);
        Check(play.Region.CurrentSection == "Ⅲ", "she crosses into section III");
    }

    private async Task CheckArrival()
    {
        await play.Frames(3);
        Check(play.Actions.Mirrored && play.Region.GetNode<MirrorView>("Generated/Travel/Mirror").Flipped, "the whole picture is turned over");
        await Until(() => Said.Contains("咦，反过来了"), 3, "she must say it is the wrong way round");
        Check(room.Now == DeepRoom.Stage.Arrived && room.Entries(DeepRoom.Stage.Arrived) == 1, "the scene knows she has arrived");
        // Not picking the cat up leaves everything as it is.
        await Walk(new(5, Floor, 5));
        Engine.TimeScale = 5;
        await Until(() => Said.Contains("按 E 抱起"), 60, "she must be told to pick the cat up");
        Engine.TimeScale = 1;
        Check(room.Now == DeepRoom.Stage.Arrived && !ask.IsSevered("s2") && !room.GetNode<AudioStreamPlayer>("heartbeat").Playing, "nothing happens until she picks the cat up");
    }

    private async Task CheckPanic()
    {
        var pickup = CatBody.GetNode<Interactable3D>("Pickup");
        pickup.Interact();
        Check(cat.Held, "she has the cat in her arms");
        await Until(() => room.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        Check(ask.Bounds(DeepGrazer.Id, "Ⅲ") is null && play.Region.HasNode($"Generated/Ⅲ/Geometry/{DeepGrazer.Door}"), "nothing of the grazer is in the room yet, and the door is");
        await Until(() => ask.IsSevered("s2"), 3, "the first bite must cut the seam");
        await play.Frames(3);
        Check(play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s2/Cut"), "the lens shows a plainly cut end of the seam");
        // She puts the cat down and picks it up again on the way, and walks about the room.
        play.Player.PutDown!();
        Check(!cat.Held && play.Player.PutDown is null, "she puts the cat down");
        pickup.Interact();
        Check(cat.Held && room.Entries(DeepRoom.Stage.Panic) == 1, "picking it up again does not start the scene again");
        play.Player.PutDown!();
        foreach (var spot in new Vector3[] { new(2, Floor, 2), new(8, Floor, 6), new(5, Floor, 2.5f) }) await Walk(spot);
        // Looking at where the seam was: nothing to cross, and the body's sounds come up.
        await Walk(S2);
        play.LookAt(S2, new(5, .5f, 4.3f));
        Button(MouseButton.Right, true);
        await Until(() => room.GetNode<AudioStreamPlayer>("heartbeat").Playing && room.GetNode<AudioStreamPlayer>("breathing").Playing, 12, "her heart and breath must be heard");
        Check(prism.TargetSection == "Ⅱ" && !play.Region.Travel!.CanCross, "the lens shows section II, and there is no seam to cross");
        room.Home.Hud.ClearText();
        Button(MouseButton.Left, true);
        Button(MouseButton.Left, false);
        await Until(() => Said.Contains("咬断"), 3, "she must be told the seam is cut");
        Check(play.Region.GetNode<MirrorView>("Generated/Travel/Mirror").Flipped, "she is still turned over");
        // The lens on her own room.
        Button(MouseButton.WheelDown, true);
        await play.Frames(2);
        Check(prism.TargetSection == "Ⅰ", "the lens shows her own room");
        var labels = play.Region.Props("Ⅰ")!.GetChildren().OfType<Label3D>().Where(label => (label.Layers & 2) != 0).Select(label => label.Text).ToArray();
        Check(labels.Any(text => text.Contains("台灯亮着")) && labels.Any(text => text.Contains("笔记本摊开着")) && labels.Any(text => text.Contains("9:40")),
            $"the lamp, the notebook and the clock are in the lens: {string.Join(", ", labels)}");
        await Until(() => Said.Contains("爸爸"), 20, "she must call for her father");
        Button(MouseButton.Right, false);
    }

    private async Task CheckGrazer()
    {
        var music = room.GetNode<GrazerMusic>("Music");
        play.LookAt(new(2.5f, Floor, 5.5f), new(8, 2, 3));
        Engine.TimeScale = 4;
        await Until(() => music.Levels.Bass > 0, 30, "the music must give warning from far off");
        await Until(() => ask.Bounds(DeepGrazer.Id, "Ⅲ") is not null, 60, "the grazer must show up in the room");
        var first = ask.Bounds(DeepGrazer.Id, "Ⅲ")!.Value.Size.Length();
        Check(music.Beats > 0 && music.Levels.Rhythm > 0, "its scraping is heard before it is seen");
        await Until(() => eaten.Contains(DeepGrazer.Door), 60, "it must bite the extra door");
        // She has the cat in her arms from now on, until it tears loose.
        play.LookAt(new(3.5f, Floor, 5.5f), new(5.5f, 2, 1.9f));
        CatBody.GetNode<Interactable3D>("Pickup").Interact();
        Check(cat.Held, "she has the cat in her arms");
        var grown = ask.Bounds(DeepGrazer.Id, "Ⅲ")!.Value.Size.Length();
        Check(grown > first && grown > 4, $"what was a few lumps has swollen into one body: {first:F1} m across at first, {grown:F1} m at the first bite");
        await Until(() => eaten.Contains(DeepGrazer.Frame), 60, "it must bite the window frame");
        Check(eaten.IndexOf(DeepGrazer.Door) < eaten.IndexOf(DeepGrazer.Corner) && eaten.IndexOf(DeepGrazer.Corner) < eaten.IndexOf(DeepGrazer.Frame),
            $"it takes the door, the corner and the frame in that order: {string.Join(", ", eaten)}");
        Check(Meals.All(meal => !play.Region.HasNode($"Generated/Ⅲ/Geometry/{meal}") && !ask.Exists(meal)), "what it ate is gone from the engine and from the room, whole");
        Check(ask.Exists("DeepWalls") && ask.Exists("DeepDoor") && ask.Exists("DeepFloor"), "the rest of the room is still there");
    }

    private async Task CheckEnd()
    {
        var shrieking = false;
        room.StageEntered += stage =>
        {
            if (stage == DeepRoom.Stage.Scream) shrieking = room.GetNode<AudioStreamPlayer>("scream").Playing;
        };
        await Until(() => room.Entries(DeepRoom.Stage.Grazing) > 0, 30, "it must come for her after the frame");
        await Until(() => room.CatFled, 40, "the cat must tear loose");
        Check(!cat.Held && play.Player.PutDown is null && !cat.Holdable, "the cat is out of her arms and cannot be picked up again");
        await Until(() => room.Entries(DeepRoom.Stage.Scream) > 0, 60, "she must scream once it is on her");
        Check(shrieking && room.Entries(DeepRoom.Stage.Scream) == 1, "the scream sounds as the scene enters its stage");
        await Until(() => Flat(CatBody.Position - play.Player.Position).Length() > 1.5f, 1.5, "the cat must run from her");
        var eyes = play.Player.Camera;
        Check((-eyes.GlobalBasis.Z).Dot(GrazerAt() - eyes.GlobalPosition) > 0, "she looks at the grazer");
        await Until(() => room.Entries(DeepRoom.Stage.Black) > 0, 10, "the screen must cut to black");
        Check(room.Cut is not null && !room.HasNode("Music") && room.GetChildren().OfType<AudioStreamPlayer>().All(sound => !sound.Playing),
            "the picture is black and every sound has stopped");
        Engine.TimeScale = 1;
        room.Finished += () => finished = true;
        await Until(() => room.Cut!.TitleVisible, 6, "the title must come up");
        Check(room.Cut!.TitleText != "" && !room.GetChildren().OfType<AudioStreamPlayer>().Any(sound => sound.Playing), "the title is up in silence");
        await Until(() => finished, 8, "the scene must end");
    }

    // A player who never stands still: the grazer comes faster the longer it takes, and still gets her.
    private async Task CheckRunningAway()
    {
        await Start();
        await Until(() => play.Region.CurrentSection == "Ⅱ", 3, "she must have followed the cat into section II");
        await Cross();
        await play.Frames(3);
        CatBody.GetNode<Interactable3D>("Pickup").Interact();
        await Until(() => room.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        play.Player.PutDown!();
        Engine.TimeScale = 6;
        await Until(() => room.Entries(DeepRoom.Stage.Grazing) > 0, 150, "it must come for her");
        var (next, limit) = (0.0, elapsed + 200);
        while (room.Entries(DeepRoom.Stage.Scream) == 0)
        {
            Check(elapsed < limit, "the scene must end although she keeps away");
            if (room.Now == DeepRoom.Stage.Grazing && elapsed >= next)
            {
                // The corner of the room farthest from it.
                var corner = new[] { new Vector3(1.5f, Floor, 1.2f), new(8.5f, Floor, 1.2f), new(1.5f, Floor, 6.8f), new(8.5f, Floor, 6.8f) }
                    .MaxBy(spot => Flat(spot - GrazerAt()).Length());
                play.Player.Position = corner;
                next = elapsed + .5;
            }
            await play.Frames(1);
        }
        Check(room.CatFled && room.Entries(DeepRoom.Stage.Scream) == 1, "she is caught in the end, and the cat has torn loose");
        room.Finished += () => finished = true;
        await Until(() => finished, 60, "the scene must end");
        await End();
    }

    private async Task Start()
    {
        finished = false;
        eaten.Clear();
        room = GD.Load<PackedScene>(DebugScenes.Find("deep-room")!).Instantiate<DeepRoom>();
        AddChild(room);
        await play.Use(room.Region!);
        ask = new RoomQueries(room.Region!);
        cat = CatBody.GetNode<Cat>("Cat");
        prism = room.Region!.GetNode<PrismView>("Generated/Prism");
    }

    private async Task End()
    {
        Engine.TimeScale = 1;
        ask = null!;
        RemoveChild(room);
        room.Free();
        await Settle();
    }

    // The audio thread lets go of a stopped sound only after it has faded out, which takes time and not frames.
    private async Task Settle() => await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);

    private Vector3 GrazerAt()
    {
        var at = ask.State(DeepGrazer.Id).Pose.Position;
        return new((float)at.X, (float)at.Y, (float)at.Z);
    }

    // Half a metre per frame, so that what she carries is moved the way a walk moves it.
    private async Task Walk(Vector3 feet)
    {
        for (var frames = 0; Flat(play.Player.Position - feet).Length() > .001f; frames++)
        {
            Check(frames < 200, $"she must be able to stand at {feet} but is at {play.Player.Position}");
            play.Player.Position = play.Player.Position.MoveToward(new(feet.X, play.Player.Position.Y, feet.Z), .5f);
            await play.Frames(1);
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private async Task Until(Func<bool> condition, double seconds, string message)
    {
        var limit = elapsed + seconds;
        while (!condition())
        {
            Check(elapsed < limit, $"{message} (not within {seconds} s; the scene is in {room.Now})");
            await play.Frames(1);
        }
    }
}
