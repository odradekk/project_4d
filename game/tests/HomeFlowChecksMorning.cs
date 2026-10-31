using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Audio;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Game.Regions;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scene 1 from the alarm to the bus: the flat, the front door's conditions, the cat under the ground-floor stair, the bus
// called near the stop, boarding and the ride.
public partial class HomeFlowChecks
{
    private const string Asks = "你看什么呢？";
    // The wall under the ground-floor stair, and the passenger's eyes on the bus.
    private const float UnderStairWall = 4.94f;
    private static readonly Vector3 PassengerEyes = new(-.8f, 2.02f, -1.3f);

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
        await CheckCatUnderStair(morning);
        await CheckBus();
    }

    // Out through the shared vestibule and along the open corridor to the second storey's landing, right above the cat,
    // which she does not meet from there; down the stair to the ground floor, where she meets it as she comes off the flight.
    private async Task CheckCatUnderStair(HomeMorning morning)
    {
        var cat = scene.GetNode<Region3D>("Region").Character("cat");
        var at = cat.Position;
        Check(cat.Posture == Posture.Sitting && at.Y < .1f && at.X is > 1.39f and < UnderStairWall && at.Z is > 18.59f and < 20.53f,
            $"the cat sits on the floor under the ground-floor stair; it is at {at}, {cat.Posture}");
        // Its gaze meets the wall under the stair, at the far end of the space beneath it, short of the stair hall's south wall.
        var gaze = -cat.GlobalBasis.Z;
        var met = at.Z + gaze.Z * (UnderStairWall - at.X) / gaze.X;
        Check(gaze.X > 0 && met is > 18.59f and < 20.65f, $"the cat stares at the empty corner at the far end under the stair; it looks along {gaze}");
        var eyes = player.Camera;
        var standing = eyes.Position.Y;
        await Walk((.75f, 6.8f), (.75f, 8.2f), (-.9f, 8.2f), (-.9f, 18.5f), (.8f, 19.55f));
        await Frames(60);
        Check(player.Hud.SubtitleText != Asks && eyes.Position.Y == standing, "she does not meet the cat from the landing above it");
        await Walk((7f, 19.55f), (7f, 17.5f), (1.2f, 17.5f));
        await Until(() => player.Hud.SubtitleText == Asks, 3, "she must ask the cat what it sees");
        Check(eyes.Position.Y < standing - .5f, $"she kneels to ask; her eyes are {standing - eyes.Position.Y} m lower");
        await Until(() => player.Hud.SubtitleText == "那里明明什么也没有……", 5, "she must find nothing there");
        await Until(() => player.Hud.SubtitleText == "算了，要迟到了。", 5, "she must go on");
        Check(Mathf.IsEqualApprox(eyes.Position.Y, standing) && cat.Posture == Posture.Sitting, "she stands up again, and the cat stays");
        Check(!finished, "the stair and the cat do not end the scene");
    }

    // Out of the building, through the compound and the gate to the bus stop; the bus comes once she is within 15 m of the
    // stop and waits for her; E on its door seats her, and the scene fades and finishes as it drives off.
    private async Task CheckBus()
    {
        var bus = scene.GetNode<Region3D>("Region").Props("Ⅰ")!.GetNode<Bus3D>("Bus");
        Engine.TimeScale = 8;  // the walk across the compound is long
        await Walk((-3f, 18.5f), (-3f, 41f), (15.8f, 41f), (15.8f, 93.5f), (81f, 93.5f), (82f, 87.2f));
        Check(bus.State == Bus3D.BusState.Away, $"16 m from the stop the bus has not been called; it is {bus.State}");
        await Walk((82f, 85f));
        Check(bus.State == Bus3D.BusState.Arriving, $"within 15 m of the stop the bus comes; it is {bus.State}");
        await Walk((83.4f, 74.3f));
        await Until(() => bus.State == Bus3D.BusState.Waiting, 60, "the bus must stop and open its doors");
        await Frames(120);
        Check(bus.State == Bus3D.BusState.Waiting && !finished, "the bus waits with its doors open");
        Engine.TimeScale = 1;
        Check(await Looks(new(84.4f, 1.3f, 74.3f), "E  上车"), $"the crosshair finds the bus's door; the hint is \"{player.Hud.HintText}\"");
        var stop = bus.GlobalPosition;
        var boarded = Time.GetTicksMsec();
        Press(Key.E);
        await Frames(2);
        Check(bus.State == Bus3D.BusState.Closing, $"E on the door seats her and the doors close; the bus is {bus.State}");
        Check(player.Camera.GlobalPosition.DistanceTo(bus.ToGlobal(PassengerEyes)) < .01f, $"her view is at the passenger's seat; it is at {player.Camera.GlobalPosition}");
        var (feet, facing) = (player.Position, player.Rotation);
        Input.ActionPress("move_forward");
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(300, 0), ScreenRelative = new(300, 0) });
        Press(Key.E);
        await Frames(30);
        Input.ActionRelease("move_forward");
        Check(player.Position.IsEqualApprox(feet) && player.Rotation.IsEqualApprox(facing), "on the bus the keys and the mouse do nothing");
        Check(player.Camera.GlobalPosition.DistanceTo(bus.ToGlobal(PassengerEyes)) < .01f, "her view rides with the bus");
        await Until(() => finished, 8, "the scene must end after the ride");
        var ride = Time.GetTicksMsec() - boarded;
        Check(ride > 4800, $"she rides about 5 s before the scene ends; it ended after {ride} ms");
        Check(bus.State == Bus3D.BusState.Departing && bus.GlobalPosition.DistanceTo(stop) > 8, $"the bus has driven off; it is {bus.State}, {bus.GlobalPosition.DistanceTo(stop)} m from the stop");
        Check(scene.FindChildren("*", nameof(ColorRect), true, false).Cast<ColorRect>().Any(black => black.Color == Colors.Black),
            "the screen has faded to black");
        await End();
    }
}
