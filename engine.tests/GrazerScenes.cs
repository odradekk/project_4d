using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Scenes with a grazer for the tests of metaball bodies. Lengths are in metres, Y is up. Sections I and II are at
// W = 0 and .42.
internal static class GrazerScenes
{
    public static readonly EntityId GrazerId = new("grazer");

    // A ball of radius .5 and weight 1 whose field reaches this threshold at distance .3 from its center, since
    // (1 - .3² / .5²)² = .64² = .4096. Alone, it makes a four-dimensional ball of radius .3.
    public const double LumpRadius = .3;
    public const double LumpThreshold = .4096;

    public static Metaball Ball(string id, Vec4 center, double radius = .5, double weight = 1) =>
        new(new(id), center, radius, weight);

    public static MetaballShape Lump(Vec4 center) => new([Ball("lump", center)], LumpThreshold);

    public static GrazerDefinition Grazer(Pose4 pose, MetaballShape shape) => new(GrazerId, pose, TestSource, shape);

    public static World GrazerScene(Pose4 pose, MetaballShape shape, params EntityDefinition[] more) =>
        Scene([Grazer(pose, shape), .. more]);

    public static UpdateResult.Updated Frame(this World world, Pose4 pose, MetaballShape shape) =>
        Assert.IsType<UpdateResult.Updated>(world.SetGrazerFrame(GrazerId, pose, shape));

    public static Failure Refused(this UpdateResult result) => Assert.IsType<UpdateResult.Rejected>(result).Failure;

    // The volume a closed surface wound outward encloses, summed over the tetrahedra from `center` to its triangles.
    public static double EnclosedVolume(this SliceGeometry slice, Vec3 center) => Enumerable.Range(0, slice.Parts.Count / 3)
        .Sum(t => slice.Positions.Vertex(3 * t).Minus(center)
            .Dot(slice.Positions.Vertex(3 * t + 1).Minus(center).Cross(slice.Positions.Vertex(3 * t + 2).Minus(center))) / 6);

    // The smallest and largest values along one axis of the slice's vertices.
    public static (double Min, double Max) Extent(this SliceGeometry slice, Axis3 axis)
    {
        var values = slice.Coordinates(axis).ToArray();
        return (values.Min(), values.Max());
    }
}
