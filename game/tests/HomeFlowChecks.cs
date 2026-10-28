using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Audio;
using Game.Characters;
using Game.Debugging;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Scenes 1 and 7 played through: the player walks the flat on the real physics, finds each prop with the crosshair,
/// and presses E on it.
/// </summary>
public partial class HomeFlowChecks : Node
{
    private const int FramesPerSecond = 60;

    private Node scene = null!;
    private FirstPersonPlayer player = null!;

    public override async void _Ready()
    {
        try
        {
            await CheckMorning();
            await CheckEvening();
            GD.Print("Home flow checks passed: morning (alarm, note, teeth, bag, door, stairs) and evening (note, clip, meal, cat, forecast, homework, notebook).");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.ActionRelease("move_forward");
            Input.ActionRelease("run");
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private async Task CheckMorning()
    {
        var morning = await Start<HomeMorning>("home-morning");
        var finished = false;
        morning.Finished += () => finished = true;
        var hud = player.Hud;
        var alarm = Sound("alarm");
        Check(morning.AlarmRinging, "the alarm rings at 6:50");
        await Until(() => alarm.Playing && Sound("breathing").Playing, 2, "the alarm must sound and her father must snore");
        Button(MouseButton.Right, true);
        await Frames(2);
        Check(!player.PrismRaised && hud.HintText.Contains("棱镜"), "the prism cannot be raised at home in the morning");
        Button(MouseButton.Right, false);
        Press(Key.N);
        await Frames(2);
        Check(!hud.NotebookOpen, "the notebook cannot be opened in the morning");
        // The bedroom, the living room around the dining set, her father's room, the bathroom and the kitchen can be walked
        // to, one after the other, through their doors.
        await Walk((3.85f, 5f), (3.85f, 6.9f), (8.2f, 6.9f), (9.25f, 6.9f), (9.25f, 5.6f), (9.25f, 6.9f), (9.05f, 7.9f), (9.05f, 9.4f),
            (9.05f, 8.5f), (4.05f, 8.48f), (4.05f, 9.4f), (4.05f, 8.48f), (3.5f, 7.45f));
        await Refused("闹钟还在响。");
        await Use("Clock");
        Check(!morning.AlarmRinging && !alarm.Playing, "the alarm stops");
        await Refused("牙还没刷。");
        hud.ClearText();
        await Use("Father");
        Check(hud.SubtitleText.Contains("爸爸睡得很沉"), "her father is asleep");
        await Use("Sink");
        Check(morning.TeethBrushed, "she brushes her teeth in front of the mirror");
        await Refused("早饭钱还在桌上。");
        await Use("Money");
        Check(morning.MoneyTaken && !Prop("Money").Visible, "she takes the two yuan");
        await Refused("书包还在凳子上。");
        await Use("Backpack");
        Check(morning.HasBackpack && morning.Missing is null && !morning.NoteWritten, "the note is hers to answer, not a step before the bag is");
        hud.ClearText();
        await Use("Note");
        var paper = Prop("Note").GetNode<Node3D>("Paper");
        Check(morning.NoteWritten && Mathf.Abs(paper.Rotation.X - Mathf.Pi) < .01f, "E turns the note over");
        Check(paper.GetNode<Label3D>("Back").Visible && paper.GetNode<Label3D>("Back").Text == "爸，我上学去了。"
            && !paper.GetNode<Label3D>("Front").Visible, "she writes the answer on the back");
        Check(Heard("爸，我上学去了。", 10), "the written line is shown");
        await Use("FrontDoor");
        Check(morning.DoorOpen && Prop("FrontDoor").CollisionLayer == 0, "the door opens");
        await Walk((3.5f, 7.45f), (1.9f, 7.45f), (1.9f, 9.7f));
        Check(!finished, "she is still in the flat's corridor until she goes down");
        await Use("StairDoor");
        Check(finished && Prop("StairDoor").CollisionLayer == 0, "going down the stairs ends the scene, once");
        await End();
    }

    private async Task CheckEvening()
    {
        var evening = await Start<HomeEvening>("home-evening");
        var finished = false;
        evening.Finished += () => finished = true;
        var hud = player.Hud;
        Check(hud.NotebookAvailable && hud.UnlockedPages.SequenceEqual([1, 2, 3, 4]), "the notebook is open to her from here, with the pages of the school");
        Press(Key.N);
        await Frames(2);
        Check(hud.NotebookOpen, "N opens the notebook");
        Press(Key.N);
        await Frames(2);
        Check(!hud.NotebookOpen, "N closes the notebook");
        Button(MouseButton.Right, true);
        await Frames(2);
        Check(!player.PrismRaised && hud.HintText.Contains("棱镜"), "the prism cannot be raised at home in the evening either");
        Button(MouseButton.Right, false);
        foreach (var name in new[] { "Father", "Money", "Backpack", "FrontDoor", "Sink" })
            Check(!Prop(name).Visible && Prop(name).CollisionLayer == 0, $"{name} belongs to the morning");
        Check(Prop("Clock").Visible && Prop("Clock").CollisionLayer == 0, "the alarm clock stays on the bedside table and cannot be used");
        await Walk((1.9f, 7.45f), (3.6f, 7.45f), (3.85f, 6.9f), (5.8f, 6.9f));
        hud.ClearText();
        await Use("Note");
        Check(evening.NoteRead && Heard("饭在锅里，热一下再吃。作业写完早点睡。——爸", 10), "the new note says where the meal is");
        await Use("HairClip");
        Check(evening.ClipTaken && !Prop("HairClip").Visible, "she takes the hair clip");
        await Walk((8.2f, 6.9f), (9f, 7.6f));
        hud.ClearText();
        await Use("Tv");
        Check(evening.ForecastHeard && Heard("电视：明天白天，晴转多云，偏南风二到三级。", 10), "the weather forecast is on");
        hud.ClearText();
        Prop("Desk").Interact();
        Check(!evening.HomeworkDone && hud.SubtitleText == "先把饭热上吧。", "the homework waits for the meal");
        await Walk((8.2f, 8.48f), (5.8f, 8.48f), (4.05f, 8.48f), (4.05f, 9.4f));
        await Use("Stove");
        Check(!evening.Heated, "the meal takes a while to heat");
        await Until(() => evening.Heated, 6, "the meal must be hot");
        Check(Prop("Stove").GetNode<Node3D>("Steam").Visible && !Prop("Stove").GetNode<Node3D>("Flame").Visible, "the pot steams and the fire is off");
        hud.ClearText();
        Prop("Desk").Interact();
        Check(!evening.HomeworkDone && hud.SubtitleText == "煤球还没吃呢。", "the homework waits for the cat");
        await Walk((4.05f, 8.48f), (6.15f, 8.48f));
        await Use("Sausage");
        // She picks the cat up on its way to its half and puts it down: the sausage is still there, and she sends it again.
        await Frames(20);
        var pickup = evening.CatBody.GetNode<Interactable3D>("Pickup");
        pickup.Interact();
        await Frames(20);
        Check(evening.CatBody.GetNode<Cat>("Cat").Held && !evening.CatFed, "the cat is in her arms before it has eaten");
        player.PutDown?.Invoke();
        await Frames(20);
        Check(!evening.CatFed && Prop("Sausage").Visible, "the sausage stays while the cat has not eaten");
        await Use("Sausage");
        await Until(() => evening.CatFed, 20, $"the cat must come for its half; it stands at {evening.CatBody.Position}, the player at {player.Position}");
        Check(!Prop("Sausage").Visible, "the sausage is gone once it is eaten");
        Check(evening.CatBody.Section == "Ⅰ" && Flat(evening.CatBody.Position - new Vector3(5.8f, 0, 8.5f)).Length() < .6f,
            "the cat eats at the dining table");
        await Walk((4.05f, 8.48f), (3.85f, 6.9f), (3.85f, 5.2f), (6.5f, 3.3f));
        await Use("Desk");
        Check(!finished, "the homework takes a few minutes");
        await Until(() => finished, 15, "the homework must end the scene");
        Check(evening.HomeworkDone && hud.UnlockedPages.Contains(8), "the last page of the notebook is drawn");
        Check(hud.OpenNotebook(), "the notebook opens");
        while (hud.CurrentPage < 8) hud.TurnPage(1);
        Check(hud.CurrentPage == 8, "the last page can be turned to");
        hud.CloseNotebook();
        await End();
    }

    /// <summary>Presses E on the door, which she opens only when nothing is left to do, and checks what she says.</summary>
    private async Task Refused(string line)
    {
        player.Hud.ClearText();
        await Use("FrontDoor");
        Check(player.Hud.SubtitleText == line && Prop("FrontDoor").Visible, $"she does not leave yet and says \"{line}\"");
    }

    // Starts the scene as the debug entry of the given name does.
    private async Task<T> Start<T>(string entry) where T : Node
    {
        var path = DebugScenes.Find(entry);
        Check(path is not null, $"the debug entry {entry} must be registered");
        scene = GD.Load<PackedScene>(path).Instantiate<T>();
        AddChild(scene);
        await Frames(3);
        player = scene.GetNode<FirstPersonPlayer>("Region/Generated/Player");
        return (T)scene;
    }

    // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
    private async Task End()
    {
        RemoveChild(scene);
        scene.Free();
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
    }

    private Interactable3D Prop(string name) => scene.GetNode<Region3D>("Region").Props("Ⅰ")!.GetNode<Interactable3D>(name);

    private SectionSound Sound(string file) => scene.GetNode("Region").GetChildren().OfType<SectionSound>()
        .Single(sound => sound.Stream.ResourcePath.EndsWith($"{file}.wav"));

    /// <summary>Whether the line shows within the given seconds of subtitle time.</summary>
    private bool Heard(string text, double seconds)
    {
        for (var played = 0.0; played <= seconds; played += .25)
        {
            if (player.Hud.SubtitleText == text) return true;
            player.Hud._Process(.25);
        }
        return false;
    }

    /// <summary>Walks through the points on the real physics, running, with the view turned toward each.</summary>
    private async Task Walk(params (float X, float Z)[] route)
    {
        foreach (var (x, z) in route)
        {
            var goal = new Vector3(x, 0, z);
            for (var frames = 0; Flat(player.Position - goal).Length() > .25f; frames++)
            {
                Check(frames < 10 * FramesPerSecond, $"the player must reach ({x}, {z}) but stands at {player.Position}");
                var offset = goal - player.Position;
                player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
                Input.ActionPress("move_forward");
                Input.ActionPress("run");
                await Frames(1);
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        await Frames(1);
    }

    /// <summary>Finds the prop with the crosshair, as close as she stands, and presses E. When it cannot be found from where she stands, she is moved beside it.</summary>
    private async Task Use(string name)
    {
        await Aim(Prop(name));
        Press(Key.E);
        await Frames(2);
    }

    private async Task Aim(Interactable3D prop)
    {
        var expected = $"E  {prop.Prompt}";
        if (await Looks(prop.GlobalPosition, expected)) return;
        foreach (var distance in new[] { .9f, 1.5f })
            for (var side = 0; side < 8; side++)
            {
                var angle = side * Mathf.Pi / 4;
                var spot = prop.GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                spot.Y = .02f;
                if (!IsFree(spot)) continue;
                player.Position = spot;
                player.Velocity = Vector3.Zero;
                if (await Looks(prop.GlobalPosition, expected)) return;
            }
        Check(false, $"the crosshair must find {prop.Name} from some side");
    }

    private async Task<bool> Looks(Vector3 target, string expected)
    {
        var offset = target - (player.Position + new Vector3(0, 1.3f, 0));
        if (offset.Length() > 1.9f) return false;
        player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
        await Frames(2);
        return player.Hud.HintText == expected;
    }

    private bool IsFree(Vector3 spot)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new CapsuleShape3D { Radius = .22f, Height = 1.4f },
            Transform = new Transform3D(Basis.Identity, spot + new Vector3(0, .71f, 0)),
            CollisionMask = 1
        };
        return player.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private async Task Until(Func<bool> condition, double seconds, string message)
    {
        for (var frame = 0; !condition(); frame++)
        {
            Check(frame < seconds * FramesPerSecond, $"{message} (not within {seconds} s)");
            await Frames(1);
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
