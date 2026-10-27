using System;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The player carries the cat: while it is in the arms the crosshair still finds what is ahead, E still uses an
/// interactable, and any other command puts the cat down.
/// </summary>
public partial class CatHoldChecks : Node
{
    private static readonly Vector3 CatAt = new(-1.5f, .02f, 3);

    private TurningFixture play = null!;
    private int pulled;
    private FirstPersonPlayer player => play.Player;
    private Character3D catBody => play.Region.Character("cat");
    private Cat cat => catBody.GetNode<Cat>("Cat");
    private Interactable3D pickup => catBody.GetNode<Interactable3D>("Pickup");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await Fresh();
            await CheckPickUpAndPutDown();
            await Fresh();
            await CheckAnotherCommandPutsTheCatDown();
            await Fresh(poleOnPlatform: true);
            await CheckAimingWhileHeld();
            await Fresh(poleOnPlatform: true, schoolmate: true);
            await CheckAimingPastACharacter();
            await Fresh();
            await CheckInteractingWhileHeld();
            GD.Print("Cat hold checks passed: pick up, put down, other commands, aim, aim past a character, interact.");
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

    private Task Fresh(bool poleOnPlatform = false, bool schoolmate = false)
    {
        pulled = 0;
        return play.Load(region =>
        {
            AddCat(region, "cat", CatAt);
            AddHatch(region);
            if (schoolmate) AddSchoolmate(region, "mate", new(.5f, .02f, .85f));
            // The platform spans x -1 to 1, z -0.5 to 0.5 and y 0 to 0.6, so a pole on it is in the line of the crosshair 30 degrees down.
            if (poleOnPlatform) region.GetNode<TurnableObject3D>("Sections/Ⅰ/Turnables/Pole").Position = new(.5f, .63125f, 0);
        });
    }

    // An interactable in the middle of the room, like the hatch the player pulls with a cat in the arms.
    private void AddHatch(Region3D region)
    {
        var hatch = new Interactable3D { Prompt = "拉开", Position = new(1.5f, 1.3f, .6f) };
        hatch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.4f, .4f, .4f) } });
        hatch.Interacted += () => pulled++;
        region.AddChild(hatch);
    }

    private async Task PickUp()
    {
        catBody.PlaceAt(CatAt, 0);
        await play.Frames(3);
        play.LookAt(new(CatAt.X, .02f, 4.2f), catBody.Position + new Vector3(0, .2f, 0));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
    }

    private async Task CheckPickUpAndPutDown()
    {
        play.LookAt(new(CatAt.X, .02f, 4.2f), CatAt + new Vector3(0, .2f, 0));
        await play.Frames(2);
        Check(player.Hud.HintText == "E  抱起", $"the prompt offers to pick the cat up; saw '{player.Hud.HintText}'");
        var layer = pickup.CollisionLayer;
        var pickedUp = 0;
        var putDown = 0;
        cat.PickedUp += () => pickedUp++;
        cat.PutDown += () => putDown++;
        Press(Key.E);
        await play.Frames(3);
        Check(cat.Held && pickedUp == 1 && player.PutDown is not null && player.Hud.HintText == "E  放下", "a picked-up cat is in the player's arms");
        Check(pickup.CollisionLayer == 0, "a cat in the arms cannot be aimed at");
        player.Position = new(2, .02f, 3);
        await play.Frames(3);
        var held = player.Position + player.Basis * new Vector3(0, 1, -.45f);
        Check(catBody.Position.DistanceTo(held) < .05f, $"the cat goes where the player goes; it is at {catBody.Position}, the arms at {held}");
        Press(Key.E);
        await play.Frames(90);
        Check(!cat.Held && putDown == 1 && !catBody.Busy && player.PutDown is null, "E puts the cat down");
        Check(catBody.Position.Y < .05f, $"a cat put down lands on the floor; it is at {catBody.Position}");
        Check(pickup.CollisionLayer == layer, "a cat on the floor can be picked up again");
    }

    private async Task CheckAnotherCommandPutsTheCatDown()
    {
        var layer = pickup.CollisionLayer;
        var commands = new (string Name, Action Command)[]
        {
            ("a command to the cat", () => cat.Flee(player.Position)),
            ("a command to its body", () => catBody.GoTo(new(-3, 0, 3))),
            ("stopping its body", catBody.Stop)
        };
        foreach (var (name, command) in commands)
        {
            await PickUp();
            Check(cat.Held && player.PutDown is not null, $"the cat is picked up before {name}");
            command();
            await play.Frames(3);
            Check(!cat.Held && player.PutDown is null && player.Hud.HintText != "E  放下" && pickup.CollisionLayer == layer,
                $"{name} puts the cat down: the arms are free, the prompt is gone and the cat can be picked up again");
        }
        await PickUp();
        Check(player.PutDown is not null, "a cat that was put down by a command is picked up with one E");
        Press(Key.E);
        await play.Frames(3);
    }

    private async Task CheckAimingWhileHeld()
    {
        await PickUp();
        play.LookAt(new(.5f, .02f, 1.3f), new(.5f, .63125f, 0));
        await play.Frames(3);
        Press(Key.F);
        await play.Frames(5);
        Check(play.State("pole").Rotating, "F turns what the crosshair is on while the cat is in the arms");
        await play.Frames(TurnFrames);
    }

    private async Task CheckAimingPastACharacter()
    {
        // A schoolmate stands between the player and the pole, in the line of the crosshair, and does not hide it.
        play.LookAt(new(.5f, .02f, 1.3f), new(.5f, .63125f, 0));
        await play.Frames(3);
        Press(Key.F);
        await play.Frames(5);
        Check(play.State("pole").Rotating, "a character in front of an object does not stop the crosshair from finding the object");
    }

    private async Task CheckInteractingWhileHeld()
    {
        await PickUp();
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, 1.3f, .6f));
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("拉开"), $"the prompt of what the crosshair is on shows while the cat is in the arms; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(pulled == 1 && player.PutDown is not null, "E uses the interactable and keeps the cat in the arms");
        play.LookAt(new(1.5f, .02f, 2.4f), new(-2, 1.3f, 2.4f));
        await play.Frames(2);
        Check(player.Hud.HintText == "E  放下", $"with nothing to use ahead, the prompt is to put the cat down; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(pulled == 1 && player.PutDown is null, "E with nothing to use ahead puts the cat down");
    }
}
