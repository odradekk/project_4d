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
/// Scenes 1 and 7 played through in the compound: the player walks the flat on the real physics, finds each prop with the
/// crosshair, and presses E on it. Scene 1 ends on the flight down from the second storey; in scene 7 the cat's way
/// between the rooms is clear.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class HomeFlowChecks : Node
{
    private const int FramesPerSecond = 60;
    private const string FrontDoor = "HomeDoors/FrontDoor/Leaf";

    private Node scene = null!;
    private FirstPersonPlayer player = null!;
    private bool finished;

    public override async void _Ready()
    {
        try
        {
            await CheckMorning();
            await CheckEvening();
            GD.Print("Home flow checks passed: morning (alarm, note, teeth, bag, door, flight down) and evening (cat routes, note, clip, meal, cat, forecast, homework, notebook).");
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
        var hud = player.Hud;
        var alarm = Sound("alarm");
        Check(morning.AlarmRinging, "the alarm rings at 6:50");
        Check(player.Position.X > 3.25f && player.Position.Z is > 4 and < 7.35f, $"she wakes in her room; she is at {player.Position}");
        Check(Door("LivingRoomDoor").State == Door3D.DoorState.Ajar, "the living-room door is ajar, with her father asleep inside");
        await Until(() => alarm.Playing && Sound("breathing").Playing, 2, "the alarm must sound and her father must snore");
        Button(MouseButton.Right, true);
        await Frames(2);
        Check(!player.PrismRaised && hud.HintText.Contains("棱镜"), "the prism cannot be raised at home in the morning");
        Button(MouseButton.Right, false);
        Press(Key.N);
        await Frames(2);
        Check(!hud.NotebookOpen, "the notebook cannot be opened in the morning");
        // Out of her room, the bathroom, the kitchen, past the ajar door into the living room and out onto the balcony,
        // then back to the entrance hall, each through its door.
        await Walk((2.75f, 4.6f), (2.55f, 5f), (2.55f, 6f), (2.55f, 5f), (1f, 4.5f), (1f, 3.3f), (1f, 4.5f), (2.8f, 4.5f), (2.8f, 3.3f),
            (3.8f, 3f), (6.2f, 3f), (7.3f, 3.42f), (8.5f, 3.42f), (7.3f, 3.42f), (6.2f, 3f), (3.8f, 3f), (2.8f, 3.3f), (2.8f, 4.5f), (.75f, 6.6f));
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
        hud.ClearText();
        await Use(FrontDoor);
        Check(Door("FrontDoor").State == Door3D.DoorState.Open && hud.SubtitleText == "我走啦。", "the front door opens, and she says goodbye");
        // Out through the shared vestibule and along the open corridor to the stair hall's landing on the second storey,
        // where the flight up and the flight down begin; only stepping onto the flight down ends the scene.
        await Walk((.75f, 6.8f), (.75f, 8.2f), (-.9f, 8.2f), (-.9f, 18.5f), (.8f, 17.5f), (2.2f, 17.5f), (.8f, 17.5f), (.8f, 19.55f));
        Check(!finished, "the corridor, the landing and the first steps up do not end the scene");
        await Walk((2.6f, 19.55f));
        Check(finished, $"stepping onto the flight down ends the scene; she is at {player.Position}");
        await End();
    }

    private async Task CheckEvening()
    {
        var evening = await Start<HomeEvening>("home-evening");
        var hud = player.Hud;
        Check(player.Position.X < 1.8f && player.Position.Z is > 5.5f and < 7.35f, $"she starts in the entrance hall; she is at {player.Position}");
        CheckCatRoutes();
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
        foreach (var name in new[] { "Father", "Money", "Backpack", "Sink" })
            Check(!Prop(name).Visible && Prop(name).CollisionLayer == 0, $"{name} belongs to the morning");
        Check(Prop("Clock").Visible && Prop("Clock").CollisionLayer == 0, "the alarm clock stays on the desk and cannot be used");
        await Refused("天黑了，不出去了。");
        await Walk((.75f, 5f), (2.6f, 4.5f), (2.6f, 3.3f), (4.75f, 2.6f));
        hud.ClearText();
        await Use("Note");
        Check(evening.NoteRead && Heard("饭在锅里，热一下再吃。作业写完早点睡。——爸", 10), "the new note says where the meal is");
        await Use("HairClip");
        Check(evening.ClipTaken && !Prop("HairClip").Visible, "she takes the hair clip");
        await Walk((5f, 3f));
        hud.ClearText();
        await Use("Tv");
        Check(evening.ForecastHeard && Heard("电视：明天白天，晴转多云，偏南风二到三级。", 10), "the weather forecast is on");
        hud.ClearText();
        Prop("Desk").Interact();
        Check(!evening.HomeworkDone && hud.SubtitleText == "先把饭热上吧。", "the homework waits for the meal");
        await Walk((2.6f, 3.3f), (2.6f, 4.5f), (1f, 4.5f), (1f, 3.3f));
        await Use("Stove");
        Check(!evening.Heated, "the meal takes a while to heat");
        await Until(() => evening.Heated, 6, "the meal must be hot");
        Check(Prop("Stove").GetNode<Node3D>("Steam").Visible && !Prop("Stove").GetNode<Node3D>("Flame").Visible, "the pot steams and the fire is off");
        hud.ClearText();
        Prop("Desk").Interact();
        Check(!evening.HomeworkDone && hud.SubtitleText == "煤球还没吃呢。", "the homework waits for the cat");
        await Walk((1f, 4.5f), (2.6f, 4.5f), (2.6f, 3.3f), (4.6f, 2.9f));
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
        Check(evening.CatBody.Section == "Ⅰ" && Flat(evening.CatBody.Position - new Vector3(5.4f, 0, 2.75f)).Length() < .6f,
            "the cat eats between the tea table and the television");
        await Walk((2.6f, 3.3f), (2.6f, 4.5f), (2.75f, 4.6f), (4f, 4.6f), (4.6f, 5.9f), (6.3f, 5.9f));
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

    // The cat walks straight from each waypoint of HomeRoute to the next. Between every two of the entrance hall, the
    // living room, the kitchen, her room and the bathroom, no wall, door or furniture stands on those lines. Toward her on
    // the balcony it walks as clear a way and stops in the living room.
    private void CheckCatRoutes()
    {
        var floor = HomeScene.Floor;
        Vector3[] places = [new(.75f, floor, 6.1f), new(5.4f, floor, 2.75f), new(1f, floor, 2f), new(4.8f, floor, 6f), new(2.4f, floor, 6.3f)];
        var balcony = new Vector3(8.5f, floor, 2f);
        foreach (var from in places)
            foreach (var goal in places.Where(place => place != from).Append(balcony))
            {
                var at = from;
                for (var leg = 0; at != goal; leg++)
                {
                    Check(leg < 5, $"the route from {from} must reach {goal}");
                    var next = HomeRoute.Next(at, goal);
                    if (next == at) break;
                    for (var step = 0f; step <= 1; step += .02f)
                        Check(CatFits(at.Lerp(next, step)), $"the cat's way from {from} to {goal} is clear between {at} and {next}");
                    at = next;
                }
                Check(goal != balcony || at.X < 7.6f && at.Z < 4, $"toward the balcony the cat stops in the living room; it stops at {at}");
            }
    }

    private bool CatFits(Vector3 spot) => player.GetWorld3D().DirectSpaceState.IntersectShape(new PhysicsShapeQueryParameters3D
    {
        Shape = new SphereShape3D { Radius = .15f },
        Transform = new Transform3D(Basis.Identity, spot + new Vector3(0, .25f, 0)),
        CollisionMask = PhysicsLayers.World
    }, 1).Count == 0;

    /// <summary>Presses E on the front door, which stays shut while she may not leave, and checks what she says.</summary>
    private async Task Refused(string line)
    {
        player.Hud.ClearText();
        await Use(FrontDoor);
        Check(player.Hud.SubtitleText == line && Door("FrontDoor").State == Door3D.DoorState.Shut, $"she does not leave yet and says \"{line}\"");
    }

    // Starts the scene as the debug entry of the given name does, listening for its end from the first frame on.
    private async Task<T> Start<T>(string entry) where T : Node
    {
        var path = DebugScenes.Find(entry);
        Check(path is not null, $"the debug entry {entry} must be registered");
        scene = GD.Load<PackedScene>(path).Instantiate<T>();
        finished = false;
        scene.Connect("Finished", Callable.From(() => finished = true));
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

    private Door3D Door(string name) => scene.GetNode<Region3D>("Region").Props("Ⅰ")!.GetNode<Door3D>($"HomeDoors/{name}");

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
        foreach (var distance in new[] { .6f, .9f, 1.5f })
            for (var side = 0; side < 8; side++)
            {
                var angle = side * Mathf.Pi / 4;
                var spot = prop.GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                spot.Y = HomeScene.Floor + .02f;
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
