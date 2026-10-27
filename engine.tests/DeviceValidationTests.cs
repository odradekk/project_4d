using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// What a device must satisfy to load: its loft, its pose and its motion.
public class DeviceValidationTests
{
    private sealed record UnknownMotion : DeviceMotion;

    private static DeviceDefinition WithSegments(params LoftSegment[] segments) => Sash() with { Shape = new(segments) };

    private static DeviceDefinition WithKeys(params LoftKey[] keys) => Sash() with { Shape = Loft(keys) };

    // A loft that holds one part, the same at both its keys, so that only the shape of the part can be at fault.
    private static DeviceDefinition Lone(ConvexPart part) => WithKeys(Key(0, part), Key(.4, part));

    private static ConvexPart WithVertex(ConvexPart part, int index, Vec3 vertex) =>
        part with { Vertices = [.. part.Vertices.Select((existing, i) => i == index ? vertex : existing)] };

    // The part with a vertex added in the middle of the edge between two vertices, in the faces on either side of it.
    private static ConvexPart WithMidpoint(ConvexPart part, int first, int second)
    {
        var (a, b) = (part.Vertices[first], part.Vertices[second]);
        var added = part.Vertices.Count;
        IEnumerable<int> Inserted(IReadOnlyList<int> face) => face.SelectMany((vertex, i) =>
            new[] { first, second }.Contains(vertex) && new[] { first, second }.Contains(face[(i + 1) % face.Count]) ? new[] { vertex, added } : new[] { vertex });
        return part with
        {
            Vertices = [.. part.Vertices, new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2)],
            Faces = [.. part.Faces.Select(face => face with { Vertices = [.. Inserted(face.Vertices)] })]
        };
    }

    // The L-shaped prism: a polygon of six corners with a reflex one, lifted by 1 m.
    private static ConvexPart LShape()
    {
        Vec3[] plan = [new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0)];
        var vertices = plan.Concat(plan.Select(corner => new Vec3(corner.X, corner.Y, 1))).ToArray();
        var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 }, new[] { 6, 7, 8, 9, 10, 11 } };
        for (var i = 0; i < 6; i++) faces.Add(new[] { i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6 });
        return new(new("l"), vertices, faces.Select(face => new ConvexFace(face, new FaceUv(Axis3.Z, true, new(1, 1), new(0, 0)))).ToArray());
    }

    private static readonly ConvexPart Slab = LoftBox("pane", (0, 1), (0, 1), (0, 1));
    private static readonly ConvexPart Other = LoftBox("other", (0, 1), (0, 1), (0, 1));

    public static IEnumerable<object[]> InvalidDevices()
    {
        var hanger = Hanger();
        var sash = Sash();
        var pane = Pane(0);

        // Pose
        yield return [sash with { Pose = At(0, 0, 0, rotation: new(new(2, 0, 0, 0), Identity4.Y, Identity4.Z, Identity4.W)) }, LoadErrorCode.InvalidPose];
        yield return [sash with { Pose = At(double.NaN, 0, 0) }, LoadErrorCode.InvalidNumber];

        // Segments and keys
        yield return [sash with { Shape = null! }, LoadErrorCode.InvalidGeometry];
        yield return [WithSegments(), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, pane)), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, pane), Key(0, pane)), LoadErrorCode.InvalidInterval];
        yield return [WithKeys(Key(.4, pane), Key(.2, pane)), LoadErrorCode.InvalidInterval];
        yield return [WithKeys(Key(double.NaN, pane), Key(.4, pane)), LoadErrorCode.InvalidNumber];
        yield return [WithSegments(new([Key(0, pane), Key(.4, pane)]), new([Key(.3, Other), Key(.7, Other)])), LoadErrorCode.InvalidInterval];
        yield return [WithKeys(Key(0), Key(.4)), LoadErrorCode.InvalidGeometry];

        // Parts, and their correspondence between keys
        yield return [WithKeys(Key(0, pane, pane), Key(.4, pane, pane)), LoadErrorCode.DuplicateId];
        yield return [WithSegments(new([Key(0, pane), Key(.4, pane)]), new([Key(.4, pane), Key(.8, pane)])), LoadErrorCode.DuplicateId];
        yield return [WithKeys(Key(0, pane), Key(.4, pane, LoftBox("extra", (2, 3), (0, 1), (0, 1)))), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, pane), Key(.4, pane with { Id = new("other") })), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, pane), Key(.4, pane with { Faces = [.. pane.Faces.Reverse()] })), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, pane), Key(.4, pane with { Faces = [.. pane.Faces.Select(face => face with { Uv = face.Uv with { Offset = new(1, 0) } })] })), LoadErrorCode.InvalidGeometry];

        // Each key shape must be a closed convex solid with outward faces
        FaceUv Uv(FaceUv uv, Vec2? scale = null, Vec2? offset = null) => uv with { Scale = scale ?? uv.Scale, Offset = offset ?? uv.Offset };
        ConvexPart WithUvs(Func<FaceUv, FaceUv> change) => pane with { Faces = [.. pane.Faces.Select(face => face with { Uv = change(face.Uv) })] };
        ConvexPart WithFaceVertices(Func<ConvexFace, IReadOnlyList<int>> change) => pane with { Faces = [.. pane.Faces.Select(face => face with { Vertices = change(face) })] };
        yield return [Lone(WithUvs(uv => Uv(uv, scale: new(0, 1)))), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithUvs(uv => uv with { Axis = (Axis3)5 })), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithUvs(uv => Uv(uv, offset: new(double.NaN, 0)))), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithVertex(pane, 0, new(double.NaN, 0, 0))), LoadErrorCode.InvalidNumber];
        yield return [Lone(pane with { Faces = [] }), LoadErrorCode.InvalidGeometry];
        yield return [Lone(pane with { Faces = [.. pane.Faces.Take(3)] }), LoadErrorCode.InvalidGeometry];
        yield return [Lone(pane with { Faces = [.. pane.Faces.Skip(1)] }), LoadErrorCode.InvalidGeometry];
        yield return [Lone(pane with { Faces = [.. pane.Faces, pane.Faces[0]] }), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithFaceVertices(face => [.. face.Vertices.Reverse()])), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithFaceVertices(face => [.. face.Vertices.Select(v => v == 0 ? 8 : v)])), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithFaceVertices(face => [.. face.Vertices.Select(v => v == 0 ? -1 : v)])), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithFaceVertices(face => [.. face.Vertices.Take(2)])), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithVertex(pane, 7, new(-.6, .9, .5))), LoadErrorCode.InvalidGeometry];
        yield return [Lone(LShape()), LoadErrorCode.InvalidGeometry];
        yield return [Lone(WithMidpoint(pane, 6, 7)), LoadErrorCode.InvalidGeometry];

        // A loft must stay flat between keys, and thicker than the sampling distance
        yield return [WithKeys(Key(0, pane), Key(.4, pane with { Vertices = [.. pane.Vertices.Select(v => new Vec3(v.X + .2 * v.Y, v.Y, v.Z))] })), LoadErrorCode.InvalidGeometry];
        yield return [Lone(LoftBox("pane", (0, 1), (0, 1), (0, .009))), LoadErrorCode.InvalidGeometry];
        yield return [Lone(LoftBox("pane", (0, 1), (0, 1), (0, .01))), LoadErrorCode.InvalidGeometry];
        yield return [WithKeys(Key(0, Slab), Key(.009, Slab)), LoadErrorCode.InvalidGeometry];

        // Motion
        yield return [sash with { Motion = null! }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Motion = new UnknownMotion() }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Motion = new DeviceTranslation(Axis3.X, new(1, 1), 1) }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Motion = new DeviceTranslation(Axis3.X, new(double.NaN, 1), 0) }, LoadErrorCode.InvalidNumber];
        yield return [sash with { Motion = new DeviceTranslation(Axis3.X, new(0, 1), 1.01) }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Motion = new DeviceTranslation(Axis3.X, new(0, 1), -.01) }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Motion = new DeviceTranslation((Axis3)7, new(0, 1), 0) }, LoadErrorCode.InvalidMotion];
        yield return [hanger with { Motion = new DeviceRotation((RotationPlane)9, default, new(0, 1), 0) }, LoadErrorCode.InvalidMotion];
        yield return [hanger with { Motion = new DeviceRotation(RotationPlane.XY, new(double.NaN, 0, 0, 0), new(0, 1), 0) }, LoadErrorCode.InvalidNumber];
        yield return [hanger with { Motion = new DeviceRotation(RotationPlane.XY, default, new(0, 1), double.NaN) }, LoadErrorCode.InvalidNumber];

        // A track runs along one of the world axes, with no part in W
        var aboutYBy45 = new Rotation4(new(Math.Sqrt(.5), 0, -Math.Sqrt(.5), 0), Identity4.Y, new(Math.Sqrt(.5), 0, Math.Sqrt(.5), 0), Identity4.W);
        yield return [sash with { Pose = At(0, 0, 0, rotation: aboutYBy45) }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Pose = At(0, 0, 0, rotation: XwRotation(.8, .6)) }, LoadErrorCode.InvalidMotion];
        yield return [sash with { Pose = At(0, 0, 0, rotation: XwRotation(Math.Sqrt(1 - 1e-8), 1e-4)) }, LoadErrorCode.InvalidMotion];
        var (cos, sin) = (Math.Cos(1e-3), Math.Sin(1e-3));
        yield return [sash with { Pose = At(0, 0, 0, rotation: new(new(cos, sin, 0, 0), new(-sin, cos, 0, 0), Identity4.Z, Identity4.W)) }, LoadErrorCode.InvalidMotion];
    }

    [Theory]
    [MemberData(nameof(InvalidDevices))]
    public void ADeviceThatBreaksARuleDoesNotLoad(DeviceDefinition device, LoadErrorCode code)
    {
        var errors = LoadErrors(Description([device]));

        Assert.Contains(errors, error => error.Code == code && error.Entity == device.Id);
    }

    [Fact]
    public void AnErrorPointsAtThePartThatDoesNotMatch()
    {
        var device = WithKeys(Key(0, Pane(0)), Key(.4, Pane(0) with { Id = new("other") }));

        var error = Assert.Single(LoadErrors(Description([device])));

        Assert.Equal(LoadErrorCode.InvalidGeometry, error.Code);
        Assert.Equal($"{device.Source.Property}.Shape.Segments[0].Keys[1].Parts[0]", error.Source.Property);
    }

    [Fact]
    public void APartJustThickerThanTheSamplingDistanceLoads()
    {
        var thin = LoftBox("pane", (0, 1), (0, 1), (0, .0101));

        Assert.IsType<LoadResult.Loaded>(World.Load(Description([Lone(thin)])));
    }

    [Fact]
    public void ADeviceMayStartTiltedIntoW()
    {
        // Turning x into W leaves a track along y in the world's XYZ.
        var tilted = Lift() with { Pose = At(0, 0, 0, rotation: XwRotation(.8, .6)) };

        Assert.IsType<LoadResult.Loaded>(World.Load(Description([tilted])));
    }

    [Fact]
    public void RotationLimitsMayExceedAFullTurn()
    {
        var hanger = Hanger() with { Motion = new DeviceRotation(RotationPlane.YZ, default, new(-10, 10), 2 * Math.PI) };

        Assert.IsType<LoadResult.Loaded>(World.Load(Description([hanger])));
    }
}
