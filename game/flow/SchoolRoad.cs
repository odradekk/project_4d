using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 2 of the prologue, from the door of the home down the stair to the school gate. Xiaoman buys a bun at the stall
/// outside, breaks half of it for the cat under the stair, and kneels to ask the cat what it is staring at in the empty
/// corner of the hall. The prism cannot be used here. <see cref="Finished"/> follows when she goes in at the school gate,
/// which she can do once she has done that; before it she is reminded of the cat.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class SchoolRoad : Node
{
    private const int BunPrice = 5;
    // How far her eyes drop when she kneels.
    private const float Kneel = .55f;
    private const double KneelSeconds = .45;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The money Xiaoman starts with: the two yuan from the table.</summary>
    public Wallet Wallet { get; set; } = new(20);

    public Region3D Region => street.Region;

    private Street street = null!;
    private Character3D coal = null!;
    private Interactable3D feed = null!;
    private bool hasBun;
    private bool fed;
    private bool beatDone;

    private GameHud Hud => street.Hud;

    public override void _Ready()
    {
        street = new Street(this);
        street.UseProps("BunStall", "ToyStall", "SchoolGate");
        coal = Region.Character("cat");
        // On this road the cat is fed, not carried: the crosshair on the cat finds a prompt of its own.
        coal.GetNode<Cat>("Cat").Holdable = false;
        coal.Sit();
        feed = new Interactable3D { Name = "Feed", Prompt = "看看煤球", CollisionLayer = PhysicsLayers.Pickups, CollisionMask = 0 };
        feed.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .3f }, Position = new(0, .2f, 0) });
        coal.AddChild(feed);
        feed.Interacted += TalkToCat;
        var stall = street.Prop("BunStall");
        stall.Prompt = $"买包子（{Wallet.Format(BunPrice)}）";
        stall.Interacted += BuyBun;
        var toys = street.Prop("ToyStall");
        toys.Prompt = "看看小摊";
        toys.Interacted += () => Hud.Say("小满", "玻璃弹珠，一毛钱三颗……放学再来看吧。", 3);
        var gate = street.Prop("SchoolGate");
        gate.Prompt = "进校门";
        gate.Interacted += EnterSchool;
        Hud.Say("", "楼道里飘着邻居家炒菜的油烟味。", 3);
    }

    // The cat staring at the corner is the one odd thing on the road, so the scene does not end before it has been seen.
    private void EnterSchool()
    {
        if (!beatDone)
        {
            Hud.Say("小满", "煤球还在楼梯底下呢，先去看看它。", 3);
            return;
        }
        street.Prop("SchoolGate").CollisionLayer = 0;
        Hud.Say("小满", "到学校了。", 2);
        EmitSignal(SignalName.Finished);
    }

    private void BuyBun()
    {
        if (hasBun || fed)
        {
            Hud.Say("小满", "一个包子就够了。", 2);
            return;
        }
        if (!street.Buy(Wallet, "包子", BunPrice)) return;
        hasBun = true;
        Region.Character("bun_vendor").Say("热乎的，拿好！", 2);
        feed.Prompt = "掰一半喂煤球";
    }

    private void TalkToCat()
    {
        if (!hasBun) Hud.Say("小满", "煤球，你饿了吧？等我，我去给你买个包子。", 3.5);
        else Feed();
    }

    // The cat eats, then stops and stares at the empty corner at the far end of the hall, and Xiaoman kneels to ask.
    private void Feed()
    {
        hasBun = false;
        fed = true;
        feed.QueueFree();
        var corner = street.Mark("EmptyCorner").GlobalPosition;
        var eyes = street.Player.Camera;
        var standing = eyes.Position.Y;
        var beat = CreateTween();
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "来，掰一半给你。", 2)));
        beat.TweenInterval(3);
        beat.TweenCallback(Callable.From(() => coal.GetNode<Cat>("Cat").Stare(corner)));
        beat.TweenInterval(1.5);
        beat.TweenProperty(eyes, "position:y", standing - Kneel, KneelSeconds);
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "你看什么呢？", 2.5)));
        beat.TweenInterval(3);
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "那里明明什么也没有……", 3)));
        beat.TweenInterval(2);
        beat.TweenProperty(eyes, "position:y", standing, KneelSeconds);
        beat.TweenCallback(Callable.From(() =>
        {
            Hud.Say("小满", "算了，要迟到了。", 2.5);
            beatDone = true;
        }));
    }
}
