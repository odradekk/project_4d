using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Game.Rendering;
using Game.UI;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The seam test region and what the checks of seams and the backpack do on it: the plain seam at x = -1.5, the mirror seam at
/// x = 1.5 (both at z = 1.5), the cut seam at x = 3.5 and the plain seam to the shallower section at z = -1.5; a pole and a
/// crate, and five small things on the floor.
/// </summary>
internal sealed class SeamPlay(Node host, TurningFixture play)
{
    internal const string Scene = "res://regions/seam_test.tscn";

    internal FirstPersonPlayer Player => play.Player;
    internal PrismView Prism => play.Region.GetNode<PrismView>("Generated/Prism");
    internal BackpackBar Bar => Player.Hud.GetNode<BackpackBar>("Backpack");
    internal FourD.World World => play.Region.World!;
    internal string Said => Player.Hud.SubtitleText;
    internal string[] InBackpack => play.Region.Travel!.Backpack.Items.Select(item => item.Value).ToArray();
    internal static EntityId Xiaoman => new("xiaoman");

    internal Task Load(Action<Region3D>? change = null) => play.Load(change, Scene);

    /// <summary>Puts her feet at <paramref name="feet"/>, looking at <paramref name="look"/>, with no text on the screen.</summary>
    internal async Task Stand(Vector3 feet, Vector3 look)
    {
        play.LookAt(feet, look);
        Player.Hud.ClearText();
        await play.Frames(3);
    }

    internal async Task Click()
    {
        Button(MouseButton.Left, true);
        Button(MouseButton.Left, false);
        await play.Frames(3);
    }

    internal async Task RaiseLens()
    {
        Button(MouseButton.Right, true);
        await play.Frames(2);
    }

    internal void Pack(string id) =>
        Check(World.PackEntity(Xiaoman, new(id), Backpack.Slots) is CarryResult.Changed, $"{id} must go into the backpack");

    internal async Task Hold(string id)
    {
        Check(World.HoldEntity(Xiaoman, new(id)) is CarryResult.Changed, $"{id} must come into the hand");
        await play.Frames(3);
    }

    /// <summary>The entity of the first thing that a ray from one point to another hits in a world, or null.</summary>
    internal static string? Hit(World3D space, Vector3 from, Vector3 to)
    {
        var hit = space.DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        return hit.Count == 0 ? null : hit["collider"].AsGodotObject() is Node node ? node.GetParent().Name.ToString() : null;
    }

    /// <summary>A block that exists in the second section only, among the building's elements.</summary>
    internal static void AddBlock(Region3D region, string id, Vector3 position, Vector3 size)
    {
        var block = (BuildingElement3D)region.GetNode<BuildingElement3D>("Buildings/Home/Floor").Duplicate();
        block.Name = id;
        block.EntityId = id;
        block.WMin = .2;
        block.Position = position;
        block.GetNode<BoxPart3D>("Part").Mesh = new BoxMesh { Size = size };
        region.GetNode("Buildings/Home").AddChild(block);
    }

    /// <summary>Everything that reads the player's section agrees on it: the engine, the main view, the physics, the sound and the lens.</summary>
    internal void CheckArrived(string section, string lensShows)
    {
        Check(play.State("xiaoman").Section?.Value == section, $"the engine must have her in {section}");
        Check(play.Region.CurrentSection == section && play.Actions.CurrentSection == section, $"the region must report {section}");
        var sectionWorld = play.Region.GetNode<SubViewport>($"Generated/{section}").World3D;
        Check(host.GetViewport().FindWorld3D() == sectionWorld, $"the main view must show {section}");
        Check(Player.GetWorld3D() == sectionWorld, $"the player's physics and eyes must be in {section}");
        Check(Player.Camera.IsCurrent(), "the player's camera must stay the current one");
        Check(Math.Abs(play.Region.SectionDistance(section)) < 1e-9, "sound distance must be measured from the section she is in");
        Check(Prism.TargetSection == lensShows, $"the lens must show {lensShows}, not {Prism.TargetSection}");
    }
}
