using Game.Adapter;
using Game.Characters;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 2 of the prologue, from the door of the home down the stair to the school gate. Xiaoman buys a bun at the stall
/// outside, breaks half of it for the cat under the stair, and kneels to ask the cat what it is staring at in the empty
/// corner of the hall. The prism cannot be used here. <see cref="Finished"/> follows when she reaches the school gate.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class SchoolRoad : Node
{
    private const int BunPrice = 5;
    private const float StandingEyes = 1.3f;
    private const float KneelingEyes = .75f;
    private const double KneelSeconds = .45;
    // How near the school gate the player is when the scene is over.
    private const float GateReach = 1.5f;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The money Xiaoman starts with: the two yuan from the table.</summary>
    public Wallet Wallet { get; set; } = new(20);

    public Region3D Region => street.Region;

    private Street street = null!;
    private Character3D coal = null!;
    private bool hasBun;
    private bool fed;
    private bool finished;

    private GameHud Hud => street.Hud;

    public override void _Ready()
    {
        street = new Street(this);
        street.UseProps("BunStall", "CatFeed", "ToyStall");
        coal = Region.Character("coal");
        // On this road the cat is fed, not carried. The shape is switched off for the physics, which is not done in a callback.
        coal.GetNode<CollisionShape3D>("Pickup/Shape").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        coal.Sit();
        var stall = street.Prop("BunStall");
        stall.Prompt = $"买包子（{Wallet.Format(BunPrice)}）";
        stall.Interacted += BuyBun;
        var cat = street.Prop("CatFeed");
        cat.Prompt = "看看煤球";
        cat.Interacted += TalkToCat;
        var toys = street.Prop("ToyStall");
        toys.Prompt = "看看小摊";
        toys.Interacted += () => Hud.Say("小满", "玻璃弹珠，一毛钱三颗……放学再来看吧。", 3);
        Hud.Say("", "楼道里飘着邻居家炒菜的油烟味。", 3);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (finished) return;
        var gate = street.Mark("SchoolGate").GlobalPosition;
        var player = street.Player.GlobalPosition;
        if (new Vector2(gate.X - player.X, gate.Z - player.Z).Length() > GateReach) return;
        finished = true;
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
        street.Prop("CatFeed").Prompt = "掰一半喂煤球";
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
        street.Prop("CatFeed").CollisionLayer = 0;
        var corner = street.Mark("EmptyCorner").GlobalPosition;
        var eyes = street.Player.Camera;
        var beat = CreateTween();
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "来，掰一半给你。", 2)));
        beat.TweenInterval(3);
        beat.TweenCallback(Callable.From(() => coal.GetNode<Cat>("Cat").Stare(corner)));
        beat.TweenInterval(1.5);
        beat.TweenProperty(eyes, "position:y", KneelingEyes, KneelSeconds);
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "你看什么呢？", 2.5)));
        beat.TweenInterval(3);
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "那里明明什么也没有……", 3)));
        beat.TweenInterval(2);
        beat.TweenProperty(eyes, "position:y", StandingEyes, KneelSeconds);
        beat.TweenCallback(Callable.From(() => Hud.Say("小满", "算了，要迟到了。", 2.5)));
    }
}
