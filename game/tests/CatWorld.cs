using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Player;
using Godot;

namespace Game.Tests;

/// <summary>
/// An engine world with two sections, three seams and a nut with a ring, built in code, and a physics room for each
/// section with a cat, a schoolmate and the player in them. The authored test region has no seams yet.
/// Lengths are in metres; section I is at W = 0 and section II at W = .42.
/// </summary>
internal sealed class CatWorld
{
    internal const string Ring = "ring";
    private static readonly Source Origin = new("/test", "definition");
    private static readonly Rotation4 Identity = new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));
    // The nut's ring lies in the YZ plane of the nut, so that standing the nut up keeps it in one section.
    private static readonly Rotation4 RingInYz = new(new(0, 1, 0, 0), new(0, 0, 1, 0), new(1, 0, 0, 0), new(0, 0, 0, 1));

    internal readonly Dictionary<string, Node3D> Rooms = [];
    internal World World { get; private set; } = null!;
    internal FirstPersonPlayer Player { get; private set; } = null!;
    internal Character3D Cat { get; private set; } = null!;
    internal Character3D Mate { get; private set; } = null!;

    /// <summary>A second cat, which starts in section II.</summary>
    internal Character3D Kit { get; private set; } = null!;

    internal static CatWorld Build(Node host)
    {
        var result = new CatWorld();
        result.World = ((LoadResult.Loaded)World.Load(Description())).World;
        foreach (var section in new[] { "I", "II" })
        {
            var room = new Node3D { Name = section };
            var floor = new StaticBody3D { Position = new(0, -.1f, 0) };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(12, .2f, 12) } });
            room.AddChild(floor);
            host.AddChild(room);
            result.Rooms.Add(section, room);
        }
        result.Player = GD.Load<PackedScene>("res://player/first_person_player.tscn").Instantiate<FirstPersonPlayer>();
        result.Player.Position = new(0, .02f, 3);
        host.AddChild(result.Player);
        result.Cat = result.Character(CharacterFixture.CatScene, "cat", "I");
        result.Mate = result.Character(CharacterFixture.SchoolmateScene, "mate", "I");
        result.Kit = result.Character(CharacterFixture.CatScene, "kit", "II");
        return result;
    }

    /// <summary>The player stands the nut up toward section II, which turns its ring into a hoop there.</summary>
    internal void StandNutUp()
    {
        var plan = ((RotationPlanResult.Allowed)World.PlanRotation(new StandUp(new("player"), new("nut"), new("II")))).Plan;
        World.StartRotation(plan);
        World.Step(1.4);
    }

    internal static Cat Brain(Character3D cat) => cat.GetNode<Cat>("Cat");

    // The engine state of an entity, for the checks.
    internal EntityState State(string id) => ((EntityReadResult.Found)World.ReadEntity(new EntityId(id))).Entity;

    private Character3D Character(string scene, string id, string section)
    {
        var character = GD.Load<PackedScene>(scene).Instantiate<Character3D>();
        character.Bind(new CreatureLink(World, id, "player", Player, name => Rooms[name]));
        Rooms[section].AddChild(character);
        character.PlaceAt(new((float)State(id).Pose.Position.X, 0, (float)State(id).Pose.Position.Z), 0);
        return character;
    }

    private static WorldDescription Description()
    {
        var floor = new BuildingElementDefinition(new("floor"), At(0, 0, 0, 0), Origin, new("home"), new(-.5, 1), [],
            Box(new(6, .05, 6), new(0, -.05, 0)));
        var nut = new ItemDefinition(new("nut"), At(0, .03, -1, 0), Origin, new("home"),
            new HiddenRingItem(Box(new(.03, .03, .03), default), new Ring4(new(Ring), new(.41, .87, 0, .2), RingInYz, .35, .04)),
            new TurnableProperties(Holdable: true, Packable: true, HorizontalRadius: .03, LongAxis: Axis3.X, Hinge: new(-.01, 0, 0), Reach: .04));
        return new WorldDescription(
            [new(new("I"), 0, Origin), new(new("II"), .42, Origin)],
            [new(new("home"), new(-.5, 1), Origin)],
            [floor, nut, Creature("player", 0, 3, .2, 1.6), Creature("cat", 1.5, -1, .15, .4, seams: true),
                Creature("mate", 2, 0, .2, 1.35), Creature("kit", 1.5, -1, .15, .4, seams: true, section: "II")],
            [Seam("normal", new(2, 0, 0), mirrored: false), Seam("mirror", new(-2, 0, 0), mirrored: true), Seam("closed", new(0, 0, -2), open: false)],
            [],
            []);
    }

    private static Creature3Definition Creature(string id, double x, double z, double radius, double height, bool seams = false, string section = "I") =>
        new(new(id), At(x, 0, z, section == "I" ? 0 : .42), Origin, new(section), radius, height, seams);

    // A seam in the YZ plane through its position, so its normal is +X.
    private static SeamDefinition Seam(string id, Vec3 position, bool mirrored = false, bool open = true) =>
        new(new(id), position, new(1, 0, 0), [new("I"), new("II")], mirrored, open, false, Origin);

    private static Pose4 At(double x, double y, double z, double w) => new(new(x, y, z, w), Identity);

    private static BoxAssembly Box(Vec3 half, Vec3 center)
    {
        var uvs = new[] { Axis3.X, Axis3.Y, Axis3.Z }
            .SelectMany(axis => new[] { false, true }, (axis, positive) => new FaceUv(axis, positive, new(1, 1), new(0, 0)))
            .ToArray();
        return new([new(new("body"), center, half, new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), uvs)]);
    }
}
