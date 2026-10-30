using System.Threading.Tasks;
using Game.Interaction;
using Godot;
using static Game.Interaction.Door3D.DoorState;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The doors of the compound and the edge of the sidewalk: each of Xiaoman's doors opens and shuts with E and stops her only
/// while shut; another flat's front door stays shut on E, and she says why; the sidewalk's invisible walls keep her off the
/// bicycle lane and from walking on past either end.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class CompoundChecks
{
    private const string NotOurs = "小满：爸爸说过不能随便敲别人家的门。";

    // Each of Xiaoman's doors: the middle of its hole, whether its wall runs along x, the side of the wall it opens to, and
    // its state at the start. The bathroom's is checked a little west of its middle, clear of the basin.
    private static readonly (string Name, float X, float Z, bool AlongX, int Side, Door3D.DoorState Start)[] HomeDoors =
    [
        ("FrontDoor", .75f, 7.35f, true, -1, Shut), ("KitchenDoor", 1, 4, true, -1, Open),
        ("LivingRoomDoor", 2.6f, 4, true, -1, Ajar), ("RoomDoor", 3.25f, 4.6f, false, 1, Open),
        ("BathroomDoor", 2.5f, 5.5f, true, 1, Open), ("BalconyDoor", 7.85f, 3.425f, false, 1, Open),
    ];

    // Ends with every door open.
    private async Task CheckHomeDoors()
    {
        foreach (var (name, x, z, alongX, side, start) in HomeDoors)
        {
            var door = play.Region.Props("Ⅰ")!.GetNode<Door3D>($"HomeDoors/{name}");
            var middle = new Vector3(x, Storey, z);
            var across = alongX ? new Vector3(0, 0, side * .7f) : new Vector3(side * .7f, 0, 0);
            var (inside, outside) = (middle + across, middle - across);
            Check(door.State == start, $"{name} starts {start}; it is {door.State}");
            if (start != Shut)
            {
                Check(await Crosses(inside, outside), $"{name}, {start}, lets her through; she is at {feet}");
                await Use(door, inside, Shut);
            }
            Check(!await Crosses(inside, outside), $"{name}, shut, stops her; she is at {feet}");
            await Use(door, inside, Open);
            Check(await Crosses(inside, outside), $"{name}, opened, lets her through; she is at {feet}");
        }
    }

    // Pushes from one side of a doorway toward the other, and tells whether she got past its middle.
    private async Task<bool> Crosses(Vector3 from, Vector3 to)
    {
        play.LookAt(from, to + Vector3.Up * 1.3f);
        await Push((to.X, to.Z));
        return feet.DistanceTo(to) < feet.DistanceTo(from);
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
