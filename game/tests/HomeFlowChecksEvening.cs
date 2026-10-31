using System.Threading.Tasks;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Game.Regions;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scene 7 from the bus to the flat: the ride in, getting off, the bus leaving, the cat waiting at the foot of the stair and
// following her up to the front door, and the door locked once both are in.
public partial class HomeFlowChecks
{
    private const string Whisper = "小满：爸不让养猫，你可别出声啊。";
    // Where the cat waits, at the mouth of the ground-floor stair hall.
    private static readonly Vector3 CatWaits = new(-1f, .02f, 18.5f);

    // She rides in seated, only watching; once the bus stands with its doors open, E on the door gets her off onto the
    // sidewalk in front of the stop, where she only watches while the bus shuts its doors and pulls away, and it drives off
    // out of sight.
    private async Task CheckRideHome()
    {
        var bus = scene.GetNode<Region3D>("Region").Props("Ⅰ")!.GetNode<Bus3D>("Bus");
        var eyes = player.Camera;
        await Frames(2);
        Check(bus.State == Bus3D.BusState.Arriving && player.Watching, $"she starts on the bus coming in, only watching; it is {bus.State}");
        Check(eyes.GlobalPosition.DistanceTo(bus.ToGlobal(PassengerEyes)) < .01f, $"her view is at the passenger's seat; it is at {eyes.GlobalPosition}");
        Check(player.Hud.HintText == "", "while the bus comes in no prompt is shown");
        Engine.TimeScale = 6;
        await Until(() => bus.State == Bus3D.BusState.Waiting, 60, "the bus must stop and open its doors");
        Engine.TimeScale = 1;
        await Frames(2);
        Check(player.Hud.HintText == "E  下车", $"at the stop her view is on the door, which says she gets off; the hint is \"{player.Hud.HintText}\"");
        // While she waits to get off, the mouse and the keys do not move her view off the seat.
        var feet = player.Position;
        Input.ActionPress("move_forward");
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(300, 200), ScreenRelative = new(300, 200) });
        await Frames(10);
        Input.ActionRelease("move_forward");
        Check(eyes.GlobalPosition.DistanceTo(bus.ToGlobal(PassengerEyes)) < .01f && player.Position.IsEqualApprox(feet),
            $"seated at the stop she stays in her seat; her view is at {eyes.GlobalPosition}");
        Check(player.Hud.HintText == "E  下车", "her view stays on the door");
        Press(Key.E);
        await Frames(2);
        Check(bus.State == Bus3D.BusState.Closing && player.Watching, $"E on the door gets her off, and she watches the doors close; the bus is {bus.State}");
        Check(eyes.GlobalPosition.X is > 78 and < 84 && Flat(eyes.GlobalPosition - new Vector3(83.4f, 0, 69.7f)).Length() < .1f,
            $"she stands on the sidewalk in front of the stop; her view is at {eyes.GlobalPosition}");
        Check(Mathf.Abs(eyes.Rotation.X) < .01f, "she looks ahead, not where the seat looked");
        var off = player.Position;
        Input.ActionPress("move_forward");
        await Frames(150);
        Check(player.Position.IsEqualApprox(off) && player.Watching, $"while the bus pulls away she cannot move; she is at {player.Position}");
        await Until(() => !player.Watching, 1, "she must be free to go once the bus has pulled away");
        await Frames(20);
        Input.ActionRelease("move_forward");
        Check(Flat(player.Position - off).Length() > .3f, $"then she walks; she is at {player.Position}");
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(0, 4), ScreenRelative = new(0, 4) });
        await Frames(2);
        Check(Mathf.Abs(eyes.Rotation.X) < .05f, $"the mouse turns her view on from ahead, not from where it turned on the seat; she looks {eyes.Rotation.X} rad down");
        var stop = bus.GlobalPosition;
        Engine.TimeScale = 6;
        await Until(() => bus.State == Bus3D.BusState.Away, 60, "the bus must drive off out of sight");
        Engine.TimeScale = 1;
        Check(!bus.Visible && bus.GlobalPosition.DistanceTo(stop) > 100, $"the bus has gone; it was last {bus.GlobalPosition.DistanceTo(stop)} m from the stop");
    }

    // Home through the gate and the compound to the stair hall, where the cat sits and does not stir while she is 3.5 m off;
    // within 3 m it gets up and she whispers to it. When she walks off beyond the forecourt, it goes back and waits, and
    // follows her again when she comes back. It follows her up the first flight; she carries it to the turn and puts
    // it down there, and it follows her again up the second flight and along the corridor; the front door opens for her,
    // and once she and the cat are in, it shuts and stays locked. The cat is in within 60 s of getting up.
    private async Task CheckCatUpstairs(HomeEvening evening)
    {
        var cat = evening.CatBody;
        var hud = player.Hud;
        Check(cat.Posture == Posture.Sitting && Flat(cat.Position - CatWaits).Length() < .05f && cat.Position.Y < .1f,
            $"the cat sits at the foot of the stair; it is at {cat.Position}, {cat.Posture}");
        Engine.TimeScale = 6;
        await Walk((83.6f, 76f), (82f, 93.5f), (15.8f, 93.5f), (15.8f, 41f), (-3f, 41f), (-4.5f, 18.5f));
        Engine.TimeScale = 1;
        await Frames(30);
        Check(cat.Posture == Posture.Sitting && !cat.Busy && hud.SubtitleText != Whisper, "3.5 m from the cat it stays sitting, and she says nothing to it");
        await Walk((-3.8f, 18.5f));
        await Until(() => hud.SubtitleText == Whisper, 1, "she must whisper to the cat once she is within 3 m");
        await Frames(30);
        Check(cat.Busy && cat.Posture == Posture.Standing, "the cat gets up and follows her");
        // Off beyond the forecourt she is out of the cat's way: it goes back to its place and waits; back within reach, it
        // follows her again, and she does not whisper again.
        await Walk((-9f, 18.5f));
        await Until(() => cat.Posture == Posture.Sitting && Flat(cat.Position - CatWaits).Length() < .3f, 15,
            $"the cat must go back to the stair and wait; it is at {cat.Position}");
        var told = 0;
        void Told() => told++;
        cat.ActionReplaced += Told;
        await Frames(60);
        cat.ActionReplaced -= Told;
        Check(cat.Posture == Posture.Sitting && Flat(cat.Position - CatWaits).Length() < .3f && told == 0,
            $"the cat waits at the stair while she is away, and is not sent back again; it was told {told} times");
        hud.ClearText();
        await Walk((-3.8f, 18.5f));
        await Frames(30);
        Check(cat.Busy && cat.Posture == Posture.Standing && hud.SubtitleText != Whisper, "back within reach, the cat follows her again, without the whisper");
        var called = Engine.GetPhysicsFrames();
        Check(!evening.Indoors && !Door("FrontDoor").Locked(), "the front door is not locked while she is out");
        await Walk((-1.5f, 17.5f), (.5f, 17.5f), (6.9f, 17.5f));
        cat.GetNode<Interactable3D>("Pickup").Interact();
        await Walk((6.9f, 19.55f));
        player.PutDown?.Invoke();
        await Frames(30);
        Check(!cat.GetNode<Cat>("Cat").Held && cat.Position.Y is > 1.4f and < 1.8f, $"she puts the cat down at the turn; it is at {cat.Position}");
        await Walk((.8f, 19.55f), (-.9f, 18.5f), (-.9f, 8.2f), (.75f, 8.2f));
        hud.ClearText();
        await Use(FrontDoor);
        Check(Door("FrontDoor").State == Door3D.DoorState.Open && hud.SubtitleText == "", "the front door opens for her as she comes home");
        await Walk((.75f, 6.6f), (1.6f, 4.6f), (2.6f, 4.5f));
        await Until(() => evening.Indoors, 60 - (Engine.GetPhysicsFrames() - called) / (double)FramesPerSecond, $"the cat must follow her in; it is at {cat.Position}");
        Check(HomeRoute.InFlat(cat.Position) && Door("FrontDoor").State == Door3D.DoorState.Shut,
            $"with both in, the front door is shut; the cat is at {cat.Position}");
    }
}
