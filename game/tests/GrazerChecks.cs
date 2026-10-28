using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Creatures;
using Game.Rendering;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Grazers on the grazer test region: the authored grazer, the path the engine is given, the slices in the main view and the
/// lens, and the bite. The grazer's own clock is stopped, so every check moves time itself.
/// </summary>
public partial class GrazerChecks : Node
{
    private static readonly EntityId Grazer = new(GrazerDemo.Grazer);
    private TurningFixture play = null!;
    private GrazerChoreography grazers => play.Region.Grazers!;
    private PrismView prism => play.Region.GetNode<PrismView>("Generated/Prism");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await Load();
            CheckAuthoring();
            CheckAuthoringErrors();
            await Load();
            CheckPath();
            await Load();
            CheckMouth();
            await Load();
            await CheckSlices();
            await Load();
            CheckLensAfterTheEnd();
            await Load();
            CheckHoldingStill();
            await Load();
            await CheckBite();
            await CheckBiteOfIdsWithOneNodeName();
            GD.Print("Grazer checks passed: authoring, path, slices, bite.");
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

    private async Task Load()
    {
        await play.Load(scene: GrazerDemo.Scene);
        grazers.SetProcess(false);
    }

    private void CheckAuthoring()
    {
        Check(play.Region.Grazers is not null, "a region with a grazer has its choreography");
        var state = play.State("grazer");
        Check(state.Definition is GrazerDefinition { Shape.Balls.Count: 25 }, "the authored body is the engine's grazer");
        Check(state.Pose.Position == new Vec4(0, 1.4, -2.2, 11), "its first frame is the node's place and W");
        Check(play.State("grazer").Pose.Rotation == new Rotation4(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1)),
            "with no Rotation4 it starts unturned");
        Check(!play.Region.HasNode("Generated/Ⅰ/Geometry/grazer"), "its slice is not part of the static geometry");
    }

    private void CheckAuthoringErrors()
    {
        var node = play.Region.GetNode<Grazer3D>("Grazers/Grazer");
        bool Reports(string property)
        {
            var result = new SceneReader(play.Region).Load();
            return result is LoadResult.Invalid invalid && invalid.Errors.Any(error => error.Source.NodePath == "Grazers/Grazer" && error.Source.Property == property);
        }
        Check(!Reports("Shape") && !Reports("OpenShape") && !Reports("Rotation4") && !Reports("EntityId"), "the grazer as authored has no errors");
        var open = node.OpenShape;
        node.OpenShape = (MetaballShapeResource)open!.Duplicate(true);
        node.OpenShape.Balls[0].Id = "another";
        Check(Reports("OpenShape"), "an open shape with other balls is reported at OpenShape");
        node.OpenShape = (MetaballShapeResource)open.Duplicate(true);
        node.OpenShape.Balls[5].Radius = 0;
        Check(Reports("OpenShape"), "an open shape that the engine refuses is reported at OpenShape when the region loads");
        node.OpenShape = open;
        var shut = node.Shape;
        node.Shape = (MetaballShapeResource)shut!.Duplicate(true);
        node.Shape.Balls[3].Radius = 0;
        Check(Reports("Shape.Balls[3].Radius"), "a ball with no radius is reported at its place in Shape, by the engine's check");
        node.Shape.Balls[3].Radius = .45;
        node.Shape.Balls[3].Center = [1, 2, 3];
        Check(Reports("Shape.Balls[3].Center"), "a ball center that is not four numbers is reported at its place in Shape");
        node.Shape = null;
        Check(Reports("Shape"), "a missing shape is reported at Shape");
        node.Shape = shut;
        node.Rotation4 = new double[7];
        Check(Reports("Rotation4"), "a rotation that is not 16 numbers is reported at Rotation4");
        // A quarter turn in the XY plane: +X goes to +Y and +Y to -X.
        node.Rotation4 = [0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        var loaded = new SceneReader(play.Region).Load();
        Check(loaded is LoadResult.Loaded { World: var world } && ((EntityReadResult.Found)world.ReadEntity(Grazer)).Entity.Pose.Rotation.X == new Vec4(0, 1, 0, 0),
            "Rotation4 is read a column at a time");
        node.Rotation4 = [];
        // Trying the open shape at load leaves the grazer shut: with its first frame in section I, the slice is as wide as the
        // ring of lips, 2 m round plus what the surface adds, and not 3 m round as with the mouth open.
        node.W = 0;
        var shutLoad = new SceneReader(play.Region).Load();
        node.W = 11;
        var width = shutLoad is LoadResult.Loaded { World: var shutWorld } ? WidthOf(shutWorld) : -1;
        Check(width is > 2 and < 3.2, $"the region loads with the grazer's mouth shut: {width} m wide");
    }

    private static double WidthOf(World world)
    {
        var slice = ((SectionGeometryResult.Geometry)world.GetSectionGeometry(new("Ⅰ"))).Entities.First(entity => entity.Entity == Grazer);
        var xs = slice.Positions.Where((_, index) => index % 3 == 0).ToArray();
        return xs.Max() - xs.Min();
    }

    private void CheckPath()
    {
        var timeline = new GrazerTimeline(
            [new(0, new(0, 1, -2, 10), [new(RotationPlane.XW, 0)], 0), new(10, new(4, 1, -2, 6), [new(RotationPlane.XW, Math.PI / 2)], 1)], []);
        grazers.Play("grazer", timeline);
        Check(grazers.IsPlaying("grazer"), "a grazer plays once told to");
        grazers.Advance(5);
        var half = play.State("grazer").Pose;
        Check(half.Position == new Vec4(2, 1, -2, 8), "half way along the path, half way in position");
        var cos = Math.Cos(Math.PI / 4);
        Near4(half.Rotation.X, new(cos, 0, 0, cos), "XW turned by a quarter of a right angle takes +X half way to +W");
        grazers.Advance(5);
        var end = play.State("grazer").Pose;
        Check(end.Position == new Vec4(4, 1, -2, 6) && !grazers.IsPlaying("grazer"), "at the end of the path the grazer is at the last key and stops");
        Near4(end.Rotation.X, new(0, 0, 0, 1), "XW turned by a right angle takes +X to +W");
        grazers.Advance(5);
        Check(play.State("grazer").Pose == end, "a grazer that has finished stays where it ended");
        GrazerKey Still(double time) => new(time, new(0, 1, -2, 10), [new(RotationPlane.XW, 0)], 0);
        void Refused(string what, string grazer, IReadOnlyList<EntityId> elements, IReadOnlyList<SeamId> seams)
        {
            try
            {
                grazers.Play(grazer, new GrazerTimeline([Still(0), Still(10)], [new(5, elements, seams)]));
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new InvalidOperationException($"playing must be refused for {what}");
        }
        Refused("a grazer that is not in the region", "nobody", [], []);
        Refused("a bite of an element that is not there", "grazer", [new("nothing")], []);
        Refused("a bite of something that is not a building element", "grazer", [Grazer], []);
        Refused("a bite of a seam that is not there", "grazer", [], [new("nowhere")]);
        Check(!grazers.IsPlaying("grazer"), "a refused timeline does not start");
    }

    private static void Near4(Vec4 actual, Vec4 expected, string message) =>
        Check(Math.Abs(actual.X - expected.X) + Math.Abs(actual.Y - expected.Y) + Math.Abs(actual.Z - expected.Z) + Math.Abs(actual.W - expected.W) < 1e-9,
            $"{message}: expected {expected}, got {actual}");

    private void CheckMouth()
    {
        GrazerKey Mouth(double time, double mouth) => new(time, new(0, 1.4, -2.2, 0), [], mouth);
        grazers.Play("grazer", new GrazerTimeline([Mouth(0, 0), Mouth(10, 1)], []));
        grazers.Advance(0);
        var shut = play.SliceBounds("Ⅰ", "grazer").Size.X;
        grazers.Advance(10);
        var open = play.SliceBounds("Ⅰ", "grazer").Size.X;
        // The ring of lips goes from 1.0 to 1.5 m round the origin, so the mouth is a metre wider, give or take the lattice.
        Check(open - shut is > .6f and < 1.4f, $"opening the mouth widens the slice by about a metre: {shut} to {open}");
    }

    private bool EngineSlices(string section) =>
        play.Region.World!.HasSlice(Grazer, new SectionId(section)) is SlicePresenceResult.Present { HasVolume: true };

    private async Task CheckSlices()
    {
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(0);
        Check(!EngineSlices("Ⅰ") && !play.HasSlice("Ⅰ", "grazer"), "far up in W, the grazer is in no section");
        // W is 2.87 here: the section at 2.4 is cut, and the ones at 1.2 and 0 are not.
        grazers.Advance(18);
        Check(EngineSlices("Ⅲ") && !EngineSlices("Ⅱ") && !EngineSlices("Ⅰ"), "the engine cuts the deepest section first");
        Check(!play.HasSlice("Ⅲ", "grazer"), "a section nobody looks at is not drawn");
        prism.SetRaised(true);
        prism.MoveTarget(1);
        Check(prism.TargetSection == "Ⅲ", "the lens looks at the deepest section");
        grazers.Advance(0);
        Check(play.HasSlice("Ⅲ", "grazer"), "the lens shows the grazer's slice in the section it looks at");
        Check(!play.HasSlice("Ⅱ", "grazer") && !play.HasSlice("Ⅰ", "grazer"), "and only there");
        Check(play.Mask("Ⅲ", "grazer") != Colors.Black, "the slice has the glowing outline of a through-object in the lens");
        prism.SetRaised(false);
        grazers.Advance(0);
        Check(!play.HasSlice("Ⅲ", "grazer"), "with the lens down, the slice there is dropped");
        grazers.Advance(9);
        Check(EngineSlices("Ⅰ") && play.HasSlice("Ⅰ", "grazer"), "in the player's section the main view shows the slice");
        Check(!play.Region.HasNode("Generated/Ⅰ/Dynamic/grazer/Collision"), "which nothing in the world stops at");
        var bounds = play.SliceBounds("Ⅰ", "grazer");
        Check(bounds.HasPoint(new(0, 1.4f, -2.2f)) && bounds.Size.X > 2 && bounds.Size.X < 4,
            $"the slice is the body round its origin, about the width of its mouth ring: {bounds}");
        Check(play.Region.GetNode("Generated/Ⅰ/Dynamic").GetChildCount(includeInternal: false) == 1, "one node for the grazer, replaced each frame");
        prism.SetRaised(true);
        prism.MoveTarget(-1);
        grazers.Advance(0);
        Check(prism.TargetSection == "Ⅱ" && EngineSlices("Ⅱ") && play.HasSlice("Ⅱ", "grazer") && !play.HasSlice("Ⅲ", "grazer"),
            "the lens shows the slice of the section it looks at, which the engine cuts");
    }

    private void CheckLensAfterTheEnd()
    {
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(99);
        Check(!grazers.IsPlaying("grazer") && EngineSlices("Ⅱ") && !play.HasSlice("Ⅱ", "grazer"), "after the timeline, a section nobody looks at is not drawn");
        prism.SetRaised(true);
        grazers.Advance(0);
        Check(prism.TargetSection == "Ⅱ" && play.HasSlice("Ⅱ", "grazer"), "raising the lens then shows the slice in the section it looks at");
        prism.SetRaised(false);
        grazers.Advance(0);
        Check(!play.HasSlice("Ⅱ", "grazer"), "and lowering it drops the slice");
    }

    private void CheckHoldingStill()
    {
        GrazerKey Still(double time) => new(time, new(0, 1.4, -2.2, .1), [new(RotationPlane.XZ, .3)], .2);
        grazers.Play("grazer", new GrazerTimeline([Still(0), Still(10)], []));
        grazers.Advance(1);
        var slice = play.Region.GetNode("Generated/Ⅰ/Dynamic/grazer").GetInstanceId();
        grazers.Advance(1);
        Check(play.Region.GetNode("Generated/Ⅰ/Dynamic/grazer").GetInstanceId() == slice, "a grazer that holds still is not cut again");
    }

    // Two IDs that Godot gives the same node name, Door.1 and Door_1: the second node is renamed. A bite takes the one it names.
    private async Task CheckBiteOfIdsWithOneNodeName()
    {
        await play.Load(region =>
        {
            region.GetNode<BuildingElement3D>("Buildings/Home/Door").EntityId = "Door.1";
            region.GetNode<BuildingElement3D>("Buildings/Home/Lintel").EntityId = "Door_1";
            var needle = region.GetNode<Seam3D>("Seams/Needle");
            var other = (Seam3D)needle.Duplicate();
            other.Name = "Other";
            other.SeamId = "needle_1";
            other.Position = new(-2, 0, 1);
            needle.SeamId = "needle.1";
            region.GetNode("Seams").AddChild(other);
        }, GrazerDemo.Scene);
        grazers.SetProcess(false);
        // The door is 2.2 m high and stands on the floor, the lintel above it: their centers are 1.1 and 2.6 m up.
        float[] Heights(string section) => play.Region.GetNode($"Generated/{section}/Geometry").GetChildren().OfType<Node3D>()
            .Select(node => node.GetNode<MeshInstance3D>("Surface").Mesh.GetAabb().GetCenter())
            .Where(center => Math.Abs(center.X) < .1f && Math.Abs(center.Z + 4) < .1f).Select(center => center.Y).Order().ToArray();
        Check(Heights("Ⅰ").Length == 2 && Heights("Ⅱ").Length == 2, "the door and the lintel stand in both sections");
        GrazerKey Still(double time) => new(time, new(0, 1.4, -2.2, .1), [], 0);
        grazers.Play("grazer", new GrazerTimeline([Still(0), Still(1)], [new(1, [new("Door_1")], [new("needle.1")])]));
        grazers.Advance(1);
        var world = play.Region.World!;
        Check(world.ReadEntity(new("Door_1")) is EntityReadResult.Missing && world.ReadEntity(new("Door.1")) is EntityReadResult.Found,
            "the engine took the lintel and left the door");
        foreach (var section in new[] { "Ⅰ", "Ⅱ" })
        {
            var left = Heights(section);
            Check(left.Length == 1 && Math.Abs(left[0] - 1.1f) < .05f, $"the world of {section} took the lintel and left the door");
        }
        await play.Frames(3);
        var lights = play.Region.GetNode("Generated/Ⅱ/Overlay/Seams").GetChildren().Cast<Node3D>().ToArray();
        Check(lights.Length == 2 && lights.Single(light => light.HasNode("Cut")).Position.X > 1 && lights.Single(light => !light.HasNode("Cut")).Position.X < -1,
            "the seam that was bitten shows as cut, and the one with the same node name does not");
    }

    private async Task CheckBite()
    {
        var door = new EntityId("Door");
        var world = play.Region.World!;
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(GrazerDemo.BiteTime - .5);
        foreach (var section in new[] { "Ⅰ", "Ⅱ", "Ⅲ" })
            Check(play.Region.HasNode($"Generated/{section}/Geometry/Door"), $"the door stands in {section} before the bite");
        Check(world.ReadEntity(door) is EntityReadResult.Found && !Severed(), "and the engine has it, and the seam whole");
        grazers.Advance(.5);
        foreach (var section in new[] { "Ⅰ", "Ⅱ", "Ⅲ" })
            Check(!play.Region.HasNode($"Generated/{section}/Geometry/Door"), $"after the bite the door is gone from {section}");
        Check(play.Region.HasNode("Generated/Ⅰ/Geometry/Lintel"), "and what was not bitten stays");
        Check(world.ReadEntity(door) is EntityReadResult.Missing && Severed(), "the engine has no door, and the seam is cut");
        await play.Frames(3);
        Check(play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/needle/Cut"), "the seam shows as cut");
        grazers.Advance(10);
        Check(world.ReadEntity(door) is EntityReadResult.Missing, "a bite is taken once");
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(30);
        Check(grazers.IsPlaying("grazer"), "a timeline that bites what is already bitten plays on");

        bool Severed() => ((SeamReadResult.Found)world.ReadSeam(new SeamId("needle"))).Seam.Severed;
    }
}
