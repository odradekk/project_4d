using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Flow;
using Game.Interaction;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

// The ball over the pit, and the nut with its ring at the seam.
public partial class RoomFlowChecks
{
    private async Task CheckBall()
    {
        // The stool stood up over the pit is a sheet in page II that the cat does not step on.
        Look(new(4.2f, Floor, 5.4f), new(4.2f, .45f, 4.8f));
        await Frames(3);
        Check(actions.Aimed == "stool" && actions.Use() is null, $"she picks the stool up; the crosshair is on {actions.Aimed}");
        await Walk(new(4.95f, Floor, 4.8f));
        Face(new(4.95f, Floor, 4.8f), new(0, 0, -1));
        await Frames(10);
        room.Home.Hud.ClearText();
        Check(actions.StandOrLay() is null, "the stool can be stood up over the pit");
        await Until(() => room.Home.Hud.SubtitleText.Contains("薄得像张纸"), 6, "she must say that the sheet is too thin to step on");
        Check(room.Now == CatRoom.Stage.AtPit, "the cat does not cross on the stool");
        await Until(() => !ask.State("stool").Rotating, 3, "the stand-up must end");
        Check(actions.StandOrLay() is null, "she lays the stool down again");
        await Until(() => !ask.State("stool").Rotating, 3, "the laying down must end");
        Check(actions.Use() is null && actions.Held is null, "she puts the stool down");
        // The ball is carried to the floor of page I over the pit, where page II's slice of it hangs in the hole.
        Look(new(4.3f, Floor, 4.1f), new(4.3f, .25f, 3.5f));
        await Frames(3);
        Check(actions.Aimed == "ball" && actions.Use() is null && actions.Held == "ball", $"she picks the ball up; the crosshair is on {actions.Aimed}");
        await Walk(new(4.95f, Floor, 4.8f));
        Face(new(4.95f, Floor, 4.8f), new(0, 0, -1));
        await Frames(10);
        Check(room.Now == CatRoom.Stage.AtPit, "the cat does not cross on a ball in her hand");
        Check(actions.Use() is null, "she puts the ball down");
        await Until(() => ask.IsResting("ball"), 3, "the ball must come to rest");
        var slice = ask.Bounds("ball", "Ⅱ")!.Value;
        Check(ask.State("ball").Pose.Position.Y is > .24 and < .26 && slice.Position.Y > .1f && slice.Position.Y < .13f,
            $"the ball rests on the floor of page I and hangs 11 cm over the pit in page II: {slice}");
        await Until(() => room.Now == CatRoom.Stage.Chasing, 8, "the cat must cross on the ball");
        Check(cat.Section == "Ⅱ" && cat.Position.Z < 3.95f && cat.Position.Y < .3f, $"the cat is across the pit: {cat.Position}");
    }

    private async Task CheckRing()
    {
        // The nut is taken from the desk and carried to the wall.
        Look(new(6.6f, Floor, 3.2f), new(7.2f, .775f, 3.2f));
        await Frames(3);
        Check(actions.Aimed == "nut" && actions.Use() is null && actions.Held == "nut", $"she picks the nut up; the crosshair is on {actions.Aimed}");
        await Walk(new(7.3f, Floor, 4.3f));
        // Facing the wall, the ring would turn into it: red lines, and a stand-up that is refused.
        Face(new(7.3f, Floor, 4.3f), new(1, 0, 0));
        Button(MouseButton.Right, true);
        await Frames(25);
        Check(Lines(LensOverlay.Blocked).Any(), "the ring of a nut against the wall is drawn red");
        await Frames(40);
        Check(Lines(LensOverlay.Clear).Any() && Lines(LensOverlay.Blocked).Any(), "a still nut shows its sweep pale up to the wall and red from it");
        room.Home.Hud.ClearText();
        Check(actions.StandOrLay() is { Code: FailureCode.RotationBlocked }, "against the wall the stand-up is refused");
        Check(!ask.State("nut").Rotating && ask.Bounds("nut", "Ⅱ", "ring") is null, "the nut did not turn");
        // In the gap between desk and wardrobe, facing into the room, the ring is set up in front of the seam.
        Face(Seam, new(-1, 0, 0));
        await Frames(20);
        Check(Lines(LensOverlay.Blocked).Count() == 0 && Lines(LensOverlay.Clear).Any(), "facing into the room the ring is drawn pale");
        Check(actions.StandOrLay() is null, "the nut can be stood up facing into the room");
        await Until(() => !ask.State("nut").Rotating, 3, "the stand-up must end");
        Button(MouseButton.Right, false);
        var ring = ask.Bounds("nut", "Ⅱ", "ring");
        Check(ring is not null, "the ring of the nut is in page II");
        Near(ring!.Value.GetCenter(), new(6.71f, .9f, 4.3f), "the ring stands in front of the seam at the height of her chest");
        Check(actions.Use() is null && actions.Held is null, "she lets the nut go and the ring stays");
        // The yarn is put beyond the ring, as if she had waved the wand there.
        var wand = ask.State("wand");
        var yarn = ask.Bounds("wand", "Ⅱ", "yarn")!.Value.GetCenter();
        var at = wand.Pose.Position;
        var shifted = wand.Pose with { Position = new(at.X + 7f - yarn.X, at.Y, at.Z + 4.3f - yarn.Z, at.W) };
        Check(room.Home.Region.World!.PlaceEntity(new("wand"), shifted) is UpdateResult.Updated, "the wand is put where the yarn is beyond the ring");
        // The cat leaps; picked up in the middle of the leap it is sent to the lure again, and leaps once more.
        await Until(() => room.Now == CatRoom.Stage.Leaping, 20, "the cat must run up to the ring and leap");
        // She is in page I and the cat in page II: the pick-up ends at once, the way a command is dropped when the cat is let go.
        cat.GetNode<Interactable3D>("Pickup").Interact();
        await Frames(5);
        player.PutDown?.Invoke();
        await Until(() => room.Now == CatRoom.Stage.Leaping, 20, "the cat must leap again");
        var finished = false;
        room.Finished += () => finished = true;
        await Until(() => room.Now == CatRoom.Stage.Gone, 10, "the cat must vanish in the middle of the leap");
        Check(cat.Section == "Ⅲ" && cat.Mirrored, $"the cat is in page III and mirrored: {cat.Section}");
        Check(room.Home.Hud.UnlockedPages.Contains(7), "the notebook has the page about the three kinds of things");
        await Until(() => finished, 8, "the scene must end");
    }

    // The path of the lines drawn in the lens of page II, of a color.
    private System.Collections.Generic.IEnumerable<Vector3> Lines(Color color)
    {
        var preview = room.Home.Region.GetNodeOrNull<MeshInstance3D>("Generated/Ⅱ/Overlay/Preview");
        for (var i = 0; preview?.Mesh is not null && i < preview.Mesh.GetSurfaceCount(); i++)
            if (((StandardMaterial3D)preview.Mesh.SurfaceGetMaterial(i)).AlbedoColor == color)
                foreach (var vertex in preview.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()) yield return vertex;
    }
}
