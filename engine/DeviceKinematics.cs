namespace FourD;

// The pose of a device's own coordinates for a position along its motion.
internal static class DeviceKinematics
{
    internal static Interval Limits(DeviceMotion motion) => motion switch
    {
        DeviceRotation rotation => rotation.Limits,
        DeviceTranslation translation => translation.Limits,
        _ => throw new ArgumentException("Unknown device motion.", nameof(motion))
    };

    internal static double Initial(DeviceMotion motion) => motion switch
    {
        DeviceRotation rotation => rotation.Initial,
        DeviceTranslation translation => translation.Initial,
        _ => throw new ArgumentException("Unknown device motion.", nameof(motion))
    };

    // A translation moves the device along the world axis that its track lies on, the same axis a slide is checked
    // along; a rotation turns it about the pivot, a point of its own coordinates, in a plane of its own coordinates.
    internal static Pose4 PoseAt(DeviceDefinition device, double position)
    {
        var pose = device.Pose;
        if (device.Motion is DeviceTranslation)
        {
            var (axis, sign) = Track(device);
            var offset = new double[4];
            offset[axis] = sign * position;
            return pose with { Position = World.Add(pose.Position, Box4.ToVec4(offset)) };
        }
        var rotation = (DeviceRotation)device.Motion;
        var (first, second) = Axes(rotation.Plane);
        // A half turn uses the exact cosine and sine, as in a turn of a turnable object.
        var halfTurn = position == Math.PI || position == -Math.PI;
        var (cos, sin) = halfTurn ? (-1d, 0d) : (Math.Cos(position), Math.Sin(position));

        // The turn maps the local axis `first` to cos·first + sin·second, and `second` to -sin·first + cos·second.
        double[] Turned(double[] vector)
        {
            var result = vector.ToArray();
            result[first] = cos * vector[first] - sin * vector[second];
            result[second] = sin * vector[first] + cos * vector[second];
            return result;
        }
        Vec4 Column(int axis)
        {
            var unit = new double[4];
            unit[axis] = 1;
            return World.Transform(pose.Rotation, Box4.ToVec4(Turned(unit)));
        }
        var pivot = Box4.ToArray(rotation.Pivot);
        var shift = World.Transform(pose.Rotation, Box4.ToVec4(Box4.Sub(pivot, Turned(pivot))));
        return new(World.Add(pose.Position, shift), new(Column(0), Column(1), Column(2), Column(3)));
    }

    // How far the farthest vertex of the pieces lies from the pivot in the plane of the rotation.
    internal static double Reach(IEnumerable<LoftPiece> pieces, DeviceRotation rotation)
    {
        var (first, second) = Axes(rotation.Plane);
        var pivot = Box4.ToArray(rotation.Pivot);
        return pieces.SelectMany(piece => piece.Vertices)
            .Max(vertex => Math.Sqrt(Math.Pow(vertex[first] - pivot[first], 2) + Math.Pow(vertex[second] - pivot[second], 2)));
    }

    // The coordinate axes of a plane, in the order that a positive angle turns from the first to the second.
    internal static (int First, int Second) Axes(RotationPlane plane) => plane switch
    {
        RotationPlane.XY => (0, 1),
        RotationPlane.XZ => (0, 2),
        RotationPlane.XW => (0, 3),
        RotationPlane.YZ => (1, 2),
        RotationPlane.YW => (1, 3),
        RotationPlane.ZW => (2, 3),
        _ => throw new ArgumentException("Unknown rotation plane.", nameof(plane))
    };

    // The world axis (0 for X, 1 for Y, 2 for Z) that a translation runs along, and whether it runs toward its
    // negative end. Validation has seen to it that the track lies on a world axis.
    internal static (int Axis, double Sign) Track(DeviceDefinition device)
    {
        var track = Box4.ToArray(World.LocalAxis(device.Pose.Rotation, ((DeviceTranslation)device.Motion).Axis));
        var axis = Enumerable.Range(0, 3).MaxBy(i => Math.Abs(track[i]));
        return (axis, Math.Sign(track[axis]));
    }
}
