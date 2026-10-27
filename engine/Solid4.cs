namespace FourD;

// A convex or tubular piece of a body, used for collision and support. Boxes and rounds are the two kinds:
// a round is a point or a circle thickened by a radius, so a ball is a round whose circle has no extent.
internal abstract record Solid4(EntityId Entity, PartId Part, double[] Center)
{
    // Half the extent of the solid along a unit vector.
    internal abstract double Radius(double[] axis);

    // Half the extent of the axis-aligned bounds along a coordinate axis.
    internal abstract double Extent(int coordinate);

    // A bound on the distance of any point of the solid from `pivot`, measured in the plane of `axis` and W.
    internal abstract double Reach(double[] pivot, double[] axis);

    internal Solid4 Moved(double[] offset) => this with { Center = Center.Select((value, i) => value + offset[i]).ToArray() };

    internal Interval WSpan
    {
        get
        {
            var extent = Extent(3);
            return new(Center[3] - extent, Center[3] + extent);
        }
    }

    // Cheap rejection on the axis-aligned bounds: false as soon as, on some coordinate axis, they overlap by no more
    // than the contact tolerance. Surfaces that merely touch count as not overlapping.
    internal bool BoundsMayOverlap(Solid4 other) =>
        Enumerable.Range(0, 4).All(i => Math.Abs(Center[i] - other.Center[i]) < Extent(i) + other.Extent(i) - World.ContactTolerance);
}
