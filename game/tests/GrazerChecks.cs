using System;
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
/// Grazers on the grazer test region: the timeline, the authored body, the path the engine is given, the slices in the main
/// view and the lens, and the bite. The grazer's own clock is stopped, so every check moves time itself.
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
            CheckTimeline();
            CheckBadTimelines();
            CheckBody();
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
            await CheckBite();
            GD.Print("Grazer checks passed: timeline, body, authoring, path, slices, bite.");
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

    private static GrazerKey Key(double time, Vec4 position, double angle, double mouth) =>
        new(time, position, [new(RotationPlane.XZ, angle), new(RotationPlane.XW, 2 * angle)], mouth);

    private static GrazerTimeline Straight() => new(
        [Key(0, new(0, 0, 0, 10), 0, 0), Key(10, new(2, 1, -4, 0), 1, 1), Key(14, new(2, 3, -4, 0), 1, 1)],
        [new(10, [new EntityId("a")], []), new(12, [], [new SeamId("s")])]);

    private void CheckTimeline()
    {
        var timeline = Straight();
        Check(timeline.Duration == 14, "a timeline lasts until its last key");
        var start = timeline.At(0);
        Check(start.Position == new Vec4(0, 0, 0, 10) && start.Mouth == 0, "at time 0 the first key");
        var middle = timeline.At(5);
        Check(middle.Position == new Vec4(1, .5, -2, 5) && middle.Mouth == .5, "half way between two keys is half way in position and mouth");
        Check(middle.Turns.Select(turn => (turn.Plane, turn.Angle)).SequenceEqual([(RotationPlane.XZ, .5), (RotationPlane.XW, 1.0)]),
            "half way is half way in each plane's angle, with the planes in the keys' order");
        Check(timeline.At(10).Position == new Vec4(2, 1, -4, 0), "at a key time, the key");
        Check(timeline.At(12).Position == new Vec4(2, 2, -4, 0), "between the later keys, the later segment");
        Check(timeline.At(-3).Position == new Vec4(0, 0, 0, 10) && timeline.At(99).Position == new Vec4(2, 3, -4, 0),
            "before the start the first key, after the end the last");
        Check(timeline.At(99).Time == 14, "the key out of range is at the time it was clamped to");
    }

    private static void CheckBadTimelines()
    {
        GrazerKey Plain(double time, double mouth = 0, RotationPlane plane = RotationPlane.XZ) => new(time, new(0, 0, 0, 0), [new(plane, 0)], mouth);
        void Rejected(GrazerKey[] keys, GrazerBite[] bites, string reason)
        {
            try { _ = new GrazerTimeline(keys, bites); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException($"a timeline must be refused: {reason}");
        }
        Rejected([Plain(0)], [], "one key");
        Rejected([Plain(1), Plain(2)], [], "the first key is not at time 0");
        Rejected([Plain(0), Plain(0)], [], "two keys at one time");
        Rejected([Plain(0), Plain(2), Plain(1)], [], "keys out of order");
        Rejected([Plain(0), new(1, new(0, double.NaN, 0, 0), [new(RotationPlane.XZ, 0)], 0)], [], "a position that is not a number");
        Rejected([Plain(0), new(1, new(0, 0, 0, 0), [new(RotationPlane.XZ, double.PositiveInfinity)], 0)], [], "an angle that is not finite");
        Rejected([Plain(0), Plain(1, mouth: 1.1)], [], "a mouth past 1");
        Rejected([Plain(0, mouth: -.1), Plain(1)], [], "a mouth below 0");
        Rejected([Plain(0), Plain(1, plane: RotationPlane.XW)], [], "keys that turn in different planes");
        Rejected([Plain(0), new(1, new(0, 0, 0, 0), [], 0)], [], "keys with different numbers of turns");
        Rejected([Plain(0), Plain(1)], [new(1.5, [], [])], "a bite after the end");
        Rejected([Plain(0), Plain(2)], [new(1, [], []), new(.5, [], [])], "bites out of order");
        _ = new GrazerTimeline([Plain(0), Plain(1)], [new(1, [], [])]);
    }

    private static void CheckBody()
    {
        MetaballShape Shape(double x, double radius, double threshold) =>
            new([new Metaball(new("a"), new(x, 0, 0, 0), radius, 1), new Metaball(new("b"), new(0, x, 0, 2 * x), radius, 2)], threshold);
        var body = new GrazerBody(Shape(1, .5, .25), Shape(3, 1.5, .75));
        Check(Same(body.At(0), body.Shut) && Same(body.At(1), body.Open!), "the mouth shut is the shut shape and wide open the open shape");
        var half = body.At(.5);
        Check(half.Balls[0].Center == new Vec4(2, 0, 0, 0) && half.Balls[1].Center == new Vec4(0, 2, 0, 4),
            "half open is half way between the ball centers");
        Check(half.Balls[0].Radius == 1 && half.Balls[1].Weight == 2 && half.Threshold == .5 && half.Balls[1].Id == new PartId("b"),
            "and between radii and thresholds, with the weights and IDs kept");
        Check(Same(new GrazerBody(Shape(1, .5, .25), null).At(.7), Shape(1, .5, .25)), "a mouth that cannot open leaves the body as it is");
        Check(GrazerBody.SameBalls(Shape(1, .5, .25), Shape(3, 1, .5)), "shapes with the same balls in the same order can be opened between");
        Check(!GrazerBody.SameBalls(Shape(1, .5, .25), new([new Metaball(new("a"), new(0, 0, 0, 0), 1, 1)], 1)), "a ball fewer is not the same body");
        Check(!GrazerBody.SameBalls(Shape(1, .5, .25), new([.. Shape(1, .5, .25).Balls.Reverse()], .25)), "the same balls in another order are not the same body");
    }

    private static bool Same(MetaballShape a, MetaballShape b) => a.Threshold == b.Threshold && a.Balls.SequenceEqual(b.Balls);

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
        node.OpenShape = open;
        var shut = node.Shape;
        node.Shape = (MetaballShapeResource)shut!.Duplicate(true);
        node.Shape.Balls[3].Radius = 0;
        Check(Reports("Shape"), "a ball with no radius is reported at Shape by the engine's check");
        node.Shape.Balls[3].Radius = .45;
        node.Shape.Balls[3].Center = [1, 2, 3];
        Check(Reports("Shape"), "a ball center that is not four numbers is reported at Shape");
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
        try
        {
            grazers.Play("nobody", timeline);
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("playing a grazer that is not in the region must be refused");
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
        Check(play.Mask("Ⅲ", "grazer") == Colors.Black, "the slice of a creature has no glowing outline in the lens");
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

        bool Severed() => ((SeamReadResult.Found)world.ReadSeam(new SeamId("needle"))).Seam.Severed;
    }
}
