using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Rotation4.FromPlaneAngles (docs/frame/contract.md): the host gives angles and the engine makes the matrix. The
// four columns of a Rotation4 are the world directions of the local axes. Expected matrices are worked out by hand.
public class PlaneRotationTests
{
    private static readonly Vec4 EX = new(1, 0, 0, 0);
    private static readonly Vec4 EY = new(0, 1, 0, 0);
    private static readonly Vec4 EZ = new(0, 0, 1, 0);
    private static readonly Vec4 EW = new(0, 0, 0, 1);

    private static Vec4 Minus(Vec4 v) => new(-v.X, -v.Y, -v.Z, -v.W);

    private static Rotation4 Turns(params PlaneAngle[] turns) => Rotation4.FromPlaneAngles(turns);

    [Theory]
    [InlineData(RotationPlane.XY, 0, 1)]
    [InlineData(RotationPlane.XZ, 0, 2)]
    [InlineData(RotationPlane.XW, 0, 3)]
    [InlineData(RotationPlane.YZ, 1, 2)]
    [InlineData(RotationPlane.YW, 1, 3)]
    [InlineData(RotationPlane.ZW, 2, 3)]
    public void AQuarterTurnTakesTheFirstAxisOfAPlaneToItsSecondAndTheSecondToTheFirstReversed(RotationPlane plane, int first, int second)
    {
        Vec4[] axes = [EX, EY, EZ, EW];
        var columns = (Vec4[])axes.Clone();
        columns[first] = axes[second];
        columns[second] = Minus(axes[first]);

        AssertNear(new(columns[0], columns[1], columns[2], columns[3]), Turns(new PlaneAngle(plane, Math.PI / 2)));
    }

    [Fact]
    public void AnAngleTurnsByItsCosineAndSineAndANegativeAngleTurnsTheOtherWay()
    {
        // cos(pi/6) = .8660254 and sin(pi/6) = .5.
        AssertNear(new(new(.8660254, .5, 0, 0), new(-.5, .8660254, 0, 0), EZ, EW), Turns(new PlaneAngle(RotationPlane.XY, Math.PI / 6)), 7);
        AssertNear(new(new(.8660254, -.5, 0, 0), new(.5, .8660254, 0, 0), EZ, EW), Turns(new PlaneAngle(RotationPlane.XY, -Math.PI / 6)), 7);
    }

    [Fact]
    public void TurnsComposeInTheOrderGiven()
    {
        // Each turn is applied to the result of the ones before it. XW takes X to W and W to -X; XY takes X to Y and Y to -X.
        AssertNear(
            new(EW, Minus(EX), EZ, Minus(EY)),
            Turns(new PlaneAngle(RotationPlane.XW, Math.PI / 2), new PlaneAngle(RotationPlane.XY, Math.PI / 2)));
        AssertNear(
            new(EY, Minus(EW), EZ, Minus(EX)),
            Turns(new PlaneAngle(RotationPlane.XY, Math.PI / 2), new PlaneAngle(RotationPlane.XW, Math.PI / 2)));
    }

    [Fact]
    public void TurnsInPlanesThatShareNoAxisGiveTheSameMatrixInEitherOrder()
    {
        // The contract's example, [XW(a), YZ(b)] is YZ(b) times XW(a): X to W, Y to Z, Z to -Y, W to -X.
        var expected = new Rotation4(EW, EZ, Minus(EY), Minus(EX));

        AssertNear(expected, Turns(new PlaneAngle(RotationPlane.XW, Math.PI / 2), new PlaneAngle(RotationPlane.YZ, Math.PI / 2)));
        AssertNear(expected, Turns(new PlaneAngle(RotationPlane.YZ, Math.PI / 2), new PlaneAngle(RotationPlane.XW, Math.PI / 2)));
    }

    [Fact]
    public void TwoTurnsInOnePlaneAddUpAndAnAngleNeedNotBeWithinOneRound()
    {
        // cos(.7) = .7648422 and sin(.7) = .6442177.
        var sevenTenths = new Rotation4(new(.7648422, 0, 0, .6442177), EY, EZ, new(-.6442177, 0, 0, .7648422));
        AssertNear(sevenTenths, Turns(new PlaneAngle(RotationPlane.XW, .3), new PlaneAngle(RotationPlane.XW, .4)), 7);

        var quarter = new Rotation4(EW, EY, EZ, Minus(EX));
        AssertNear(quarter, Turns(new PlaneAngle(RotationPlane.XW, 2 * Math.PI + Math.PI / 2)));
        AssertNear(quarter, Turns(new PlaneAngle(RotationPlane.XW, -3 * Math.PI / 2)));
    }

    [Fact]
    public void NoTurnsGiveTheIdentity()
    {
        Assert.Equal(Identity4, Rotation4.FromPlaneAngles([]));
        Assert.Equal(Identity4, Turns(new PlaneAngle(RotationPlane.YW, 0)));
    }

    [Fact]
    public void TheResultIsOrthonormalAndRightHanded()
    {
        var rotation = Turns(
            new PlaneAngle(RotationPlane.XW, .7), new PlaneAngle(RotationPlane.YZ, -1.1), new PlaneAngle(RotationPlane.XY, 2.5), new PlaneAngle(RotationPlane.ZW, .2),
            new PlaneAngle(RotationPlane.YW, 9), new PlaneAngle(RotationPlane.XZ, -4));

        Vec4[] columns = [rotation.X, rotation.Y, rotation.Z, rotation.W];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                var dot = columns[i].X * columns[j].X + columns[i].Y * columns[j].Y + columns[i].Z * columns[j].Z + columns[i].W * columns[j].W;
                Assert.Equal(i == j ? 1 : 0, dot, 12);
            }
        // The world accepts it as a pose, which needs det4 = +1 within 1e-10, and refuses the mirror image.
        Load(Description([Grazer(At(0, 1, 0, rotation: rotation), Lump(default))]));
        var mirrored = new Rotation4(rotation.Y, rotation.X, rotation.Z, rotation.W);
        var error = Assert.Single(LoadErrors(Description([Grazer(At(0, 1, 0, rotation: mirrored), Lump(default))])));
        Assert.Equal(LoadErrorCode.InvalidPose, error.Code);
    }

    [Fact]
    public void AMissingListAnUnknownPlaneOrAnAngleThatIsNotFiniteIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Rotation4.FromPlaneAngles(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Turns(new PlaneAngle((RotationPlane)9, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Turns(new PlaneAngle(RotationPlane.XY, double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Turns(new PlaneAngle(RotationPlane.XY, double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Turns(new PlaneAngle(RotationPlane.XY, 1), new PlaneAngle(RotationPlane.ZW, double.NegativeInfinity)));
    }

    [Fact]
    public void ABodyTurnedByTheFactoryHasItsBallsInWAsAQuarterTurnPutsThem()
    {
        // The ball 2 along local X goes to W = 2, so section I cuts it 2 from the center: sqrt(3² - 2²) = 2.23607.
        var world = GrazerScene(At(0, 4, 0, rotation: Turns(new PlaneAngle(RotationPlane.XW, Math.PI / 2))), Lump(new(2, 0, 0, 0)));

        var (shortest, longest) = DistancesFrom(world.SliceOf(GrazerId, new("I"))!, new(0, 4, 0));

        Assert.InRange(shortest, 2.23607 - .02, 2.23607 + .02);
        Assert.InRange(longest, 2.23607 - .02, 2.23607 + .02);
    }
}
