using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Characters and the seams of the seam test region: a cat crosses into the physics world of another section, is turned into
/// its mirror image by the mirror seam and is refused where the engine refuses it; the player's own crossing puts down the
/// cat in her arms; and her arms hold one thing, a cat or an object.
/// </summary>
public partial class CatSeamChecks : Node
{
    private static readonly Vector3 CatAt = new(-1, .02f, 3.2f);

    private TurningFixture play = null!;
    private SeamPlay seam = null!;
    private Region3D region => play.Region;
    private FirstPersonPlayer player => play.Player;
    private Character3D catBody => region.Character("cat");
    private Cat cat => catBody.GetNode<Cat>("Cat");
    private Interactable3D pickup => catBody.GetNode<Interactable3D>("Pickup");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        seam = new SeamPlay(this, play);
        try
        {
            await Fresh();
            await CheckCatCrosses();
            await Fresh();
            await CheckCatIsRefused();
            await Fresh();
            await CheckCrossingPutsTheCatDown();
            await Fresh();
            await CheckCatOrObject();
            await Fresh();
            await CheckBandBehindACat();
            await play.Unload();
            GD.Print("Cat seam checks passed: crossing, mirror, refusals, carried cat, arms, band behind a cat.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private Task Fresh() => seam.Load(region =>
    {
        AddCat(region, "cat", CatAt);
        AddSchoolmate(region, "mate", new(3, .02f, .5f));
    });

    private async Task CheckCatCrosses()
    {
        var second = region.GetNode<SubViewport>("Generated/Ⅱ");
        catBody.PlaceAt(new(-1.5f, .02f, 1.5f), 0);
        Check(catBody.CrossSeam("needle", "Ⅱ") is null, "a cat crosses a plain seam without a prism");
        Check(catBody.Section == "Ⅱ" && catBody.GetParent() == second && catBody.GetWorld3D() == second.World3D,
            "a cat that crossed is in the physics world of the section it came to");
        Check(Apart(catBody.Position, new(-1.5f, 0, 1.5f)) < .001f && !catBody.Mirrored, "a plain seam keeps the place and the handedness of the cat");
        Check(play.State("cat").Section == new SectionId("Ⅱ") && play.State("cat").Pose.Position.W == .42, "the engine has the cat in the section it came to");
        await play.Frames(60);
        Check(catBody.IsOnFloor() && catBody.Position.Y > -.05f, "the cat stands on the floor of the section it came to");
        Check(catBody.CrossSeam("needle", "Ⅰ") is null && catBody.GetParent() == region.GetNode("Generated/Ⅰ"), "a seam goes both ways");

        // The mirror seam is the plane x = 1.5, so a cat 5 cm before it comes out 5 cm behind it.
        catBody.PlaceAt(new(1.45f, .02f, 1.5f), 0);
        Check(catBody.CrossSeam("glass", "Ⅱ") is null, "a cat crosses a mirror seam");
        Near(catBody.Position with { Y = 0 }, new(1.55f, 0, 1.5f), "a mirror seam puts the cat on the other side of the seam plane");
        Check(catBody.Mirrored && play.State("cat").Handedness == Handedness.Mirrored, "the engine turns the cat into its mirror image");
        await play.Frames(2);
        Check(catBody.GetNode<Node3D>("Visual").Scale.X < 0, "a mirrored cat is drawn as its mirror image");
        Check(catBody.CrossSeam("glass", "Ⅰ") is null && !catBody.Mirrored, "crossing a mirror seam again restores the cat");
    }

    private async Task CheckCatIsRefused()
    {
        var mate = region.Character("mate");
        catBody.PlaceAt(new(3.5f, .02f, -1.5f), 0);
        Check(catBody.CrossSeam("cut", "Ⅱ") == FailureCode.SeamSevered, "a severed seam is refused");
        catBody.PlaceAt(new(-.5f, .02f, 1.5f), 0);
        Check(catBody.CrossSeam("needle", "Ⅱ") == FailureCode.WrongSection, "a seam a metre away is refused");
        mate.PlaceAt(new(-1.5f, .02f, 1.5f), 0);
        Check(mate.CrossSeam("needle", "Ⅱ") == FailureCode.RequiresPrism, "a character that cannot use seams without a prism is refused");
        await play.Frames(2);
        Check(catBody.Section == "Ⅰ" && catBody.GetParent() == region.GetNode("Generated/Ⅰ") && mate.Section == "Ⅰ" && mate.GetParent() == region.GetNode("Generated/Ⅰ"),
            "a refused character stays where it was");
    }

    // The player stands before the cat and picks it up.
    private async Task Carry()
    {
        await seam.Stand(new(CatAt.X, .02f, CatAt.Z + 1.2f), catBody.Position + new Vector3(0, .2f, 0));
        Press(Key.E);
        await play.Frames(3);
        Check(cat.Held && player.PutDown is not null, "the cat is in the arms");
    }

    private async Task CheckCrossingPutsTheCatDown()
    {
        await Carry();
        // She walks to the plain seam with the cat in her arms and crosses it with the lens up, as the player does.
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await seam.RaiseLens();
        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        await play.Frames(3);
        Check(!cat.Held && player.PutDown is null && pickup.CollisionLayer != 0, "a cat is put down when she crosses a seam");
        Check(catBody.Section == "Ⅰ" && catBody.GetParent() == region.GetNode("Generated/Ⅰ"), "the cat stays in the section she left");
        Check(player.Hud.HintText != "E  放下", "she is not offered to put down a cat she no longer carries");
    }

    // The strength of the band the lens draws for the mirror seam, 0 when it draws none.
    private async Task<float> BandStrength()
    {
        await play.Frames(3);
        // The lens reads the physics of the section it shows in its own process, which comes after the signal of the frame.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var lens = (ShaderMaterial)region.GetNode<TextureRect>("Generated/Prism/Lens").Material;
        return lens.GetShaderParameter("band_bottom").AsVector4Array()[0].W;
    }

    // The lens shows II from 1.5 m away, level with the mirror seam, and the cat is held up at the height of the camera, a little
    // above where it falls from. A cat in II hides the band when its body is in the way,
    // and does not when only the sphere around it that the crosshair picks it up by is.
    private async Task CheckBandBehindACat()
    {
        catBody.PlaceAt(new(1.45f, .02f, 1.5f), 0);
        Check(catBody.CrossSeam("glass", "Ⅱ") is null, "the cat is in the section the lens shows");
        await seam.RaiseLens();
        play.LookAt(new(1.5f, 0, 3), new(1.5f, 1.3f, 1.5f));
        Check(await BandStrength() > 0, "the band is drawn with nothing in the way");
        // The cat is held where it is put, so that however many physics steps a frame takes it does not fall out of the way.
        catBody.PlaceAt(new(1.5f, 1.1f, 2.2f), 0);
        catBody.SetPhysicsProcess(false);
        Check(await BandStrength() == 0, "a cat in the way hides the band");
        catBody.PlaceAt(new(1.7f, 1.1f, 2.2f), 0);
        Check(await BandStrength() > 0, "a cat beside the way, with only the sphere it is picked up by in it, does not");
    }

    private async Task CheckCatOrObject()
    {
        // An object in the hand: the crosshair does not find the cat, so the prompt is the object's and E lets go of it.
        await seam.Stand(new(CatAt.X, .02f, CatAt.Z + 1.2f), catBody.Position + new Vector3(0, .2f, 0));
        await seam.Hold("pole");
        await play.Frames(2);
        Check(!player.Hud.HintText.Contains("抱起") && player.Hud.HintText.Contains("E  放下"),
            $"with an object in the hand she is not offered the cat; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(60);
        Check(!cat.Held && play.Actions.Held is null, "E lets go of the object, and does not pick up the cat");

        // The cat in the arms: the backpack is not opened, and E puts the cat down before it takes anything.
        seam.Pack("rubber");
        await Carry();
        Press(Key.Key1);
        await play.Frames(3);
        Check(seam.InBackpack.SequenceEqual(["rubber"]) && play.Actions.Held is null && seam.Said == "小满：手里抱着猫呢。",
            $"with the cat in the arms nothing comes out of the backpack; she said \"{seam.Said}\"");
        // The pole lies where the player will stand 0.8 m before it, on the floor and at rest, whatever happened to it before.
        Check(seam.World.PlaceEntity(new("pole"), play.State("pole").Pose with { Position = new(-1, .03125, 3.8, 0) }) is UpdateResult.Updated, "the pole is put on the floor");
        await play.Frames(5);
        var pole = play.Position("pole");
        play.LookAt(new(pole.X, .02f, pole.Z - .8f), pole);
        await play.Frames(3);
        Check(player.Hud.HintText.Contains("E  放下") && !player.Hud.HintText.Contains("拿起"), $"aiming at an object she is offered to put down the cat; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(!cat.Held && play.Actions.Held is null, "E puts the cat down before it takes the object");
        // The cat lands between her and the pole, where its pick-up sphere is in the way of the crosshair: it is moved aside.
        await play.Frames(60);
        catBody.PlaceAt(new(-4, .02f, 1), 0);
        await play.Frames(3);
        Press(Key.E);
        await play.Frames(3);
        Check(play.Actions.Held == "pole", "and the next E takes it");
        Press(Key.E);
        await play.Frames(60);
        Press(Key.Key1);
        await play.Frames(3);
        Check(play.Actions.Held == "rubber", "with the arms free the backpack opens again");
    }
}
