using System;
using System.Threading.Tasks;
using FourD;
using Game.Characters;
using Godot;
using static Game.Tests.CatWorld;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// A cat leaps through a ring that is standing in its section, in a world built in code: the ring comes from a nut that
/// the authored regions have none of.
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
            world.StandNutUp();
            await Frames(2);
            await CheckRing();
            GD.Print("Cat world checks passed: the leap through a ring.");
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
