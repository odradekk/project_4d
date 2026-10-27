using System;
using System.Threading.Tasks;
using FourD;
using Game.Characters;
using Game.Interaction;
using Godot;
using static Game.Tests.CatWorld;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Characters and the engine's seams: a cat crosses between the physics worlds of two sections, is mirrored by a
/// mirror seam, and is refused where the engine refuses it; a cat holds the player's arms until the player leaves
/// its section; a cat leaps through a ring that is standing in its section. The world is built in code.
/// </summary>
public partial class CatWorldChecks : Node
{
    private CatWorld world = null!;

    public override async void _Ready()
    {
        try
        {
            world = CatWorld.Build(this);
            await Frames(2);
            await CheckSeams();
            await CheckRefusals();
            await CheckCarriedAcrossSections();
            world.StandNutUp();
            await Frames(2);
            await CheckRing();
            GD.Print("Cat world checks passed: normal seam, mirror seam, refusals, carried cat, ring.");
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

    private async Task CheckSeams()
    {
        var cat = world.Cat;
        cat.PlaceAt(new(2, 0, 0), 0);
        Check(cat.CrossSeam("normal", "II") is null, "a cat crosses a normal seam without a prism");
        Check(cat.Section == "II" && cat.GetParent() == world.Rooms["II"], "a cat that crossed is in the physics world of the target section");
        Check(cat.Position.DistanceTo(new(2, 0, 0)) < .001f && !cat.Mirrored, "a normal seam keeps the place and the handedness");
        Check(world.State("cat").Section == new SectionId("II") && world.State("cat").Pose.Position.W == .42, "the engine has the cat in the target section");
        await Frames(2);
        Check(cat.CrossSeam("normal", "I") is null && cat.Section == "I" && cat.GetParent() == world.Rooms["I"], "a seam goes both ways");

        // The seam of mirror image is the plane x = -2, so a cat 10 cm before it comes out 10 cm behind it.
        cat.PlaceAt(new(-1.9f, 0, 0), 0);
        Check(cat.CrossSeam("mirror", "II") is null, "a cat crosses a mirror seam");
        Near(cat.Position, new(-2.1f, 0, 0), "a mirror seam puts the cat on the other side of the seam plane");
        Check(cat.Mirrored && world.State("cat").Handedness == Handedness.Mirrored, "the engine turns the cat into its mirror image");
        await Frames(2);
        Check(cat.GetNode<Node3D>("Visual").Scale.X < 0, "a mirrored cat is drawn as its mirror image");
        Check(Mathf.Abs((float)world.State("cat").Pose.Position.X - cat.Position.X) < .001f, "the engine and the cat agree where the cat is after the crossing");
        Check(cat.CrossSeam("mirror", "I") is null && !cat.Mirrored, "crossing a mirror seam again restores the cat");
        await Frames(2);
    }

    private async Task CheckRefusals()
    {
        var cat = world.Cat;
        cat.PlaceAt(new(0, 0, -2), 0);
        Check(cat.CrossSeam("closed", "II") == FailureCode.SeamClosed, "a closed seam is refused");
        cat.PlaceAt(new(3, 0, 0), 0);
        Check(cat.CrossSeam("normal", "II") == FailureCode.WrongSection, "a seam too far away is refused");
        var mate = world.Mate;
        mate.PlaceAt(new(2, 0, 0), 0);
        Check(mate.CrossSeam("normal", "II") == FailureCode.RequiresPrism, "a character that cannot use seams without a prism is refused");
        await Frames(2);
        Check(cat.Section == "I" && cat.GetParent() == world.Rooms["I"] && mate.Section == "I" && mate.GetParent() == world.Rooms["I"],
            "a refused character stays where it was");
    }

    private async Task CheckCarriedAcrossSections()
    {
        var cat = world.Cat;
        var player = world.Player;
        cat.PlaceAt(new(0, 0, 1), 0);
        player.Position = new(2, .02f, 0);
        world.World.SynchronizeActor(new("player"), new(2, 0, 0), 0);
        cat.GetNode<Interactable3D>("Pickup").Interact();
        await Frames(3);
        Check(cat.Busy && player.PutDown is not null, "the cat is in the player's arms");
        var request = new CrossSeamRequest(new("player"), new("normal"), new("II"), HasPrism: true, FreeBackpackSlots: 4);
        Check(world.World.CrossSeam(request) is CrossSeamResult.Crossed, "the player crosses the seam");
        await Frames(3);
        Check(!cat.Busy && player.PutDown is null, "a cat is put down when the player leaves its section");
        Check(cat.GetNode<Interactable3D>("Pickup").CollisionLayer == 2, "a cat put down can be picked up again");
        world.World.CrossSeam(request with { Target = new("I") });
    }

    private async Task CheckRing()
    {
        var kit = world.Kit;
        var cat = world.Cat;
        // The nut at x = 0, z = -1 stood up toward II leaves a ring there at x = -.21, centered .9 m up, facing along X.
        Check(!Brain(cat).JumpThroughRing("nut", Ring), "a cat does not see a ring that is in another section");
        var landing = new Vector3(-1.41f, 0, -1);
        var crossing = float.NaN;
        var last = kit.Position;
        Check(Brain(kit).JumpThroughRing("nut", Ring), "a cat runs at a ring that faces it");
        while (kit.Busy)
        {
            await Frames(1);
            if (float.IsNaN(crossing) && last.X > -.21f && kit.Position.X <= -.21f) crossing = kit.Position.Y + kit.Height / 2;
            last = kit.Position;
        }
        Check(Apart(kit.Position, landing) < .15f, $"a cat lands on the other side of the ring; it is at {kit.Position}");
        Check(Mathf.Abs(crossing - .9f) < .15f, $"a cat goes through the middle of the hole; its middle was {crossing} m up at the ring");

        kit.PlaceAt(new(-.21f, 0, 1.5f), 0);
        Check(!Brain(kit).JumpThroughRing("nut", Ring) && !kit.Busy, "a cat does not go at a ring that is turned edge on to it");
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        Check(Brain(kit).JumpThroughRing("nut", Ring), "the same cat goes at the ring again from the front");
        kit.Stop();

        // A crate in the hole is in the way of the cat's body.
        var crate = new TurnableDefinition(new("crate"),
            new(new(-.21, .9, -1, .42), new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1))),
            new("/test", "crate"), new("II"), new("home"),
            new(new BoxPart[]
            {
                new(new("body"), default, new(.05, .05, .05), new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
                    [new(Axis3.X, false, new(1, 1), new(0, 0)), new(Axis3.X, true, new(1, 1), new(0, 0)), new(Axis3.Y, false, new(1, 1), new(0, 0)),
                     new(Axis3.Y, true, new(1, 1), new(0, 0)), new(Axis3.Z, false, new(1, 1), new(0, 0)), new(Axis3.Z, true, new(1, 1), new(0, 0))])
            }),
            new TurnableProperties(Holdable: true, Packable: false, HorizontalRadius: .05, LongAxis: Axis3.X, Hinge: new(-.03, 0, 0), Reach: .08));
        Check(world.World.AddEntity(crate) is AddEntityResult.Added, "a crate is put in the hole");
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        Check(!Brain(kit).JumpThroughRing("nut", Ring) && !kit.Busy, "a cat does not leap at a ring with something in its hole");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
