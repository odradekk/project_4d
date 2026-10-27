namespace FourD;

// The points no farther than `Tube` from a circle of radius `Major` around `Center`, in the plane of the unit
// vectors U and V. With `Major` 0 the circle shrinks to its center and the solid is a ball of radius `Tube`.
internal sealed record Round4(EntityId Entity, PartId Part, double[] Center, double[] U, double[] V, double Major, double Tube)
    : Solid4(Entity, Part, Center)
{
    // Angles at which the circle is sampled when looking for the point of it nearest to something.
    private const int CoreSamples = 128;
    private const int RefinementSteps = 60;
    private const double GoldenRatio = 0.6180339887498949;

    internal static Round4 Ball(EntityId entity, PartId part, double[] center, double radius) =>
        new(entity, part, center, [1, 0, 0, 0], [0, 1, 0, 0], 0, radius);

    internal static Round4 From(EntityId entity, Ring4 ring, Pose4 pose)
    {
        var center = World.Add(pose.Position, World.Transform(pose.Rotation, ring.Center));
        var u = World.Transform(pose.Rotation, ring.Rotation.X);
        var v = World.Transform(pose.Rotation, ring.Rotation.Y);
        return new(entity, ring.Part, Box4.ToArray(center), Box4.ToArray(u), Box4.ToArray(v), ring.MajorRadius, ring.TubeRadius);
    }

    internal override double Radius(double[] axis) =>
        Major * Math.Sqrt(Math.Pow(Box4.Dot(U, axis), 2) + Math.Pow(Box4.Dot(V, axis), 2)) + Tube;

    internal override double Extent(int coordinate) =>
        Major * Math.Sqrt(U[coordinate] * U[coordinate] + V[coordinate] * V[coordinate]) + Tube;

    internal override Solid4 Moved(double[] offset) => this with { Center = Shifted(offset) };

    // The circle's points lie within Major times the norm of the 2 x 2 map from (cos, sin) to (axis, W).
    internal override double Reach(double[] pivot, double[] axis)
    {
        var offset = Box4.Sub(Center, pivot);
        var centerReach = Math.Sqrt(Math.Pow(Box4.Dot(offset, axis), 2) + offset[3] * offset[3]);
        var spread = Math.Sqrt(
            Math.Pow(Box4.Dot(U, axis), 2) + U[3] * U[3] + Math.Pow(Box4.Dot(V, axis), 2) + V[3] * V[3]);
        return centerReach + Major * spread + Tube;
    }

    internal double[] CorePoint(double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        var point = Center.ToArray();
        for (var i = 0; i < 4; i++) point[i] += Major * (cos * U[i] + sin * V[i]);
        return point;
    }

    // The point of the circle nearest to `point`.
    internal double[] NearestCore(double[] point)
    {
        var offset = Box4.Sub(point, Center);
        var a = Box4.Dot(offset, U);
        var b = Box4.Dot(offset, V);
        var across = Math.Sqrt(a * a + b * b);
        var (cos, sin) = across > 0 ? (a / across, b / across) : (1d, 0d);
        return Center.Select((value, i) => value + Major * (cos * U[i] + sin * V[i])).ToArray();
    }

    internal double CoreDistance(double[] point) => CoreDistance(point[0], point[1], point[2], point[3]);

    internal double CoreDistance(double x, double y, double z, double w)
    {
        double ox = x - Center[0], oy = y - Center[1], oz = z - Center[2], ow = w - Center[3];
        var a = ox * U[0] + oy * U[1] + oz * U[2] + ow * U[3];
        var b = ox * V[0] + oy * V[1] + oz * V[2] + ow * V[3];
        // What is left of the offset once its parts along the circle's plane are taken off.
        double ax = ox - a * U[0] - b * V[0], ay = oy - a * U[1] - b * V[1];
        double az = oz - a * U[2] - b * V[2], aw = ow - a * U[3] - b * V[3];
        var radial = Math.Sqrt(a * a + b * b) - Major;
        return Math.Sqrt(radial * radial + ax * ax + ay * ay + az * az + aw * aw);
    }

    // The least value of `distance` over the points of the circle, and a point where it is reached. `distance`
    // must change by no more than the distance between two points, as a distance to a set does.
    internal (double Value, double[] Point) MinOverCore(Func<double[], double> distance)
    {
        if (Major == 0) return (distance(Center), Center);
        var step = 2 * Math.PI / CoreSamples;
        var values = Enumerable.Range(0, CoreSamples).Select(i => distance(CorePoint(i * step))).ToArray();
        var lowest = Array.IndexOf(values, values.Min());
        var (best, angle) = (values[lowest], lowest * step);

        // Between two samples the value can dip below them by at most this much, so only a sample that is
        // a dip itself and within that margin of the best can hide a lower value.
        var margin = Major * step / 2;
        for (var i = 0; i < CoreSamples; i++)
        {
            var isDip = values[i] <= values[(i + CoreSamples - 1) % CoreSamples] && values[i] < values[(i + 1) % CoreSamples];
            if (i != lowest && !(isDip && values[i] - margin < best)) continue;
            var (value, found) = Refine(distance, (i - 1) * step, (i + 1) * step);
            if (value < best) (best, angle) = (value, found);
        }
        return (best, CorePoint(angle));
    }

    // Golden-section search for the lowest value between two angles that bound a single dip.
    private (double Value, double Angle) Refine(Func<double[], double> distance, double low, double high)
    {
        var left = high - GoldenRatio * (high - low);
        var right = low + GoldenRatio * (high - low);
        var leftValue = distance(CorePoint(left));
        var rightValue = distance(CorePoint(right));
        for (var i = 0; i < RefinementSteps; i++)
            if (leftValue < rightValue)
            {
                high = right;
                (right, rightValue) = (left, leftValue);
                left = high - GoldenRatio * (high - low);
                leftValue = distance(CorePoint(left));
            }
            else
            {
                low = left;
                (left, leftValue) = (right, rightValue);
                right = low + GoldenRatio * (high - low);
                rightValue = distance(CorePoint(right));
            }
        return leftValue < rightValue ? (leftValue, left) : (rightValue, right);
    }
}
