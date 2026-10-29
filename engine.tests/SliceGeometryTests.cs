using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The slice of one entity, which is the one the whole section shows for it. Sections I and II are at W = 0 and .42.
public class SliceGeometryTests
{
    private static readonly SectionId[] Sections = [new("I"), new("II")];

    private static void AssertSameSlicesAsSections(World world, params EntityId[] entities)
    {
        foreach (var section in Sections)
        {
            var whole = world.SlicesIn(section);
            foreach (var entity in entities)
            {
                var result = world.GetSliceGeometry(entity, section);
                if (whole.SingleOrDefault(slice => slice.Entity == entity) is not { } expected)
                {
                    Assert.IsType<SliceGeometryResult.Empty>(result);
                    continue;
                }
                var slice = Assert.IsType<SliceGeometryResult.Geometry>(result).Slice;
                Assert.Equal(expected.Entity, slice.Entity);
                Assert.Equal(expected.Positions, slice.Positions);
                Assert.Equal(expected.Normals, slice.Normals);
                Assert.Equal(expected.Uvs, slice.Uvs);
                Assert.Equal(expected.Parts, slice.Parts);
                Assert.Equal(expected.ThroughObject, slice.ThroughObject);
            }
        }
    }

    [Fact]
    public void EachKindOfEntityHasTheSliceItsSectionShows()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default), [.. CabinetRoom(), Cabinet(), Ball(x: 1), Rod(), Actor()]);

        AssertSameSlicesAsSections(world, Floor, new("ceiling"), CabinetId, BallId, RodId, GrazerId, new("actor"));
    }

    [Fact]
    public void AnEntityOrSectionThatIsNotThereIsUnknown()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Rod());

        Assert.Equal(FailureCode.UnknownId, Assert.IsType<SliceGeometryResult.Rejected>(world.GetSliceGeometry(new("missing"), new("I"))).Failure.Code);
        Assert.Equal(FailureCode.UnknownId, Assert.IsType<SliceGeometryResult.Rejected>(world.GetSliceGeometry(RodId, new("III"))).Failure.Code);
    }
}
