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
    // The reaches of the cat's leap, longest first, how far its body goes beyond each end of one, and its radius: see Cat.cs.
    private static readonly float[] CatReaches = [1.2f, 1f, .8f, .6f];
    private const float CatBeyond = .12f;
    private const float CatRadius = .1f;

    private HomeScene home = null!;
    private World world = null!;

    public override async void _Ready()
    {
        try
        {
            home = HomeScene.Open(this, region =>
            {
                AddCatToys(region);
                HideProps(region);
            });
            world = home.Region.World!;
            await Frames(2);
            CheckSections();
            CheckRoomLayout();
            CheckBuildings();
            CheckFloorHole();
            CheckFreeSpace();
            CheckPropsShow();
            GD.Print("Home checks passed: sections, room layout in three sections, buildings, floor hole, free space, props.");
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
    // through a ring that stands across the gap as Cat.JumpThroughRing has it: the longest of its reaches whose path, with
    // the cat's radius and margin beyond each end, is free. The ring's plane may be anywhere from x 6.5 to 6.85, and the
    // leap starts on the floor, not over the hole.
    private void CheckFreeSpace()
    {
        Check(IsFree("Ⅰ", new(6.55f, .05f + .675f, 4.3f), new(1.9f, 1.35f, .9f)), "section I keeps the gap between the desk and the wardrobe free");
        Check(IsFree("Ⅱ", new(6.55f, .05f + .675f, 4.3f), new(1.9f, 1.35f, .9f)), "section II keeps the gap between the desk and the wardrobe free");
        Check(IsFree("Ⅱ", new(6.55f, .02f + .675f, 4.3f), new(.3f, 1.35f, 2 * (CatReaches[^1] + CatBeyond))),
            "section II has room for a leap along the gap");
        var part = home.Region.GetNode<BoxPart3D>("Buildings/Home/Shell/FloorPit/p1");
        var holeEdge = part.Position.X + ((BoxMesh)part.Mesh).Size.X / 2;
        foreach (var plane in new[] { 6.5f, 6.85f })
        {
            var reach = CatReaches.FirstOrDefault(reach => IsFree("Ⅱ", new(plane, .02f + .675f, 4.3f), new(2 * (reach + CatBeyond), 1.35f, .9f)));
            Check(reach > 0, $"the cat can leap through a ring across the gap at x {plane}");
            Check(plane - reach - CatRadius >= holeEdge, $"the leap through a ring at x {plane} starts east of the hole, not over it");
        }
    }

    private bool IsFree(string section, Vector3 center, Vector3 size)
    {
        var pose = SceneReader.PoseOf(new Transform3D(Basis.Identity, center), Geometry(section).W);
        var box = new OccupancyBox(pose, new(size.X / 2, size.Y / 2, size.Z / 2, Epsilon));
        return world.QueryOccupancy(box, []) is OccupancyResult.Occupancy { Hits.Count: 0 };
    }

    // The props of the scene may be saved hidden by the editor, which hides those of the sections that PreviewW is not
    // near. They show again when the region loads.
    private void CheckPropsShow()
    {
        Check(home.Region.Props("Ⅰ")!.IsVisibleInTree(), "the props of section I show");
        Check(home.Region.Props("Ⅲ")!.IsVisibleInTree() && home.Prop<OmniLight3D>("SkyLight", "Ⅲ").IsVisibleInTree(), "the lights of section III show");
    }

    private static void HideProps(Region3D region)
    {
        foreach (var section in region.GetNode("Sections").GetChildren().OfType<Section3D>())
            if (section.GetNodeOrNull<Node3D>("Props") is { } props) props.Visible = false;
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
