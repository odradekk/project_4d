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
/// The home region's layout: how the room differs in its three sections, the building ranges and the floor hole, and the
/// space kept free for the seam, the hoop and the cat in scene 8.
/// </summary>
public partial class HomeChecks : Node
{
    private const double Epsilon = .006;

    private HomeScene home = null!;
    private World world = null!;

    public override async void _Ready()
    {
        try
        {
            home = HomeScene.Open(this, AddCatToys);
            world = home.Region.World!;
            await Frames(2);
            CheckSections();
            CheckRoomLayout();
            CheckBuildings();
            CheckFloorHole();
            CheckFreeSpace();
            GD.Print("Home checks passed: sections, room layout in three sections, buildings, floor hole, free space.");
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
        Check(Has("Ⅰ", "Cabinet") && Has("Ⅱ", "Cabinet"), "the cabinet over the door is in both sections of the first building");
        Check(Has("Ⅰ", "Fittings") && Has("Ⅱ", "Fittings"), "the window is in both sections of the first building");
        Check(Has("Ⅰ", "FloorPit") && !Has("Ⅱ", "FloorPit") && Has("Ⅱ", "Floor"), "the floor of section II has a hole and the rest of it stays");
        Check(Has("Ⅲ", "DeepDoor") && Has("Ⅲ", "DeepDoorExtra"), "section III has the door and one door more");
        Check(!Has("Ⅰ", "DeepDoorExtra") && !Has("Ⅱ", "DeepDoorExtra"), "the extra door is in section III only");
        Check(Has("Ⅱ", "DoorShut") && !Has("Ⅰ", "DoorShut"), "the door of the room is shut in section II and open in section I");
        Check(Has("Ⅲ", "skypanel") && Has("Ⅲ", "DeepFittings"), "section III has a window with a sky outside it");
    }

    // The engine checks on load that the first building holds a toy standing up toward either side, so the region only
    // loads if it covers the cat toys that AddCatToys puts in sections I and II.
    private void CheckBuildings()
    {
        var first = home.Region.GetNode<Building3D>("Buildings/Home");
        var second = home.Region.GetNode<Building3D>("Buildings/Deep");
        var w3 = Geometry("Ⅲ").W;
        Check(second.WMin < w3 && w3 < second.WMax, "the second building holds section III");
        Check(second.WMin > first.WMax, "there is empty space in W between the two buildings");
    }

    private void CheckFloorHole()
    {
        var hole = home.Region.GetNode<BuildingElement3D>("Buildings/Home/Shell/FloorPit").Gaps.Single();
        Check(Geometry("Ⅰ").W < hole.Min && hole.Min < Geometry("Ⅱ").W && Geometry("Ⅱ").W < hole.Max, "the hole is around section II only");
    }

    // Around the gap between the desk and the wardrobe nothing stands up to the height of a person: a seam, the hoop of
    // the nut's ring and the cat's run need it, in both sections that the first seam joins. In section II the cat leaps
    // through the ring from 0.72 m before its plane to 0.72 m after it, whether the ring stands across the gap or along it,
    // with the plane anywhere from x 6.49 to 6.85, and the leap starts on the floor, not over the hole.
    private void CheckFreeSpace()
    {
        const float CatReach = .72f;
        var spaces = new (string Section, string Name, Vector3 Center, Vector3 Size)[]
        {
            ("Ⅰ", "the gap", new(6.55f, .05f + .675f, 4.3f), new(1.9f, 1.35f, .9f)),
            ("Ⅱ", "the gap", new(6.55f, .05f + .675f, 4.3f), new(1.9f, 1.35f, .9f)),
            ("Ⅱ", "a leap along the gap", new(6.55f, .02f + .675f, 4.3f), new(.3f, 1.35f, 2 * CatReach)),
            ("Ⅱ", "a leap across the gap, ring at 6.49", new(6.49f, .02f + .675f, 4.3f), new(2 * CatReach, 1.35f, .9f)),
            ("Ⅱ", "a leap across the gap, ring at 6.85", new(6.85f, .02f + .675f, 4.3f), new(2 * CatReach, 1.35f, .9f))
        };
        foreach (var (section, name, center, size) in spaces)
        {
            var pose = SceneReader.PoseOf(new Transform3D(Basis.Identity, center), Geometry(section).W);
            var box = new OccupancyBox(pose, new(size.X / 2, size.Y / 2, size.Z / 2, Epsilon));
            Check(world.QueryOccupancy(box, []) is OccupancyResult.Occupancy { Hits.Count: 0 }, $"section {section} keeps {name} free");
        }
        var part = home.Region.GetNode<BoxPart3D>("Buildings/Home/Shell/FloorPit/p1");
        var holeEdge = part.Position.X + ((BoxMesh)part.Mesh).Size.X / 2;
        Check(6.49f - CatReach >= holeEdge, "the leap of a ring at 6.49 starts east of the hole");
    }

    // A cat toy: 0.48 m long with its hinge 0.02 m in, the longest turnable of the scenes.
    private static void AddCatToys(Region3D region)
    {
        foreach (var section in new[] { "Ⅰ", "Ⅱ" })
        {
            var toy = new TurnableObject3D
            {
                Name = $"Toy{section}",
                EntityId = $"toy{section}",
                BuildingId = "home",
                Position = new(4.3f, .015f, 5f),
                Interaction = new TurnableResource
                {
                    Holdable = true,
                    HorizontalRadius = .24,
                    LongAxis = Axis3.X,
                    Hinge = new(-.22f, 0, 0),
                    Reach = .46
                }
            };
            var faces = new Godot.Collections.Array<FaceUvResource>();
            foreach (var axis in new[] { Axis3.X, Axis3.Y, Axis3.Z })
                foreach (var positive in new[] { false, true })
                    faces.Add(new FaceUvResource { Axis = axis, Positive = positive });
            toy.AddChild(new BoxPart3D { Name = "Body", PartId = "body", Mesh = new BoxMesh { Size = new(.48f, .03f, .03f) }, FaceUvs = faces });
            region.GetNode($"Sections/{section}/Turnables").AddChild(toy);
        }
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
