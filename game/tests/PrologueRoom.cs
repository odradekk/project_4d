using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Flow;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scene 8 as RoomFlowChecks plays it, with the moves that only get the cat to the next stage left out: the flap is opened with the
// device, and the ball is put over the pit.
public partial class PrologueChecks
{
    private const float Floor = .02f;
    private static readonly Vector3 Seam = new(7.1f, Floor, 4.3f);

    private async Task PlayCatRoom()
    {
        await Arrive(8, 1, 2, 3, 4, 8);
        // The cat's leap through the ring has to pass within 15 cm of the seam, and at a faster pace the ticks of the leap are longer than that.
        speed = 1;
        var room = (CatRoom)prologue.Scene!;
        var actions = Region.Actions!;
        var ask = new RoomQueries(Region);
        var world = Region.World!;
        Check(Player.Hud.NotebookAvailable, "the notebook can be opened in the room");

        // The cat leaps into the seam once she looks at it with the prism up.
        Look(new(4.8f, Floor, 4.9f), new(7.1f, .5f, 4.3f));
        Button(MouseButton.Right, true);
        await Until(() => room.Entries(CatRoom.Stage.Perched) > 0, 10, "the cat must leap into the seam");
        Button(MouseButton.Right, false);

        // The wand is flipped over the guard, picked up and stood up toward section II, and shaken there.
        Look(new(5.44f, Floor, 3.3f), new(5.44f, 1.09f, 2.63f));
        await play.Frames(3);
        Check(actions.Flip() is null, "the wand is flipped over the guard");
        await Until(() => ask.IsResting("wand"), 6, "the wand must come to rest on her side of the guard");
        Look(new(5.44f, Floor, 3.6f), new(5.44f, .06f, 2.89f));
        await play.Frames(3);
        Check(actions.Use() is null && actions.Held == "wand", "she picks the wand up");
        await Walk(new(5f, Floor, 4f));
        Face(new(5f, Floor, 4f), new(0, 0, -1));
        await play.Frames(10);
        Check(actions.StandOrLay() is null, "the wand is stood up toward section II");
        await Until(() => !ask.State("wand").Rotating, 3, "the stand-up must end");
        var hand = Player.Position;
        for (var swing = 0; swing < 6; swing++)
            for (var frame = 0; frame < 10; frame++)
            {
                Player.Position += new Vector3(swing % 2 == 0 ? .04f : -.04f, 0, 0);
                await play.Frames(1);
            }
        Player.Position = hand;
        await play.Frames(4);
        Check(actions.Use() is null && actions.Held is null, "she lets the stood-up wand go and it stays");

        // The flap opens, the board comes down, and the cat comes down it for the yarn.
        Check(world.DriveDevice(new("cabinet"), -Math.PI / 2) is DriveDeviceResult.Driven, "the flap of the cabinet opens");
        await Until(() => room.Entries(CatRoom.Stage.AtPit) > 0, 20, "the cat must come down the board");

        // The ball over the pit is the cat's way across.
        var pose = ask.State("ball").Pose;
        Check(world.PlaceEntity(new("ball"), pose with { Position = new(4.95, pose.Position.Y, 4.2, pose.Position.W) }) is UpdateResult.Updated, "the ball is put over the pit");
        await Until(() => room.Entries(CatRoom.Stage.Chasing) > 0, 15, "the cat must cross on the ball");

        // Through the seam to section II and back draws the page about seams.
        await CrossSeam();
        Check(Region.CurrentSection == "Ⅱ" && Player.Hud.UnlockedPages.Contains(5), "she crosses into section II");
        await CrossSeam();
        Check(Region.CurrentSection == "Ⅰ", "she crosses back");

        // The nut is stood up in front of the seam, and the yarn is put beyond its ring.
        // The stool, which this run did not use, stands by the desk; it is put where the other checks leave it.
        var stool = ask.State("stool").Pose;
        Check(world.PlaceEntity(new("stool"), stool with { Position = new(4.2, stool.Position.Y, 5.4, stool.Position.W) }) is UpdateResult.Updated, "the stool is put by the door");
        await play.Frames(3);
        Look(new(6.6f, Floor, 3.2f), new(7.2f, .775f, 3.2f));
        await play.Frames(3);
        Check(actions.Aimed == "nut" && actions.Use() is null && actions.Held == "nut", $"she picks the nut up; the crosshair is on {actions.Aimed}");
        await Walk(new(7.3f, Floor, 4.3f));
        Face(Seam, new(-1, 0, 0));
        await play.Frames(20);
        Check(actions.StandOrLay() is null, "the nut is stood up facing into the room");
        await Until(() => !ask.State("nut").Rotating, 3, "the stand-up must end");
        Check(actions.Use() is null && actions.Held is null, "she lets the nut go and the ring stays");
        Near(ask.Bounds("nut", "Ⅱ", "ring")!.Value.GetCenter(), new(6.71f, .9f, 4.3f), "the ring stands in front of the seam at the height of her chest");
        var yarn = ask.Bounds("wand", "Ⅱ", "yarn")!.Value.GetCenter();
        var wand = ask.State("wand").Pose;
        Check(world.PlaceEntity(new("wand"), wand with { Position = new(wand.Position.X + 7f - yarn.X, wand.Position.Y, wand.Position.Z + 4.3f - yarn.Z, wand.Position.W) }) is UpdateResult.Updated,
            "the wand is put where the yarn is beyond the ring");
        await Until(() => room.Entries(CatRoom.Stage.Gone) > 0, 30, "the cat must leap through the ring and vanish");
    }

    private async Task CrossSeam()
    {
        await Walk(Seam);
        Look(Seam, new(7.1f, 1.3f, 3.3f));
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Button(MouseButton.Left, true);
        await play.Frames(10);
        Button(MouseButton.Left, false);
        Button(MouseButton.Right, false);
        await play.Frames(3);
    }

    // Puts her feet at a place with the eyes level, turned toward a point.
    private void Look(Vector3 feet, Vector3 target)
    {
        Player.Position = feet;
        Player.Velocity = Vector3.Zero;
        var offset = target - (feet + new Vector3(0, 1.3f, 0));
        Player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        Player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
    }

    private void Face(Vector3 feet, Vector3 direction) => Look(feet, feet + new Vector3(0, 1.3f, 0) + direction);

    // Half a metre per frame, so that what she carries is moved the way a walk moves it.
    private async Task Walk(Vector3 feet)
    {
        for (var frames = 0; Flat(Player.Position - feet).Length() > .001f; frames++)
        {
            Check(frames < 200, $"she must be able to stand at {feet} but is at {Player.Position}");
            Player.Position = Player.Position.MoveToward(new(feet.X, Player.Position.Y, feet.Z), .5f);
            await play.Frames(1);
        }
    }
}
