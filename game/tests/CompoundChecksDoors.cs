using System.Threading.Tasks;
using Game.Interaction;
using Godot;
using static Game.Interaction.Door3D.DoorState;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The doors of the compound and the edge of the sidewalk: each of Xiaoman's doors opens to its own side of the wall and
/// shuts with E; shut it stops her, open it lets her through, and ajar it stands solid and leaves her room to pass beside
/// it. Another flat's front door stays shut on E, and she says why; the sidewalk's unseen walls keep her off the bicycle
/// lane and from walking on past either end.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class CompoundChecks
{
    private const string NotOurs = "小满：爸爸说过不能随便敲别人家的门。";

    // Each of Xiaoman's doors: whether its wall runs along x, the side of the wall it opens to, and its state at the start.
    private static readonly (string Name, bool AlongX, int Side, Door3D.DoorState Start)[] HomeDoors =
    [
        ("FrontDoor", true, -1, Shut), ("KitchenDoor", true, -1, Open), ("LivingRoomDoor", true, -1, Ajar),
        ("RoomDoor", false, 1, Open), ("BathroomDoor", true, 1, Open), ("BalconyDoor", false, 1, Open),
    ];

    // Every door is also made ajar, as a scene may start it. Ends with every door open.
    private async Task CheckHomeDoors()
    {
        foreach (var (name, alongX, side, start) in HomeDoors)
        {
            var door = play.Region.Props("Ⅰ")!.GetNode<Door3D>($"HomeDoors/{name}");
            var leaf = door.GetNode<Interactable3D>("Leaf");
            var width = ((BoxShape3D)leaf.GetNode<CollisionShape3D>("Shape").Shape).Size.X;
            // The shut leaf runs from the hinge along the door's x, 1 cm short of the hole at each end.
            var hinge = door.GlobalPosition with { Y = Storey };
            var into = (alongX ? Vector3.Back : Vector3.Right) * side;
            var middle = hinge + door.GlobalBasis.X * (width / 2);
            var gap = hinge + door.GlobalBasis.X * (width + .01f - .25f);  // a quarter metre inside the far side of the hole
            var outside = middle - into * .7f;
            Check(door.State == start, $"{name} starts {start}; it is {door.State}");
            if (start != Shut)
            {
                Check(await Through(start == Ajar ? gap : middle, into), $"{name}, {start}, lets her through; she is at {feet}");
                await Use(door, outside, Shut);
            }
            door.State = Ajar;
            await play.Frames(1);
            Check(Stops(leaf) && Opens(leaf, hinge, into), $"{name}, ajar, stands solid on its side of the wall");
            Check(await Through(gap, into), $"{name}, ajar, leaves her room to pass beside it; she is at {feet}");
            await Use(door, outside, Shut);
            Check(!await Through(middle, into), $"{name}, shut, stops her; she is at {feet}");
            await Use(door, outside, Open);
            Check(Opens(leaf, hinge, into), $"{name} opens to its side of the wall");
            Check(await Through(middle, into), $"{name}, opened, lets her through; she is at {feet}");
        }
    }

    // Pushes from 0.7 m before a point of a doorway toward 0.7 m past it, and tells whether she got through: past the wall
    // by more than her radius.
    private async Task<bool> Through(Vector3 at, Vector3 into)
    {
        var (from, to) = (at - into * .7f, at + into * .7f);
        play.LookAt(from, to + Vector3.Up * 1.3f);
        await Push((to.X, to.Z));
        return (feet - at).Dot(into) > .2f;
    }

    private static bool Opens(Interactable3D leaf, Vector3 hinge, Vector3 into) =>
        (leaf.GetNode<Node3D>("Shape").GlobalPosition - hinge).Dot(into) > .1f;

    // Whether the leaf is a body that stops the player.
    private static bool Stops(Interactable3D leaf)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = .05f },
            Transform = new Transform3D(Basis.Identity, leaf.GetNode<Node3D>("Shape").GlobalPosition),
            CollisionMask = PhysicsLayers.World
        };
        foreach (var hit in leaf.GetWorld3D().DirectSpaceState.IntersectShape(query))
            if (hit["collider"].AsGodotObject() == leaf) return true;
        return false;
    }

    private async Task Use(Door3D door, Vector3 at, Door3D.DoorState then)
    {
        var leaf = door.GetNode<Interactable3D>("Leaf");
        play.LookAt(at, leaf.GetNode<Node3D>("Shape").GlobalPosition);
        await play.Frames(2);
        Check(play.Player.Hud.HintText == $"E  {leaf.Prompt}", $"the crosshair finds {door.Name}; the hint is \"{play.Player.Hud.HintText}\"");
        Press(Key.E);
        await play.Frames(2);
        Check(door.State == then, $"E leaves {door.Name} {then}; it is {door.State}");
    }

    // From where she stands, E on another flat's front door, whose middle is given.
    private async Task Knock(Vector3 door)
    {
        play.Player.Hud.ClearText();
        play.LookAt(feet, door);
        await play.Frames(2);
        Check(play.Player.Hud.HintText == "E  开门", $"the crosshair finds the front door at {door}; the hint is \"{play.Player.Hud.HintText}\"");
        Press(Key.E);
        await play.Frames(2);
        Check(play.Player.Hud.SubtitleText == NotOurs, $"at the front door at {door} she says \"{NotOurs}\"; the subtitle is \"{play.Player.Hud.SubtitleText}\"");
    }

    // From the sidewalk toward the bicycle lane, north of the gate, across the road to the gate and south of it; and on past
    // the north end, beside the kiosk, and past the south end.
    private async Task CheckSidewalkEdge()
    {
        const float Walk = Ground + .1f, Edge = 84, NorthEnd = 57, SouthEnd = 105;
        foreach (var z in new[] { 80, CrossRoad, 102 })
        {
            play.LookAt(new(81, Walk, z), new(90, 1.3f, z));
            await Push((90, z));
            Check(feet.X < Edge, $"the edge of the sidewalk stops her at z {z}; she is at {feet}");
        }
        play.LookAt(new(83.2f, Walk, 58.5f), new(83.2f, 1.3f, 50));
        await Push((83.2f, 50));
        Check(feet.Z > NorthEnd, $"the north end of the sidewalk stops her; she is at {feet}");
        play.LookAt(new(81, Walk, 103), new(81, 1.3f, 110));
        await Push((81, 110));
        Check(feet.Z < SouthEnd, $"the south end of the sidewalk stops her; she is at {feet}");
    }
}
