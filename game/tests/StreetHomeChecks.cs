using System.Threading.Tasks;
using Game.Characters;
using Game.Flow;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// The way home: the kiosk, the door, and the cat that follows up the stair.
public partial class StreetChecks
{
    private async Task CheckKiosk()
    {
        var road = Open<HomeRoad>("home_road");
        await play.Use(road.Region);
        Check(player.Position.X > 60 && Forward(player).Dot(Vector3.Left) > .99f, "scene 6 starts at the school gate, facing home");
        Check(Prop("BunStall").CollisionLayer == 0 && !coal.HasNode("Feed"), "the stall and the feeding of the morning are not used");
        Check(coal.Posture == Posture.Sitting && Apart(coal.Position, new(.8f, 0, -5.9f)) < .2f, "the cat waits in the mouth of the stairwell");
        Check(coal.GetNode<Cat>("Cat").Holdable, "the cat can be picked up on the way home");
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

    // The door opens for neither an empty bag nor a cat that is not in her arms.
    private async Task CheckDoorWaits()
    {
        var road = Open<HomeRoad>("home_road");
        await play.Use(road.Region);
        var finished = 0;
        road.Finished += () => finished++;
        await UseProp(Landing, "HomeDoor", "开门");
        await Says("没给煤球买火腿肠");
        Check(finished == 0, "the door does not open without the sausage");
        road.Wallet = new(5);
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        await UseProp(Landing, "HomeDoor", "开门");
        await Says("先把煤球抱起来");
        Check(finished == 0, "the door does not open without the cat in her arms");
        await Unload();
    }

    // The cat sits where it waits until the player is near, follows her up the stair, and is taken in at the door.
    private async Task CheckCatFollowsFromWaiting()
    {
        var road = await StartHome();
        var finished = 0;
        road.Finished += () => finished++;
        await play.Frames(30);
        Check(coal.Posture == Posture.Sitting && !coal.Busy, "the cat does not follow from afar");
        await WalkTo(Entrance);
        await play.Until(() => coal.Busy, 2, "the cat must get up and follow");
        Check(coal.Posture == Posture.Standing, "the cat stands up to follow");
        await Says("煤球！", 12);
        await ClimbToLanding();
        await CatIsOnTheLanding();
        await PickUpCat();

        await UseProp(Landing, "HomeDoor", "开门");
        Check(Prop("HomeDoor").CollisionLayer == 0, "the door is spent");
        await play.Until(() => subtitle == "小满：爸不让养猫，你可别出声啊。", 5, "she must tell the cat to keep quiet");
        Check(finished == 0, "the scene is not over before she has said it");
        await play.Until(() => finished == 1, 5, "the scene must end at the door");
        await Unload();
    }

    // She goes to the far end of the hall first, to look at the empty corner, and the cat is with her under the landing.
    private async Task CheckCatFollowsFromTheEndOfTheHall()
    {
        await StartHome();
        await WalkTo(Entrance, new Vector3(-.75f, 0, -5.3f), new Vector3(-.75f, 0, -12.8f));
        await play.Until(() => coal.Position.Z < -10 && Apart(coal.Position, player.Position) < 1.6f, 15, "the cat must follow her to the end of the hall");
        await WalkTo(new Vector3(-.75f, 0, -5.3f));
        await ClimbToLanding();
        await CatIsOnTheLanding();
        await Unload();
    }

    // She stands on the left half of the landing, away from the line of the ramp, and the cat must not fall off its side on the way.
    private async Task CheckCatFollowsToTheLeftOfTheLanding()
    {
        await StartHome();
        await WalkTo(Entrance);
        await ClimbToLanding(-1);
        await CatIsOnTheLanding();
        await WalkTo(new Vector3(-.5f, 0, -11.2f));
        await play.Frames(120);
        Check(coal.Position.Y > 2.9f, $"the cat stays on the landing beside her; it is at {coal.Position}");
        await Unload();
    }

    // She picks the cat up and puts it down again before she climbs.
    private async Task CheckCatFollowsAfterBeingCarried()
    {
        await StartHome();
        await WalkTo(Entrance);
        await PickUpCat();
        Press(Key.E);
        await play.Frames(3);
        Check(!coal.GetNode<Cat>("Cat").Held, "she has put the cat down");
        await ClimbToLanding();
        await CatIsOnTheLanding();
        await Unload();
    }

    // She waits for the cat to come to her and settle, and picks it up.
    private async Task PickUpCat()
    {
        await play.Until(() => Apart(coal.Position, player.Position) < 1.6f, 8, "the cat must come to her");
        await play.Frames(60);
        play.LookAt(player.Position, coal.Position + new Vector3(0, .2f, 0));
        await play.Until(() => player.Hud.HintText == "E  抱起", 4, "the cat must be offered for carrying");
        Press(Key.E);
        await play.Frames(3);
        Check(coal.GetNode<Cat>("Cat").Held, "she has the cat in her arms");
    }

    // Scene 6 opened, with the sausage bought and the player in the street in front of the building.
    private async Task<HomeRoad> StartHome()
    {
        var road = Open<HomeRoad>("home_road");
        await play.Use(road.Region);
        await UseProp(Counter(40.25f), "Sausage", "买火腿肠（5 毛）");
        Check(road.Wallet.Mao == 10, "the sausage is bought");
        player.Position = new(0, .02f, -1.5f);
        return road;
    }

    // From the entrance to the landing as a person goes: in at the door and up the ramp from its foot.
    private Task ClimbToLanding(float x = 0, float z = -12.5f) =>
        WalkTo(new Vector3(.75f, 0, -5), new Vector3(.75f, 0, -10), new Vector3(x, 0, z));

    private Task CatIsOnTheLanding() =>
        play.Until(() => coal.Position.Y > 2.9f && Apart(coal.Position, player.Position) < 1.6f, 20, "the cat must follow her up the stair");
}
