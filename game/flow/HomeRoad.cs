using System.Linq;
using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 6 of the prologue, from the school gate to the door of the home at dusk. At the kiosk Xiaoman picks one thing
/// with the change from the morning and buys a ham sausage for the cat, which waits in the mouth of the stairwell and
/// follows her up the stair; at the door she picks it up and takes it in. The scene ends there: <see cref="Finished"/>
/// follows, and the home of scene 7 is the next scene's. The prism cannot be used here.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class HomeRoad : Node
{
    private const int SausagePrice = 5;
    // The cat gets up and follows when the player is this near.
    private const float CatFollowRange = 4f;
    // The cat is at the door of the home when it is up on the landing and near the player.
    private const float LandingHeight = 2.5f;
    private const float CatAtDoorRange = 3f;
    // The player is up the stair when she is higher than this.
    private const float StairHeight = 1f;
    // A cat that walks to a point stops within a few centimetres of it.
    private const float AtMouth = .2f;

    private static readonly (string Prop, string Name, int Price)[] Goods =
    [
        ("IcePop", "冰棍", 3), ("Soda", "汽水", 10), ("Spicy", "辣条", 2), ("Stickers", "贴纸", 5)
    ];

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The money Xiaoman has left from the morning: the two yuan less the bun.</summary>
    public Wallet Wallet { get; set; } = new(15);

    public Region3D Region => street.Region;

    private Street street = null!;
    private Character3D coal = null!;
    private Cat brain = null!;
    private string? picked;
    private bool hasSausage;
    private bool following;
    private bool catAtMouth;

    private GameHud Hud => street.Hud;

    public override void _Ready()
    {
        street = new Street(this);
        street.UseProps([.. Goods.Select(goods => goods.Prop), "Sausage", "HomeDoor"]);
        street.PlacePlayer("GateStart");
        coal = Region.Character("coal");
        brain = coal.GetNode<Cat>("Cat");
        var waits = street.Mark("CatWaits");
        coal.PlaceAt(waits.GlobalPosition, Street.Yaw(waits));
        coal.Sit();
        foreach (var (prop, name, price) in Goods)
        {
            var goods = street.Prop(prop);
            goods.Prompt = $"买{name}（{Wallet.Format(price)}）";
            goods.Interacted += () => Pick(name, price);
        }
        var sausage = street.Prop("Sausage");
        sausage.Prompt = $"买火腿肠（{Wallet.Format(SausagePrice)}）";
        sausage.Interacted += BuySausage;
        var door = street.Prop("HomeDoor");
        door.Prompt = "开门";
        door.Interacted += OpenDoor;
        Hud.Say("", "放学了。黄昏的光斜斜地照在街上。", 3.5);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (following || Distance(street.Player.GlobalPosition, coal.GlobalPosition) > CatFollowRange) return;
        following = true;
        brain.Follow(TowardStairs);
        Hud.Say("小满", "煤球！", 1.5);
    }

    // One thing of the four, and not a second.
    private void Pick(string name, int price)
    {
        if (picked is not null)
        {
            Hud.Say("小满", "买一样就够了，还得给煤球买火腿肠。", 3);
            return;
        }
        if (!street.Buy(Wallet, name, price)) return;
        picked = name;
        Region.Character("clerk").Say("拿好。", 1.5);
        SayWhenDone();
    }

    private void BuySausage()
    {
        if (hasSausage)
        {
            Hud.Say("小满", "火腿肠已经买了。", 2);
            return;
        }
        if (!street.Buy(Wallet, "火腿肠", SausagePrice)) return;
        hasSausage = true;
        Region.Character("clerk").Say("拿好。", 1.5);
        SayWhenDone();
    }

    private void SayWhenDone()
    {
        if (picked is not null && hasSausage) Hud.Say("小满", "好了，回家。煤球还在等我呢。", 3);
    }

    // The cat goes straight at the player, which would take it under the ramp from the side. So while the player is up the
    // stair and the cat has not yet been at the mouth of the ramp, it goes there first, and from there straight up.
    private Vector3 TowardStairs(Vector3 player)
    {
        var mouth = street.Mark("RampMouth").GlobalPosition;
        catAtMouth |= Distance(coal.GlobalPosition, mouth) < AtMouth;
        return player.Y > StairHeight && !catAtMouth ? mouth : player;
    }

    private void OpenDoor()
    {
        if (!hasSausage)
        {
            Hud.Say("小满", "还没给煤球买火腿肠呢。", 3);
            return;
        }
        var catIsWithHer = brain.Held
            || (coal.Position.Y > LandingHeight && Distance(street.Player.GlobalPosition, coal.GlobalPosition) < CatAtDoorRange);
        if (!catIsWithHer)
        {
            Hud.Say("小满", "煤球呢？", 2.5);
            return;
        }
        street.Prop("HomeDoor").CollisionLayer = 0;
        if (!brain.Held) coal.GetNode<Interactable3D>("Pickup").Interact();
        var ending = CreateTween();
        ending.TweenCallback(Callable.From(() => Hud.Say("", "小满左右看看，楼道里没有人，把煤球抱进怀里。", 3.5)));
        ending.TweenInterval(3.5);
        ending.TweenCallback(Callable.From(() => Hud.Say("小满", "爸不让养猫，你可别出声啊。", 3.5)));
        ending.TweenInterval(3.5);
        ending.TweenCallback(Callable.From(() => EmitSignal(SignalName.Finished)));
    }

    private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
