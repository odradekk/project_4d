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
        Check(cat.GetNode<Interactable3D>("Pickup").CollisionLayer != 0 && !Brain(cat).Held, "a cat put down can be picked up again");
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

        // Things in the way of the body along its arc stop it: a crate under the arc before the ring, one beside its rising part.
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        foreach (var (id, center, half) in new[] { ("under", new Vector3(.39f, .3f, -1), new Vector3(.1f, .3f, .1f)), ("rising", new Vector3(.6f, .45f, -1), new Vector3(.1f, .1f, .1f)) })
        {
            world.AddCrate(id, center, half);
            Check(!Brain(kit).JumpThroughRing("nut", Ring) && !kit.Busy, $"a cat does not leap when something is in its way at the {id} part of the arc");
            world.World.RemoveEntity(new(id));
        }

        // A crate lying low under the high part of the arc is not in the way of a cat that flies over it.
        world.AddCrate("low", new(.09f, .05f, -1), new(.1f, .05f, .1f));
        await Leap(kit, new(-1.41f, 0, -1), "a cat leaps over what lies low under its arc");
        world.World.RemoveEntity(new("low"));

        // A crate over the cat's landing, higher than the cat is by then, is not in the way either.
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        world.AddCrate("shelf", new(-1.31f, .9f, -1), new(.1f, .1f, .1f));
        await Leap(kit, new(-1.41f, 0, -1), "a cat leaps under what hangs over its landing");
        world.World.RemoveEntity(new("shelf"));

        // A thin crate low beside the foot of the arc is met by the body as it comes down: the cat leaps the shortest way.
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        world.AddCrate("thin", new(-1.05f, .26f, -1), new(.04f, .04f, .1f));
        await Leap(kit, new(-.81f, 0, -1), "a cat that would meet a crate at the foot of its arc leaps a shorter way");
        world.World.RemoveEntity(new("thin"));

        // A wall behind the ring leaves less room: the cat leaps a shorter way, and does not leap where there is no room.
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        world.AddCrate("wall", new(-1.31f, .6f, -1), new(.1f, .6f, .3f));
        await Leap(kit, new(-1.01f, 0, -1), "a cat leaps a shorter way where there is a wall behind the ring");
        world.World.RemoveEntity(new("wall"));
        kit.PlaceAt(new(1.5f, 0, -1), 0);
        world.AddCrate("near", new(-.81f, .6f, -1), new(.1f, .6f, .3f));
        Check(!Brain(kit).JumpThroughRing("nut", Ring) && !kit.Busy, "a cat does not leap where the wall is too close behind the ring");
        world.World.RemoveEntity(new("near"));

        // A crate in the hole is in the way too.
        world.AddCrate("crate", new(-.21f, .9f, -1), new(.05f, .05f, .05f));
        Check(!Brain(kit).JumpThroughRing("nut", Ring) && !kit.Busy, "a cat does not leap at a ring with something in its hole");
        Check(!Brain(kit).JumpThroughRing("nut", Ring, "floor") && !kit.Busy, "something else that is ignored does not let the cat leap");
        // The toy that leads the cat through is in the hole, and does not count.
        Check(Brain(kit).JumpThroughRing("nut", Ring, "crate"), "a cat leaps at a ring with only an ignored thing in the way");
    }

    // The cat leaps through the ring from where it stands and lands near the place.
    private async Task Leap(Character3D kit, Vector3 landing, string message)
    {
        Check(Brain(kit).JumpThroughRing("nut", Ring), message);
        while (kit.Busy) await Frames(1);
        Check(Apart(kit.Position, landing) < .15f, $"{message}; it landed at {kit.Position}");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
