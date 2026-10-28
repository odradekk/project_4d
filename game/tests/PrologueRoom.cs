using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Game.Rendering;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// Scene 8 as RoomFlowChecks plays it, with the moves that only get the cat to the next stage left out: the flap is opened with the
// device, and the ball is put over the pit. Scene 9 plays in the same room: she follows the cat through the seams and stands where
// she is until the grazer is on her.
public partial class PrologueChecks
{
    private const float Floor = .02f;
    private static readonly Vector3 Seam = new(7.1f, Floor, 4.3f);
    private static readonly Vector3 SeamToDeep = new(6.25f, Floor, 4.3f);

    private async Task<Region3D> PlayCatRoom()
    {
        await Arrive(8, 1, 2, 3, 4, 8);
        await play.Use(Region);
        // The cat's leap through the ring has to pass within 15 cm of the seam, and at a faster pace the ticks of the leap are longer than that.
        speed = 1;
        var room = (CatRoom)prologue.Scene!;
        var actions = Region.Actions!;
        var ask = new RoomQueries(Region);
        var world = Region.World!;
        Check(Player.Hud.NotebookAvailable, "the notebook can be opened in the room");

        // The cat leaps into the seam once she looks at it with the prism up.
        play.LookAt(new(4.8f, Floor, 4.9f), new(7.1f, .5f, 4.3f));
        Button(MouseButton.Right, true);
        await Until(() => room.Entries(CatRoom.Stage.Perched) > 0, 10, "the cat must leap into the seam");
        Button(MouseButton.Right, false);

        // The wand is flipped over the guard, picked up and stood up toward section II, and shaken there.
        play.LookAt(new(5.44f, Floor, 3.3f), new(5.44f, 1.09f, 2.63f));
        await play.Frames(3);
        Check(actions.Flip() is null, "the wand is flipped over the guard");
        await Until(() => ask.IsResting("wand"), 6, "the wand must come to rest on her side of the guard");
        play.LookAt(new(5.44f, Floor, 3.6f), new(5.44f, .06f, 2.89f));
        await play.Frames(3);
        Check(actions.Use() is null && actions.Held == "wand", "she picks the wand up");
        await Walk(new(5f, Floor, 4f));
        play.LookAt(new(5f, Floor, 4f), new(5f, 1.3f + Floor, 3f));
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
        await CrossSeam(Seam, "Ⅱ");
        await Until(() => Player.Hud.UnlockedPages.Contains(5), 3, "the notebook must have the page about seams");
        await CrossSeam(Seam, "Ⅰ");

        // The nut is stood up in front of the seam, and the yarn is put beyond its ring.
        // The stool, which this run did not use, stands by the desk; it is put where the other checks leave it.
        var stool = ask.State("stool").Pose;
        Check(world.PlaceEntity(new("stool"), stool with { Position = new(4.2, stool.Position.Y, 5.4, stool.Position.W) }) is UpdateResult.Updated, "the stool is put by the door");
        await play.Frames(3);
        play.LookAt(new(6.6f, Floor, 3.2f), new(7.2f, .775f, 3.2f));
        await play.Frames(3);
        Check(actions.Aimed == "nut" && actions.Use() is null && actions.Held == "nut", $"she picks the nut up; the crosshair is on {actions.Aimed}");
        await Walk(new(7.3f, Floor, 4.3f));
        play.LookAt(Seam, Seam + new Vector3(-1, 1.3f, 0));
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
        return Region;
    }

    private async Task PlayDeepRoom(Region3D roomRegion)
    {
        var pages = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await Arrive(9, pages);
        var deep = (DeepRoom)prologue.Scene!;
        // Scene 8 hosts the room, and scene 9 takes it over: the same region, with her and the cat as scene 8 leaves them.
        Check(Region == roomRegion && deep.Region == roomRegion && roomRegion.GetParent() is CatRoom, "scene 9 plays in the room that scene 8 loaded");
        Check(Region.CurrentSection == "Ⅰ" && deep.Now == DeepRoom.Stage.Following && Player.Hud.NotebookAvailable, "she is where scene 8 had her, with the notebook");
        Check(deep.CatBody.Section == "Ⅲ" && deep.CatBody.Mirrored, "the cat is in section III, a mirror image, as scene 8 leaves it");
        speed = 1;
        await play.Use(Region);
        // The yarn of the wand still stands in the seam where she wants to come out, which would stop her: it is put aside.
        var wand = new RoomQueries(Region).State("wand").Pose;
        Check(Region.World!.PlaceEntity(new("wand"), wand with { Position = new(wand.Position.X, wand.Position.Y, wand.Position.Z + 1.5, wand.Position.W) }) is UpdateResult.Updated, "the wand is put aside");
        await play.Frames(3);
        await CrossSeam(Seam, "Ⅱ");
        await CrossSeam(SeamToDeep, "Ⅲ");
        await Until(() => deep.Entries(DeepRoom.Stage.Arrived) > 0, 3, "the scene must know she has arrived");
        deep.CatBody.GetNode<Interactable3D>("Pickup").Interact();
        await Until(() => deep.Entries(DeepRoom.Stage.Panic) > 0, 1, "picking the cat up must start the grazer");
        // Backspace starts the scene again in a room loaded anew, where the cat sits in section III and she has followed it into section II.
        await Again(() => Press(Key.Backspace), 9, pages);
        deep = (DeepRoom)prologue.Scene!;
        Check(Region != roomRegion && !GodotObject.IsInstanceValid(roomRegion), "the room is loaded anew");
        await Until(() => Region.CurrentSection == "Ⅱ", 5, "she must have followed the cat into section II");
        Check(deep.Now == DeepRoom.Stage.Following && deep.CatBody.Section == "Ⅲ" && deep.CatBody.Mirrored && !deep.CatBody.GetNode<Cat>("Cat").Held,
            "the cat sits in section III, as it did");
        await play.Use(Region);
        await CrossSeam(SeamToDeep, "Ⅲ");
        await Until(() => deep.Entries(DeepRoom.Stage.Arrived) > 0, 3, "the scene must know she has arrived");
        deep.CatBody.GetNode<Interactable3D>("Pickup").Interact();
        // She stands where she is. The grazer takes its bites and comes for her, the cat tears loose, and the screen cuts to black.
        speed = 6;
        await Until(() => deep.Cut is not null, 250, "the screen must cut to black");
        speed = 1;
        await Until(() => deep.Cut!.TitleVisible, 10, "the title must come up");
        Check(deep.Cut!.TitleText != "", "the title is on the black screen");
    }

    // She stands at a seam, raises the lens, scrolls to the section she means to go to and clicks.
    private async Task CrossSeam(Vector3 seam, string target)
    {
        await Walk(seam);
        play.LookAt(seam, seam + new Vector3(.7f, 1, 0));
        Button(MouseButton.Right, true);
        await play.Frames(3);
        var prism = Region.GetNode<PrismView>("Generated/Prism");
        for (var scrolls = 0; prism.TargetSection != target; scrolls++)
        {
            Check(scrolls < 6, $"the lens must be able to show section {target}; it shows {prism.TargetSection}");
            Button(scrolls < 3 ? MouseButton.WheelDown : MouseButton.WheelUp, true);
            await play.Frames(2);
        }
        Button(MouseButton.Left, true);
        await play.Frames(3);
        Button(MouseButton.Left, false);
        Button(MouseButton.Right, false);
        await play.Frames(3);
        Check(Region.CurrentSection == target, $"she crosses into section {target}; she is in {Region.CurrentSection} and the lens shows {prism.TargetSection}");
    }

    // Like the walk of TurningFixture, but on the ground plane: she settles a little below the height of the feet that are given.
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
