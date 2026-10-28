using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Audio;
using Game.Characters;
using Game.Debugging;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The street region and the scene scripts of scene 2 and scene 6, played as a person plays them: walking with the keys,
/// aiming at what to buy and pressing E. Where a case needs a certain amount of money the script is given it.
/// </summary>
public partial class StreetChecks : Node
{
    private static readonly Vector3 Landing = new(0, 3.02f, -12.5f);
    private static readonly Vector3 RampFoot = new(.75f, 0, -6.2f);
    private static readonly Vector3 Entrance = new(0, 0, -3.5f);
    private static readonly Vector3 AtGate = new(62.5f, .02f, 2.5f);

    private TurningFixture play = null!;
    private Node scene = null!;
    private FirstPersonPlayer player => play.Player;
    private string subtitle => player.Hud.SubtitleText;
    private Character3D coal => play.Region.Character("cat");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await CheckSchoolRoad();
            await CheckKiosk();
            await CheckDoorWaits();
            await CheckCatFollowsFromWaiting();
            await CheckCatFollowsFromTheEndOfTheHall();
            await CheckCatFollowsAfterBeingCarried();
            await CheckCatFollowsToTheLeftOfTheLanding();
#if DEBUG
            Check(DebugScenes.Find("school-road") == "res://flow/school_road.tscn" && DebugScenes.Find("home-road") == "res://flow/home_road.tscn",
                "the debug entries start the two scenes");
#endif
            GD.Print("Street checks passed: sounds, walk out, bun, cat stares, school gate, kiosk, door, cat follows up the stair four ways.");
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

    private T Open<T>(string file) where T : Node
    {
        var road = GD.Load<PackedScene>($"res://flow/{file}.tscn").Instantiate<T>();
        scene = road;
        AddChild(road);
        return road;
    }

    // The sounds of the street are playing; the audio server lets go of them some time after they stop.
    private async Task Unload()
    {
        RemoveChild(scene);
        scene.Free();
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
    }

    private Interactable3D Prop(string name) => play.Region.Props("Ⅰ")!.GetNode<Interactable3D>(name);

    // Walks the player to a point with the movement keys, in a straight line, and lets go of them there.
    private async Task WalkTo(params Vector3[] points)
    {
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        foreach (var point in points)
            for (var frame = 0; Apart(player.Position, point) > .3f; frame++)
            {
                Check(frame < 15 * 60, $"the player must walk to {point}; she is at {player.Position}");
                var offset = point - player.Position;
                player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
                await play.Frames(1);
            }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // The prompt the crosshair shows for a point from where the player stands.
    private async Task<string> PromptAt(Vector3 feet, Vector3 target)
    {
        play.LookAt(feet, target);
        await play.Frames(2);
        return player.Hud.HintText;
    }

    private async Task UseProp(Vector3 feet, string prop, string prompt)
    {
        play.LookAt(feet, Prop(prop).GlobalPosition);
        // A notice of an earlier purchase shows in its place for a while.
        await play.Until(() => player.Hud.HintText == $"E  {prompt}", 4, $"the crosshair on {prop} must show '{prompt}'");
        Press(Key.E);
        await play.Frames(2);
    }

    private bool IsPlaying(string file) => player.GetParent().GetChildren().OfType<SectionSound>()
        .Any(sound => sound.Stream.ResourcePath.EndsWith($"{file}.wav") && sound.Playing);

    // Lines wait for the one before them, so a line is waited for.
    private Task Says(string text, double seconds = 6) => play.Until(() => subtitle.Contains(text), seconds, $"the subtitle must say '{text}'");

    // Where the player stands at the window of the kiosk, in front of the goods at x.
    private static Vector3 Counter(float x) => new(x, .02f, -1.1f);

    private async Task CheckSchoolRoad()
    {
        var road = Open<SchoolRoad>("school_road");
        await play.Use(road.Region);
        var finished = 0;
        road.Finished += () => finished++;
        Check(player.Position.Y > 2.9f && Forward(player).Dot(Vector3.Back) > .99f, "scene 2 starts on the landing, facing the hall");
        await play.Until(() => IsPlaying("bell") && IsPlaying("broadcast"), 1, "the bicycle bell and the loudspeaker must sound from the start");
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Check(!player.PrismRaised, "the prism cannot be raised on the school road");
        Button(MouseButton.Right, false);
        foreach (var closed in new[] { "HomeDoor", "IcePop", "Soda", "Spicy", "Stickers", "Sausage" })
            Check(Prop(closed).CollisionLayer == 0, $"{closed} is not used on the school road");

        // Down the stair and out of the hall.
        await WalkTo(new Vector3(.75f, 0, -10));
        Check(player.Position.Y > 2.5f, "the player walks down the stair from its top");
        await WalkTo(RampFoot, Entrance);
        Check(player.Position.Y < .1f, $"the player comes down to the floor; she is {player.Position.Y} m up");

        // The cat is talked to before there is a bun, and cannot be carried.
        Check(coal.Posture == Posture.Sitting, "the cat sits under the stair");
        Check(!coal.GetNode<Cat>("Cat").Holdable, "the cat cannot be picked up on the school road");
        var catAt = new Vector3(0, .2f, 0);
        Check(await PromptAt(new(-.6f, .02f, -8), coal.Position + catAt) == "E  看看煤球", "the cat is offered for talking to");
        Press(Key.E);
        await play.Until(() => subtitle.Contains("买个包子"), 6, "the cat must be promised a bun");
        Check(road.Wallet.Mao == 20 && coal.Posture == Posture.Sitting, "nothing is spent and the cat sits on");

        // The gate does not end the scene before the cat has been fed: she is reminded.
        await UseProp(AtGate, "SchoolGate", "进校门");
        await Says("煤球还在楼梯底下");
        Check(finished == 0, "the scene does not end at the gate while the cat has not been fed");
        player.Position = new(0, .02f, -3.5f);

        // The bun is bought outside, once.
        await WalkTo(new Vector3(4, 0, -1.6f), new Vector3(7, 0, -1.6f));
        await UseProp(new(7, .02f, -1.6f), "BunStall", "买包子（5 毛）");
        Check(road.Wallet.Mao == 15 && player.Hud.HintText.Contains("1 块 5 毛"), $"the bun costs 5 mao; the wallet has {road.Wallet.Mao}");
        Press(Key.E);
        await play.Frames(2);
        Check(road.Wallet.Mao == 15, "a second bun is not bought");

        // Back at the cat: half the bun, then the stare at the empty corner and the question.
        await WalkTo(new Vector3(4, 0, -1.6f), Entrance, new Vector3(-.6f, 0, -7.5f));
        Check(await PromptAt(new(-.6f, .02f, -8), coal.Position + catAt) == "E  掰一半喂煤球", "the cat can be fed now");
        var corner = play.Region.Props("Ⅰ")!.GetNode<Node3D>("EmptyCorner").GlobalPosition;
        var toCorner = Flat(corner - coal.Position).Normalized();
        Check(Forward(coal).Dot(toCorner) < .9f, "the cat is not looking at the corner before it has eaten");
        Press(Key.E);
        await play.Until(() => Forward(coal).Dot(toCorner) > .99f, 8, "the cat must turn to the corner");
        Check(coal.Posture == Posture.Sitting && coal.Busy, "the cat sits still and keeps its eyes on the corner");
        await play.Until(() => player.Camera.Position.Y < .9f, 4, "Xiaoman must kneel");
        await play.Until(() => subtitle == "小满：你看什么呢？", 3, "she must ask what the cat is looking at");
        await play.Until(() => player.Camera.Position.Y > 1.29f, 8, "Xiaoman must stand up again");
        Check(Forward(coal).Dot(toCorner) > .99f && !coal.HasNode("Feed"), "the cat goes on staring and is not fed twice");
        await play.Until(() => subtitle == "小满：算了，要迟到了。", 4, "she must give up and go on");

        // The toy stall at the gate has things to look at, and the gate ends the scene.
        await UseProp(new(59, .02f, -1.6f), "ToyStall", "看看小摊");
        await play.Until(() => subtitle.Contains("玻璃弹珠"), 4, "the toy stall must have marbles");
        Check(finished == 0, "the scene is not over before the gate");
        await UseProp(AtGate, "SchoolGate", "进校门");
        Check(finished == 1, "the scene is over once she goes in at the school gate");
        await Unload();
    }
}
