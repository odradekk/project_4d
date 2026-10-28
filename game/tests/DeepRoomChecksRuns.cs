using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Audio;
using Game.Characters;
using Game.Creatures;
using Game.Flow;
using Game.Interaction;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// The runs that are not the main route, and what they walk with.
public partial class DeepRoomChecks
{
    // The corner and the window frame are bitten away, and she walks at the gaps with the real keys held: she stays in the room.
    private async Task CheckGaps()
    {
        await Start();
        await Until(() => play.Region.CurrentSection == "Ⅱ", 3, "she must have followed the cat into section II");
        // Left alone she is told where to go, and then what to do.
        Engine.TimeScale = 5;
        await Until(() => shown.Any(line => line.Contains("滚轮切到第Ⅲ页")), 60, "she must be told how to get through");
        Engine.TimeScale = 1;
        await Cross(S2, "Ⅲ");
        foreach (var spot in new Vector3[] { new(2, Floor, 2), new(8, Floor, 6), new(5, Floor, 2.5f) }) await Walk(spot);
        Engine.TimeScale = 5;
        await Until(() => shown.Any(line => line.Contains("按 E 抱起")), 60, "she must be told to pick the cat up");
        Engine.TimeScale = 1;
        Check(room.Now == DeepRoom.Stage.Arrived && !ask.IsSevered("s2") && !room.GetNode<AudioStreamPlayer>("heartbeat").Playing, "nothing happens until she picks the cat up");
        // The bites are given by hand, from far off in W, to see the room with the corner and the window frame gone.
        var far = new Vec4(0, 2, 0, 40);
        GrazerKey Key(double time) => new(time, far, [new(RotationPlane.XZ, 0), new(RotationPlane.YW, 0)], 0);
        room.Region!.Grazers!.Play(DeepGrazer.Id, new GrazerTimeline([Key(0), Key(1)],
            [new GrazerBite(.1, [new(DeepGrazer.Corner), new(DeepGrazer.Frame), new(DeepGrazer.Window)], [])]));
        await Until(() => eaten.Contains(DeepGrazer.Corner) && eaten.Contains(DeepGrazer.Frame), 3, "the corner and the window frame must be bitten away");
        var (corner, lowest) = await WalkAt(new(7.5f, Floor, 6f), new(12, Floor, 9));
        Check(lowest > -.5f && corner.X < 9.8f && corner.Z < 7.8f, $"walking at the corner she stays in the room: she ends at {corner} and was as low as {lowest:F1} m");
        var (window, lowestAtWindow) = await WalkAt(new(5, Floor, 3), new(5, Floor, -3));
        Check(lowestAtWindow > -.5f && window.Z > .2f, $"walking at the window she stays in the room: she ends at {window} and was as low as {lowestAtWindow:F1} m");
        await End();
    }

    // Standing where the grazer goes while it is at its bites, with the cat in her arms: it pushes her back, and she is not held and the cat does
    // not tear loose until it comes for her; then the cat tears loose, there is a chase, and then she screams.
    private async Task CheckStandingOnTheRoute()
    {
        foreach (var spot in new Vector3[] { new(7f, Floor, 5.2f), new(7.4f, Floor, 5.6f), S2 }) await CheckStandingAt(spot);
    }

    private async Task CheckStandingAt(Vector3 spot)
    {
        await Start();
        await Until(() => play.Region.CurrentSection == "Ⅱ", 3, "she must have followed the cat into section II");
        await Cross(S2, "Ⅲ");
        await Until(() => room.Entries(DeepRoom.Stage.Arrived) > 0, 2, "the scene must know she has arrived");
        CatBody.GetNode<Interactable3D>("Pickup").Interact();
        await Until(() => room.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        await Walk(spot);
        Engine.TimeScale = 6;
        await Until(() => eaten.Contains(DeepGrazer.Frame), 100, $"it must bite the window frame while she stands at {spot}");
        Check(!room.CatFled && !play.Player.Seated && room.Entries(DeepRoom.Stage.Grazing) == 0,
            $"standing at {spot} she is not held and the cat does not tear loose while it is still at its bites");
        await Until(() => room.Entries(DeepRoom.Stage.Scream) > 0, 200, "she must scream in the end");
        Check(fledAt >= grazingAt && grazingAt > 0, $"the cat tears loose {fledAt - grazingAt:F1} s after the chase has begun");
        CheckChase();
        // The lines about her own room are not said to someone who has not looked, and her father is called for before the first bite.
        InOrder("爸爸！", "那扇多出来的门");
        Check(!shown.Any(line => line.Contains("笔记本摊在桌上")), "she has not looked at her room, and is not told what is in it");
        await Until(() => finished, 60, "the scene must end");
        await End();
    }

    // Scene 8 still hosts the room and has said it is done; scene 9 takes the room over, and a player keeps running from the grazer.
    private async Task CheckHostedByScene8()
    {
        var scene8 = new CatRoom();
        AddChild(scene8);
        await play.Frames(3);
        var region = scene8.Home.Region;
        region.World!.SetSeamOpen(new("s2"), true);
        var cat8 = scene8.CatBody;
        cat8.PlaceAt(scene8.Home.SeamAt("S1"), 0);
        Check(cat8.CrossSeam("s1", "Ⅱ") is null, "the cat crosses the first seam");
        cat8.PlaceAt(scene8.Home.SeamAt("S2"), 0);
        Check(cat8.CrossSeam("s2", "Ⅲ") is null, "the cat crosses the second seam");
        var done = false;
        scene8.Finished += () => done = true;
        scene8.Vanish();
        Engine.TimeScale = 5;
        await Until(() => done, 8, "scene 8 must say that it is done");
        Engine.TimeScale = 1;
        cat8.Stop();
        await play.Frames(5);
        Check(!cat8.Busy, "scene 8 gives the cat no more commands once it is done");
        var scene9 = new DeepRoom { Region = region };
        AddChild(scene9);
        await Bind(scene9);
        Check(scene9.Region == region && scene9.Home.Region == region && !scene9.GetChildren().OfType<Region3D>().Any() && scene8.GetChildren().OfType<Region3D>().Count() == 1,
            "scene 9 uses the region it is given and loads no other");
        Check(play.Region.CurrentSection == "Ⅰ" && scene9.Now == DeepRoom.Stage.Following, "it leaves her where scene 8 had her");
        await Cross(S1, "Ⅱ");
        await Cross(S2, "Ⅲ");
        await Until(() => room.Entries(DeepRoom.Stage.Arrived) > 0, 2, "the scene must know she has arrived");
        CatBody.GetNode<Interactable3D>("Pickup").Interact();
        await Until(() => room.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        play.Player.PutDown!();
        await play.Frames(5);
        Check(!cat8.Busy, "put down, the cat is left as it is");
        Engine.TimeScale = 6;
        await RunAway();
        CheckChase();
        // The room is too small to keep away from it: she is caught by the distance, not by the guard that ends the chase after 40 s.
        Check(grazingAt >= 0 && fledAt - grazingAt < 40, $"she is caught {fledAt - grazingAt:F1} s into the chase");
        await Until(() => !cat8.Busy, 6, "the cat must be left alone once it has run");
        await Until(() => finished, 60, "the scene must end");
        Engine.TimeScale = 1;
        RemoveChild(scene9);
        scene9.Free();
        RemoveChild(scene8);
        scene8.Free();
        ask = null!;
        await Settle();
    }

    // She runs for the corner of the room that is farthest from the grazer, with the keys held, until she can no longer move.
    private async Task RunAway()
    {
        var (player, limit) = (play.Player, elapsed + 200);
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        while (room.Entries(DeepRoom.Stage.Scream) == 0)
        {
            Check(elapsed < limit, "the scene must end although she keeps away");
            var corner = new[] { new Vector3(1.5f, Floor, 1.2f), new(8.5f, Floor, 1.2f), new(1.5f, Floor, 6.8f), new(8.5f, Floor, 6.8f) }
                .MaxBy(spot => Flat(spot - GrazerAt()).Length());
            var toward = Flat(corner - player.Position);
            if (!room.CatFled && toward.Length() > .5f) player.Rotation = new(0, Mathf.Atan2(-toward.X, -toward.Z), 0);
            await play.Frames(1);
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // She stands at a place facing a point and walks at it for three seconds with the keys held. Where she ends, and how low she went.
    private async Task<(Vector3 End, float Lowest)> WalkAt(Vector3 from, Vector3 toward)
    {
        play.LookAt(from, toward + Vector3.Up * 1.3f);
        var (player, lowest, until) = (play.Player, 0f, elapsed + 3);
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        while (elapsed < until)
        {
            await play.Frames(1);
            lowest = Mathf.Min(lowest, player.Position.Y);
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        return (player.Position, lowest);
    }

    // She walks to a place with the keys held, as she would.
    private async Task Walk(Vector3 feet)
    {
        var player = play.Player;
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        for (var frames = 0; Flat(feet - player.Position).Length() > .25f; frames++)
        {
            Check(frames < 900, $"she must be able to walk to {feet} but is at {player.Position}");
            var toward = Flat(feet - player.Position);
            player.Rotation = new(0, Mathf.Atan2(-toward.X, -toward.Z), 0);
            await play.Frames(1);
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // She stands at a seam, raises the lens, scrolls to the section she means to go to and clicks.
    private async Task Cross(Vector3 seam, string target)
    {
        play.LookAt(seam, seam + new Vector3(.7f, 1, 0));
        Button(MouseButton.Right, true);
        await play.Frames(3);
        for (var scrolls = 0; prism.TargetSection != target; scrolls++)
        {
            Check(scrolls < 3, $"the lens must be able to show section {target}; it shows {prism.TargetSection}");
            Button(MouseButton.WheelDown, true);
            await play.Frames(2);
        }
        Button(MouseButton.Left, true);
        await play.Frames(3);
        Button(MouseButton.Left, false);
        Button(MouseButton.Right, false);
        Check(play.Region.CurrentSection == target, $"she crosses into section {target}");
    }
}
