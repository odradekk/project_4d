namespace FourD;

// The six planes of the world's coordinate axes in which a turn happens.
public enum RotationPlane { XY, XZ, XW, YZ, YW, ZW }

/// <summary>A turn of Angle radians in a coordinate plane, from the first axis of the plane toward the second.</summary>
public readonly record struct PlaneAngle(RotationPlane Plane, double Angle);

public readonly partial record struct Rotation4
{
    /// <summary>
    /// The rotation made by turning the identity by each of the plane angles in the order given: every turn is applied
    /// to the result of the ones before it, so [XW(a), YZ(b)] gives the matrix of YZ(b) times the matrix of XW(a). No
    /// turns give the identity, one plane may come more than once, and an angle may be any finite number of radians.
    /// A positive angle turns the first axis of the plane toward the second: XW(pi/2) takes +X to +W.
    /// ArgumentNullException for null; ArgumentOutOfRangeException for an unknown plane or an angle that is not finite.
    /// </summary>
    public static Rotation4 FromPlaneAngles(IReadOnlyList<PlaneAngle> rotations)
    {
        ArgumentNullException.ThrowIfNull(rotations);
        double[][] columns = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]];
        foreach (var (plane, angle) in rotations)
        {
            if (!double.IsFinite(angle)) throw new ArgumentOutOfRangeException(nameof(rotations), angle, "Angles must be finite.");
            var (first, second) = Axes(plane);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            foreach (var column in columns)
                (column[first], column[second]) = (cos * column[first] - sin * column[second], sin * column[first] + cos * column[second]);
        }
        return new(Box4.ToVec4(columns[0]), Box4.ToVec4(columns[1]), Box4.ToVec4(columns[2]), Box4.ToVec4(columns[3]));
    }

    // The coordinate axes of a plane (0 for X to 3 for W), in the order that a positive angle turns from the first to the second.
    internal static (int First, int Second) Axes(RotationPlane plane) => plane switch
    {
        RotationPlane.XY => (0, 1),
        RotationPlane.XZ => (0, 2),
        RotationPlane.XW => (0, 3),
        RotationPlane.YZ => (1, 2),
        RotationPlane.YW => (1, 3),
        RotationPlane.ZW => (2, 3),
        _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, "Unknown rotation plane.")
    };
}
