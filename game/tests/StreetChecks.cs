using System;
using System.Threading.Tasks;
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

    private TurningFixture play = null!;
    private Node scene = null!;
    private FirstPersonPlayer player => play.Player;
    private string subtitle => player.Hud.SubtitleText;
    private Character3D coal => play.Region.Character("coal");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await CheckSchoolRoad();
            await CheckKiosk();
            await CheckStair();
#if DEBUG
            Check(DebugScenes.Find("school-road") == "res://flow/school_road.tscn" && DebugScenes.Find("home-road") == "res://flow/home_road.tscn",
                "the debug entries start the two scenes");
#endif
            GD.Print("Street checks passed: walk out, bun, cat stares, school gate, kiosk, cat follows up the stair, door.");
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

    private Interactable3D Prop(string name) => play.Region.GetNode<Interactable3D>($"Props/{name}");

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

    // The prompt the crosshair shows for a prop from where the player stands.
    private async Task<string> PromptAt(Vector3 feet, string prop)
    {
        play.LookAt(feet, Prop(prop).GlobalPosition);
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
        Check(await PromptAt(new(-.6f, .02f, -8), "CatFeed") == "E  看看煤球", "the cat is not offered for carrying on the school road");
        Press(Key.E);
        await play.Until(() => subtitle.Contains("买个包子"), 6, "the cat must be promised a bun");
        Check(road.Wallet.Mao == 20 && coal.Posture == Posture.Sitting, "nothing is spent and the cat sits on");

        // The bun is bought outside, once.
        await WalkTo(new Vector3(4, 0, -1.6f), new Vector3(7, 0, -1.6f));
        await UseProp(new(7, .02f, -1.6f), "BunStall", "买包子（5 毛）");
        Check(road.Wallet.Mao == 15 && player.Hud.HintText.Contains("1 块 5 毛"), $"the bun costs 5 mao; the wallet has {road.Wallet.Mao}");
        Press(Key.E);
        await play.Frames(2);
        Check(road.Wallet.Mao == 15, "a second bun is not bought");

        // Back at the cat: half the bun, then the stare at the empty corner and the question.
        await WalkTo(new Vector3(4, 0, -1.6f), Entrance, new Vector3(-.6f, 0, -7.5f));
        Check(await PromptAt(new(-.6f, .02f, -8), "CatFeed") == "E  掰一半喂煤球", "the cat can be fed now");
        var corner = play.Region.GetNode<Node3D>("Marks/EmptyCorner").GlobalPosition;
        var toCorner = Flat(corner - coal.Position).Normalized();
        Check(Forward(coal).Dot(toCorner) < .9f, "the cat is not looking at the corner before it has eaten");
        Press(Key.E);
        await play.Until(() => Forward(coal).Dot(toCorner) > .99f, 8, "the cat must turn to the corner");
        Check(coal.Posture == Posture.Sitting && coal.Busy, "the cat sits still and keeps its eyes on the corner");
        await play.Until(() => player.Camera.Position.Y < .9f, 4, "Xiaoman must kneel");
        await play.Until(() => subtitle == "小满：你看什么呢？", 3, "she must ask what the cat is looking at");
        await play.Until(() => player.Camera.Position.Y > 1.29f, 8, "Xiaoman must stand up again");
        Check(Forward(coal).Dot(toCorner) > .99f && Prop("CatFeed").CollisionLayer == 0, "the cat goes on staring and is not fed twice");
        await play.Until(() => subtitle == "小满：算了，要迟到了。", 4, "she must give up and go on");

        // The toy stall at the gate has things to look at, and the gate ends the scene.
        await UseProp(new(59, .02f, -1.6f), "ToyStall", "看看小摊");
        await play.Until(() => subtitle.Contains("玻璃弹珠"), 4, "the toy stall must have marbles");
        Check(finished == 0, "the scene is not over before the gate");
        await WalkTo(new Vector3(64.2f, 0, 2.5f));
        Check(finished == 1, "the scene is over at the school gate");
        await Unload();
    }

    private async Task CheckKiosk()
    {
        var road = Open<HomeRoad>("home_road");
        await play.Use(road.Region);
        Check(player.Position.X > 60 && Forward(player).Dot(Vector3.Left) > .99f, "scene 6 starts at the school gate, facing home");
        Check(Prop("BunStall").CollisionLayer == 0 && Prop("CatFeed").CollisionLayer == 0, "the stall of the morning is not used");
        Check(coal.Posture == Posture.Sitting && Apart(coal.Position, new(.8f, 0, -5.9f)) < .2f, "the cat waits in the mouth of the stairwell");
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Check(!player.PrismRaised, "the prism cannot be raised on the way home");
        Button(MouseButton.Right, false);

        // With 3 mao the sausage cannot be bought.
        road.Wallet = new(3);
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        Check(road.Wallet.Mao == 3, "the sausage is refused for want of money");
        await Says("钱不够了");

        // With 15 mao a soda is 10 and the sausage 5, which is all of it. One thing is picked, not two.
        road.Wallet = new(15);
        await UseProp(Counter(37.1f), "Soda", "买汽水（1 块）");
        Check(road.Wallet.Mao == 5, $"the soda costs 1 yuan; the wallet has {road.Wallet.Mao}");
        await UseProp(Counter(36), "IcePop", "买冰棍（3 毛）");
        Check(road.Wallet.Mao == 5, "a second pick is refused");
        await Says("买一样就够了");
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        Check(road.Wallet.Mao == 0, "the sausage is paid for with what is left");
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        Check(road.Wallet.Mao == 0, "a second sausage is not bought");
        await Says("回家", 12);
        await Says("已经买了", 12);
        await Unload();
    }

    private async Task CheckStair()
    {
        var road = Open<HomeRoad>("home_road");
        await play.Use(road.Region);
        var finished = 0;
        road.Finished += () => finished++;

        // The door opens for neither an empty bag nor a cat that is left behind.
        await UseProp(Landing, "HomeDoor", "开门");
        await Says("没给煤球买火腿肠");
        Check(finished == 0, "the door does not open without the sausage");
        road.Wallet = new(5);
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        await UseProp(Landing, "HomeDoor", "开门");
        await Says("煤球呢");
        Check(finished == 0, "the door does not open without the cat");

        // The cat sits until the player is near, then follows. It is beside the stair, where a straight line to the player
        // up the stair would take it under the ramp, and the player is already up.
        coal.PlaceAt(new(-1.2f, .02f, -5.8f), 0);
        await play.Frames(30);
        Check(coal.Posture == Posture.Sitting && !coal.Busy, "the cat does not follow from afar");
        player.Position = new(.75f, 2.2f, -9);
        await play.Until(() => coal.Busy, 2, "the cat must get up and follow");
        Check(coal.Posture == Posture.Standing, "the cat stands up to follow");
        await Says("煤球！");
        await WalkTo(new Vector3(.75f, 0, -10), new Vector3(0, 0, -12.5f));
        await play.Until(() => coal.Position.Y > 2.9f && Apart(coal.Position, player.Position) < 1.6f, 15, "the cat must follow her up the stair");

        // At the door she picks the cat up and takes it in.
        await UseProp(Landing, "HomeDoor", "开门");
        Check(coal.GetNode<Cat>("Cat").Held && Prop("HomeDoor").CollisionLayer == 0, "she has the cat in her arms and the door is spent");
        await play.Until(() => subtitle == "小满：爸不让养猫，你可别出声啊。", 5, "she must tell the cat to keep quiet");
        Check(finished == 0, "the scene is not over before she has said it");
        await play.Until(() => finished == 1, 5, "the scene must end at the door");
        await Unload();
    }
}
