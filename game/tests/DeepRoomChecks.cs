using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// Scene 9 played through on the real inputs. The main route is the one a player takes who has just crossed: she waits a
/// second, picks the cat up, looks at the place where the seam was and then at her own room, and carries the cat when the
/// grazer comes. The other runs have her walk out of the gaps the grazer leaves, stand where the grazer goes, and keep the
/// scene after scene 8 that still hosts the room. Time is sped up while nothing is being watched, and the checks count in
/// the seconds the game counts. Pass the names of the runs after <c>--</c> to play only those: route, gaps, stand, hosted.
/// </summary>
public partial class DeepRoomChecks : Node
{
    private const float Floor = .02f;
    private static readonly Vector3 S1 = new(7.1f, Floor, 4.3f);
    private static readonly Vector3 S2 = new(6.25f, Floor, 4.3f);
    private static readonly Vector3 Here = new(4.5f, Floor, 4.3f);
    private static readonly string[] Meals = [DeepGrazer.Door, DeepGrazer.Corner, DeepGrazer.Frame, DeepGrazer.Window];

    private TurningFixture play = null!;
    private DeepRoom room = null!;
    private RoomQueries ask = null!;
    private Cat cat = null!;
    private PrismView prism = null!;
    private readonly List<string> eaten = [], shown = [];
    private double elapsed, panicAt, grazingAt, fledAt, screamAt, screamDistance, screamAim;
    private bool finished, shrieking;

    private string Said => room.Home.Hud.SubtitleText;
    private Character3D CatBody => room.CatBody;

    public override void _PhysicsProcess(double delta)
    {
        // The seconds of the game: a physics frame is as long as the speed of time makes it.
        elapsed += delta;
        if (ask is null) return;
        foreach (var meal in Meals.Where(meal => !eaten.Contains(meal) && !ask.Exists(meal))) eaten.Add(meal);
        // Every line that was on the screen, in the order it was.
        if (Said != "" && (shown.Count == 0 || shown[^1] != Said)) shown.Add(Said);
        if (room.CatFled && fledAt < 0) fledAt = elapsed;
    }

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        var runs = OS.GetCmdlineUserArgs();
        try
        {
            if (runs.Length == 0 || runs.Contains("route")) await CheckMainRoute();
            if (runs.Length == 0 || runs.Contains("gaps")) await CheckGaps();
            if (runs.Length == 0 || runs.Contains("stand")) await CheckStandingOnTheRoute();
            if (runs.Length == 0 || runs.Contains("hosted")) await CheckHostedByScene8();
            GD.Print("Deep room checks passed: the way in, the lines in order, the lens, the seam cut, the grazer's bites in order, the gaps in the walls, "
                + "the cat torn from her arms before the scream with a chase between, the black screen and the title, and the scene after scene 8.");
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

    private async Task CheckMainRoute()
    {
        await Start();
        await Until(() => play.Region.CurrentSection == "Ⅱ", 3, "she must have followed the cat into section II");
        Check(CatBody.Section == "Ⅲ" && CatBody.Mirrored && room.Now == DeepRoom.Stage.Following, "the cat sits in section III, a mirror image, as scene 8 leaves it");
        Check(!ask.IsSevered("s2"), "the seam is not cut yet");
        await Cross(S2, "Ⅲ");
        await Until(() => room.Entries(DeepRoom.Stage.Arrived) > 0, 2, "the scene must know she has arrived");
        Check(play.Actions.Mirrored && play.Region.GetNode<MirrorView>("Generated/Travel/Mirror").Flipped, "the whole picture is turned over");
        await Until(() => shown.Any(line => line.Contains("咦，反过来了")), 6, "she must say it is the wrong way round");
        await Pause(1);
        // She picks the cat up a second after she has arrived, with the lines of arrival still to come.
        var arrival = shown.Count;
        var pickup = CatBody.GetNode<Interactable3D>("Pickup");
        pickup.Interact();
        Check(cat.Held, "she has the cat in her arms");
        await Until(() => room.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        Check(ask.Bounds(DeepGrazer.Id, "Ⅲ") is null && play.Region.HasNode($"Generated/Ⅲ/Geometry/{DeepGrazer.Door}"), "nothing of the grazer is in the room yet, and the door is");
        play.Player.PutDown!();
        pickup.Interact();
        Check(cat.Held && room.Entries(DeepRoom.Stage.Panic) == 1, "putting the cat down and picking it up again does not start the scene again");
        await Until(() => ask.IsSevered("s2"), 3, "the first bite must cut the seam");
        await play.Frames(3);
        Check(play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s2/Cut"), "the lens shows a plainly cut end of the seam");
        // She looks at where the seam was, and then at her own room.
        play.LookAt(Here, new(6.25f, .2f, 4.3f));
        Button(MouseButton.Right, true);
        await Until(() => room.GetNode<AudioStreamPlayer>("heartbeat").Playing && room.GetNode<AudioStreamPlayer>("breathing").Playing, 20, "her heart and breath must be heard");
        Check(prism.TargetSection == "Ⅱ", "the lens shows section II");
        Button(MouseButton.WheelDown, true);
        await play.Frames(2);
        Check(prism.TargetSection == "Ⅰ", "the lens shows her own room");
        play.LookAt(Here, new(7.3f, 1.2f, 3));
        var labels = play.Region.Props("Ⅰ")!.GetChildren().OfType<Label3D>().Where(label => (label.Layers & 2) != 0).Select(label => label.Text).ToArray();
        Check(labels.Any(text => text.Contains("台灯亮着")) && labels.Any(text => text.Contains("笔记本摊开着")) && labels.Any(text => text.Contains("9:40")),
            $"the lamp, the notebook and the clock are in the lens: {string.Join(", ", labels)}");
        await Until(() => shown.Any(line => line.Contains("家里没有人")), 40, "she must call for her father and be answered by nothing");
        Button(MouseButton.Right, false);
        // The lines came in the order things happened, none of them lost, and the lines of arrival stopped when she picked the cat up.
        InOrder("好了，回家吧", "咦……缝呢", "只剩一截线头", "回不去了", "那是我的房间", "爸爸！", "爸爸——！", "家里没有人");
        Check(shown.Skip(arrival).All(line => !line.Contains("天光") && !line.Contains("坐在屋子中间")), "the lines of arrival are not said after she has picked the cat up");
        // There is no seam to cross.
        play.LookAt(S2, new(5.5f, .5f, 4.3f));
        Button(MouseButton.Right, true);
        Button(MouseButton.WheelUp, true);
        await play.Frames(2);
        Check(prism.TargetSection == "Ⅱ" && !play.Region.Travel!.CanCross, "the lens shows section II, and there is no seam to cross");
        Button(MouseButton.Left, true);
        Button(MouseButton.Left, false);
        await Until(() => Said.Contains("咬断"), 10, "she must be told the seam is cut");
        Button(MouseButton.Right, false);
        Check(play.Region.GetNode<MirrorView>("Generated/Travel/Mirror").Flipped, "she is still turned over");
        await CheckGrazer();
        await CheckEnd();
        await End();
    }

    private async Task CheckGrazer()
    {
        var music = room.GetNode<GrazerMusic>("Music");
        play.LookAt(Here, new(8, 2, 3));
        Engine.TimeScale = 4;
        await Until(() => music.Levels.Bass > 0, 30, "the music must give warning from far off");
        await Until(() => ask.Bounds(DeepGrazer.Id, "Ⅲ") is not null, 60, "the grazer must show up in the room");
        var first = ask.Bounds(DeepGrazer.Id, "Ⅲ")!.Value.Size.Length();
        Check(music.Beats > 0 && music.Levels.Rhythm > 0, "its scraping is heard before it is seen");
        await Until(() => eaten.Contains(DeepGrazer.Door), 60, "it must bite the extra door");
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
        await Until(() => room.CatFled, 90, "the cat must tear loose");
        Check(!cat.Held && play.Player.PutDown is null && !cat.Holdable, "the cat is out of her arms and cannot be picked up again");
        await Until(() => room.Entries(DeepRoom.Stage.Scream) > 0, 60, "she must scream once it is on her");
        CheckChase();
        await Until(() => Flat(CatBody.Position - play.Player.Position).Length() > 1.5f, 1.5, "the cat must run from her");
        await Until(() => room.Entries(DeepRoom.Stage.Black) > 0, 10, "the screen must cut to black");
        Check(room.Cut is not null && !room.HasNode("Music") && room.GetChildren().OfType<AudioStreamPlayer>().All(sound => !sound.Playing),
            "the picture is black and every sound has stopped");
        Engine.TimeScale = 1;
        await Until(() => room.Cut!.TitleVisible, 6, "the title must come up");
        Check(room.Cut!.TitleText != "" && !room.GetChildren().OfType<AudioStreamPlayer>().Any(sound => sound.Playing), "the title is up in silence");
        await Until(() => finished, 8, "the scene must end");
        InOrder("刮擦声", "冒出几块", "那扇多出来的门", "墙角也被", "窗框没了");
    }

    // The cat tears loose first, a chase of at least one leg follows, and she screams with the mouth on her and her eyes on it.
    private void CheckChase()
    {
        Check(shrieking && room.Entries(DeepRoom.Stage.Scream) == 1, "the scream sounds as the scene enters its stage");
        Check(fledAt >= 0 && screamAt - fledAt >= 3, $"the cat tears loose at {fledAt:F1} s and she screams at {screamAt:F1} s: there must be a chase between");
        Check(screamDistance < 4, $"the grazer is on her when she screams: {screamDistance:F1} m");
        Check(screamAim < .3, $"she looks at the grazer when she screams: {screamAim:F2} rad off");
    }

    private void InOrder(params string[] lines)
    {
        var at = -1;
        foreach (var line in lines)
        {
            var next = shown.FindIndex(Math.Max(at + 1, 0), said => said.Contains(line));
            Check(next > at, $"the line '{line}' must come after the lines before it; the lines were: {string.Join(" | ", shown)}");
            at = next;
        }
    }

    private async Task Start()
    {
        room = GD.Load<PackedScene>(DebugScenes.Find("deep-room")!).Instantiate<DeepRoom>();
        AddChild(room);
        await Bind(room);
    }

    private async Task Bind(DeepRoom scene)
    {
        (finished, shrieking) = (false, false);
        (panicAt, grazingAt, fledAt, screamAt) = (-1, -1, -1, -1);
        eaten.Clear();
        shown.Clear();
        room = scene;
        room.Finished += () => finished = true;
        room.StageEntered += stage =>
        {
            if (stage == DeepRoom.Stage.Panic) panicAt = elapsed;
            if (stage == DeepRoom.Stage.Grazing) grazingAt = elapsed;
            if (stage != DeepRoom.Stage.Scream) return;
            (shrieking, screamAt) = (room.GetNode<AudioStreamPlayer>("scream").Playing, elapsed);
            screamDistance = room.Region!.Grazers!.Distance(DeepGrazer.Id);
            var eyes = play.Player.Camera;
            screamAim = (-eyes.GlobalBasis.Z).AngleTo(GrazerAt() - eyes.GlobalPosition);
        };
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

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private async Task Pause(double seconds)
    {
        var until = elapsed + seconds;
        while (elapsed < until) await play.Frames(1);
    }

    private async Task Until(Func<bool> condition, double seconds, string message)
    {
        var limit = elapsed + seconds;
        while (!condition())
        {
            Check(elapsed < limit, $"{message} (not within {seconds} s; the scene is in {room?.Now})");
            await play.Frames(1);
        }
    }
}
