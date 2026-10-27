using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Flow;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The home region's layout: how the room differs in its three sections, the building ranges and the floor hole as the
/// size norms give them, and the space kept free for the seam, the hoop and the cat in scene 8.
/// </summary>
public partial class HomeChecks : Node
{
    // The cat toy is the longest turnable: 0.48 m long with its hinge 0.02 m in.
    private const double LongestReach = .46;
    // The size norms of the prologue: where the floor hole is in W and where the ball's lowest point is.
    private const double HoleMin = .3;
    private const double HoleMax = .6;
    private const double BallLowest = .21;

    private HomeScene home = null!;
    private World world = null!;

    public override async void _Ready()
    {
        try
        {
            home = HomeScene.Open(this);
            world = home.Region.World!;
            await Frames(2);
            CheckSections();
            CheckRoomLayout();
            CheckBuildings();
            CheckFloorHole();
            CheckFreeSpace();
            CheckPlaceholderLights();
            GD.Print("Home checks passed: sections, room layout in three sections, buildings, floor hole, free space, lights.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private SectionGeometryResult.Geometry Geometry(string section) =>
        (SectionGeometryResult.Geometry)world.GetSectionGeometry(new(section));

    private bool Has(string section, string entity) => Geometry(section).Entities.Any(slice => slice.Entity.Value == entity);

    private void CheckSections()
    {
        Check(Math.Abs(Geometry("Ⅱ").W - Geometry("Ⅰ").W - .42) < 1e-9, "section II lies 0.42 m of W beyond section I");
        Check(Math.Abs(Geometry("Ⅲ").W - Geometry("Ⅱ").W - 1.18) < 1e-9, "section III lies 1.18 m of W beyond section II");
    }

    private void CheckRoomLayout()
    {
        Check(Has("Ⅰ", "bed") && Has("Ⅰ", "desk") && Has("Ⅰ", "wardrobe"), "section I has her furniture");
        Check(!Has("Ⅱ", "bed") && !Has("Ⅱ", "desk") && !Has("Ⅱ", "wardrobe"), "section II has no bed and no furniture");
        Check(!Has("Ⅲ", "bed") && !Has("Ⅲ", "desk"), "section III has none of the furniture of section I");
        Check(Has("Ⅰ", "RoomWallsA") && !Has("Ⅱ", "RoomWallsA") && Has("Ⅱ", "RoomWallsB") && !Has("Ⅰ", "RoomWallsB"),
            "the wallpaper of section II is another one than that of section I");
        Check(Has("Ⅰ", "Cabinet") && Has("Ⅱ", "Cabinet"), "the cabinet over the door is in both sections of the first building");
        Check(Has("Ⅰ", "Fittings") && Has("Ⅱ", "Fittings"), "the window is in both sections of the first building");
        Check(Has("Ⅰ", "FloorPit") && !Has("Ⅱ", "FloorPit") && Has("Ⅱ", "Floor"), "the floor of section II has a hole and the rest of it stays");
        Check(Has("Ⅲ", "DeepDoor") && Has("Ⅲ", "DeepDoorExtra"), "section III has the door and one door more");
        Check(!Has("Ⅰ", "DeepDoorExtra") && !Has("Ⅱ", "DeepDoorExtra"), "the extra door is in section III only");
        Check(Has("Ⅱ", "DoorShut") && !Has("Ⅰ", "DoorShut"), "the door of the room is shut in section II and open in section I");
        Check(Has("Ⅲ", "skypanel") && Has("Ⅲ", "DeepFittings"), "section III has a window with a sky outside it");
    }

    private void CheckBuildings()
    {
        var first = home.Region.GetNode<Building3D>("Buildings/Home");
        var second = home.Region.GetNode<Building3D>("Buildings/Deep");
        var w1 = Geometry("Ⅰ").W;
        var w2 = Geometry("Ⅱ").W;
        var w3 = Geometry("Ⅲ").W;
        Check(first.WMin <= w1 - LongestReach && first.WMax >= w2 + LongestReach,
            "the first building reaches the longest turnable's length beyond its sections in both directions");
        Check(second.WMin < w3 && w3 < second.WMax, "the second building holds section III");
        Check(second.WMin > first.WMax, "there is empty space in W between the two buildings");
    }

    private void CheckFloorHole()
    {
        var hole = home.Region.GetNode<BuildingElement3D>("Buildings/Home/Shell/FloorPit").Gaps.Single();
        Check(hole.Min == HoleMin && hole.Max == HoleMax, "the floor hole runs from 0.3 to 0.6 in W");
        Check(Geometry("Ⅰ").W < hole.Min && hole.Min < Geometry("Ⅱ").W && Geometry("Ⅱ").W < hole.Max, "the hole is around section II only");
        Check(BallLowest < hole.Min, "the lowest point of the ball is held up by the floor beside the hole");
        var edge = home.Region.GetNode<Node3D>("Generated/Ⅱ/Props/PitGuard");
        Check(Ray("Ⅱ", new(4f, .5f, 4.2f), new(6f, .5f, 4.2f)) == edge, "the opening of the hole is guarded in section II");
        Check(Ray("Ⅰ", new(4f, .5f, 4.2f), new(6f, .5f, 4.2f)) is null, "the floor of section I is whole");
    }

    // Around the gap between the desk and the wardrobe nothing stands up to the height of a person: a seam, the hoop of
    // the nut's ring and the cat's run need it, in both sections that the first seam joins. The cat leaps through a ring
    // standing across either axis of the gap, and needs 0.72 m clear on both sides of the ring's plane.
    private void CheckFreeSpace()
    {
        const float CatReach = .72f;
        var spaces = new (string Name, Vector3 Size, Vector3 Center)[]
        {
            ("the gap", new(1.9f, 1.35f, .9f), new(6.55f, .05f + .675f, 4.3f)),
            ("a leap across the gap", new(2 * CatReach, .9f, .3f), new(6.55f, .05f + .45f, 4.3f)),
            ("a leap along the gap", new(.3f, .9f, 2 * CatReach), new(6.55f, .05f + .45f, 4.3f))
        };
        foreach (var section in new[] { "Ⅰ", "Ⅱ" })
            foreach (var (name, size, center) in spaces)
            {
                var query = new PhysicsShapeQueryParameters3D { Shape = new BoxShape3D { Size = size }, Transform = new Transform3D(Basis.Identity, center) };
                Check(Sub(section).World3D.DirectSpaceState.IntersectShape(query).Count == 0,
                    $"section {section} keeps {name} between the desk and the wardrobe free");
            }
    }

    private void CheckPlaceholderLights()
    {
        var light = home.Prop<OmniLight3D>("SkyLight", "Ⅲ");
        Check(light.LightColor.R >= light.LightColor.G && light.LightColor.G >= light.LightColor.B, "the sky light of section III is pink and gold");
    }

    private SubViewport Sub(string section) => home.Region.GetNode<SubViewport>($"Generated/{section}");

    private Node? Ray(string section, Vector3 from, Vector3 to)
    {
        var hit = Sub(section).World3D.DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        return hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
