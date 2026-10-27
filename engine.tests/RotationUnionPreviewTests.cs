using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public partial class RotationTests
{
    private static bool Covers(IReadOnlyList<Polyline> lines, Vec3 point) => Segments(lines).Any(s => s.Contains(point));

    // A rod made of `parts`, hinged at x = -.48, planned to stand up into section II.
    private static (World World, RotationPlan Plan) PlanStandUp(SectionDefinition[] sections, params BoxPart[] parts)
    {
        var farthestX = parts.Max(p => p.Center.X
            + Math.Abs(p.Rotation.X.X) * p.HalfSize.X
            + Math.Abs(p.Rotation.Y.X) * p.HalfSize.Y
            + Math.Abs(p.Rotation.Z.X) * p.HalfSize.Z);
        var item = LongRod with
        {
            Shape = Shape(parts),
            Interaction = LongRod.Interaction with { Reach = farthestX + .48 },
        };
        var actor = Actor(At(0, 1, 1));
        var world = Load(Description(sections, item, actor));
        return (world, world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II"))));
    }

    // The outline of the rod's slice in section I before the stand-up starts.
    private static IReadOnlyList<Polyline> SliceContours(params BoxPart[] parts)
    {
        var (world, plan) = PlanStandUp([SectionI, SectionII], parts);
        return Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, new("I"))).Value.Samples[0].Contours;
    }

    private static Rotation3 AboutY(double cos, double sin) => new(new(cos, 0, -sin), new(0, 1, 0), new(sin, 0, cos));

    // A segment runs along an edge of a box when it lies on face planes in at least two axes.
    private static bool IsBoxEdge(Segment segment, double[] xFaces, double[] yFaces, double[] zFaces)
    {
        var onFace = new[]
        {
            xFaces.Any(x => segment.LiesOnPlane(Axis3.X, x)),
            yFaces.Any(y => segment.LiesOnPlane(Axis3.Y, y)),
            zFaces.Any(z => segment.LiesOnPlane(Axis3.Z, z)),
        };
        return onFace.Count(on => on) >= 2;
    }

    // The 12 edges of the 1 x .6 x .6 box around the origin, and nothing else.
    private static void AssertCuboidBoundary(IReadOnlyList<Polyline> lines)
    {
        var segments = Segments(lines).ToArray();
        Assert.NotEmpty(segments);
        Assert.Equal(8.8, segments.Sum(s => s.Length), 9);
        Assert.All(segments, s => Assert.True(IsBoxEdge(s, [-.5, .5], [-.3, .3], [-.3, .3])));
        foreach (var (a, b) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
        {
            Assert.True(Covers(lines, new(.031, a * .3, b * .3)));
            Assert.True(Covers(lines, new(a * .5, .031, b * .3)));
            Assert.True(Covers(lines, new(a * .5, b * .3, .031)));
        }
    }

    // The outline is a polygon in the XZ plane (corners given as X, Z) extruded between y = -.3 and y = .3.
    private static void AssertExtrudedBoundary(IReadOnlyList<Polyline> lines, (double X, double Z)[] polygon)
    {
        var expected = new List<Segment>();
        for (var i = 0; i < polygon.Length; i++)
        {
            var corner = polygon[i];
            var next = polygon[(i + 1) % polygon.Length];
            expected.Add(new(new(corner.X, -.3, corner.Z), new(corner.X, .3, corner.Z)));
            foreach (var y in new[] { -.3, .3 })
            {
                expected.Add(new(new(corner.X, y, corner.Z), new(next.X, y, next.Z)));
            }
        }
        var actual = Segments(lines).ToArray();
        Assert.NotEmpty(actual);
        Assert.All(actual, s => Assert.Contains(expected, edge => edge.Contains(s.A) && edge.Contains(s.B)));
        foreach (var edge in expected)
        {
            foreach (var t in new[] { .25, .5, .75 })
            {
                Assert.True(Covers(lines, edge.PointAt(t)));
            }
        }
        Assert.Equal(expected.Sum(e => e.Length), actual.Sum(s => s.Length), 9);
    }

    [Fact]
    public void PreviewOmitsEdgesOfAContainedPart()
    {
        var lines = SliceContours(Part("outer", new(.5, .3, .3)), Part("inner", new(.1, .1, .1)));

        AssertCuboidBoundary(lines);
    }

    [Fact]
    public void PreviewOmitsCoplanarSeamsOfOverlappingParts()
    {
        var lines = SliceContours(
            Part("left", new(.3, .3, .3), center: new(-.2, 0, 0)),
            Part("right", new(.3, .3, .3), center: new(.2, 0, 0)));

        AssertCuboidBoundary(lines);
    }

    [Fact]
    public void PreviewKeepsNewConcaveEdgesOfLShapedUnion()
    {
        var lines = SliceContours(
            Part("a", new(.3, .3, .3), center: new(-.2, 0, 0)),
            Part("b", new(.3, .3, .3), center: new(.2, 0, .2)));

        AssertExtrudedBoundary(lines, [(-.5, -.3), (.1, -.3), (.1, -.1), (.5, -.1), (.5, .5), (-.1, .5), (-.1, .3), (-.5, .3)]);
        Assert.True(Covers(lines, new(.1, 0, -.1)));
        Assert.True(Covers(lines, new(-.1, 0, .3)));
        Assert.False(Covers(lines, new(.1, 0, 0)));
        Assert.False(Covers(lines, new(-.1, 0, .1)));
        Assert.True(Covers(lines, new(-.5, 0, -.3)));
        Assert.True(Covers(lines, new(.5, 0, .5)));
    }

    [Fact]
    public void PreviewKeepsIntersectionEdgesWithRotatedOverlappingPart()
    {
        var c = Math.Sqrt(.5);
        var lines = SliceContours(
            Part("a", new(.5, .3, .3)),
            Part("b", new(.2, .3, .2), center: new(.4, 0, 0), rotation: AboutY(c, c)));
        var z = .2 * Math.Sqrt(2) - .1;
        var tip = .4 + .2 * Math.Sqrt(2);

        AssertExtrudedBoundary(lines, [(-.5, -.3), (.5, -.3), (.5, -z), (tip, 0), (.5, z), (.5, .3), (-.5, .3)]);
        Assert.True(Covers(lines, new(.5, 0, z)));
        Assert.True(Covers(lines, new(.5, 0, -z)));
        Assert.False(Covers(lines, new(.5, 0, 0)));
        Assert.True(Covers(lines, new(tip, 0, 0)));
    }

    [Fact]
    public void PreviewOfObliqueSlicesOmitsContainedPartEdges()
    {
        var middle = new SectionDefinition(new("middle"), .3, TestSource);
        var (world, plan) = PlanStandUp(
            [SectionI, middle, SectionII], Part("outer", new(.5, .3, .3)), Part("inner", new(.1, .1, .1)));
        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, new("middle"))).Value;
        var lines = preview.Samples.Single(s => s.Progress == .5).Contours;
        var minX = -.18 - Epsilon * Math.Sqrt(2);
        var maxX = -.18 + Epsilon * Math.Sqrt(2);

        var segments = Segments(lines).ToArray();
        Assert.NotEmpty(segments);
        Assert.All(segments, s => Assert.True(IsBoxEdge(s, [minX, maxX], [-.3, .3], [-.3, .3])));
        Assert.True(Covers(lines, new(minX, 0, -.3)));
        Assert.True(Covers(lines, new(maxX, 0, .3)));
    }

    [Fact]
    public void PreviewOmitsSharedFaceOfAdjacentParts()
    {
        var lines = SliceContours(
            Part("left", new(.25, .3, .3), center: new(-.25, 0, 0)),
            Part("right", new(.25, .3, .3), center: new(.25, 0, 0)));

        AssertCuboidBoundary(lines);
    }

    [Fact]
    public void PreviewFindsEdgesAtNarrowAngledIntersections()
    {
        var angle = Math.PI / 180;
        var c = Math.Cos(angle);
        var sn = Math.Sin(angle);
        var lines = SliceContours(
            Part("a", new(.5, .3, .3)),
            Part("b", new(.3, .3, .3), center: new(.2, 0, 0), rotation: AboutY(c, sn)));
        var lower = (.3 * c - .3) / sn;
        var upper = (.3 - .3 * sn) / c;

        Assert.True(Covers(lines, new(.5, 0, lower)));
        Assert.True(Covers(lines, new(.5, 0, upper)));
        Assert.False(Covers(lines, new(.5, 0, 0)));
        Assert.True(Covers(lines, new(.2 + .3 * (c + sn), 0, .3 * (c - sn))));
    }

    [Fact]
    public void PreviewDoesNotDuplicateCoincidentPartEdges()
    {
        var lines = SliceContours(Part("a", new(.5, .3, .3)), Part("b", new(.5, .3, .3)));

        AssertCuboidBoundary(lines);
    }
}
