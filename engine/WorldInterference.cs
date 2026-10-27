namespace FourD;

internal static class WorldInterference
{
    internal static IEnumerable<LoadError> Check(WorldDescription description, IReadOnlyList<LoadError> existing)
    {
        var valid = description.Entities?.Where(e => e is not null && !existing.Any(error => error.Entity == e.Id)).ToArray() ?? [];
        foreach (var item in valid.OfType<TurnableDefinition>())
        {
            var contacts = new List<Contact>();
            foreach (var sign in new[] { -1d, 1d })
            {
                foreach (var pose in Sweep(item, sign))
                    foreach (var part in item.Shape.Parts)
                    {
                        var moving = Box4.From(item, pose, part);
                        foreach (var element in valid.OfType<BuildingElementDefinition>().Where(e => e.Building != item.Building))
                        {
                            var segments = World.RemainingSegments(element.WRange, element.Gaps);
                            var solid = new StaticEntity(element.Id, element.Pose, element.Shape.Parts.ToArray(), segments, false);
                            foreach (var other in element.Shape.Parts) foreach (var segment in segments)
                                {
                                    var obstacle = Box4.From(solid, other, segment);
                                    if (Enumerable.Range(0, 4).Any(i => Math.Abs(moving.Center[i] - obstacle.Center[i]) >= moving.Radius(Unit(i)) + obstacle.Radius(Unit(i)) - .005)) continue;
                                    if (!Box4.Overlaps(moving, obstacle, .005)) continue;
                                    var point = Box4.IntersectionPoint(moving, obstacle);
                                    if (point is null) continue;
                                    var delta = Box4.Sub(moving.Center, obstacle.Center);
                                    var normal = Box4.SeparatingAxes(moving, obstacle).MinBy(n => moving.Radius(n) + obstacle.Radius(n) - Math.Abs(Box4.Dot(n, delta)))!;
                                    if (Box4.Dot(normal, delta) < 0) normal = normal.Select(v => -v).ToArray();
                                    var section = description.Sections?.FirstOrDefault(s => s is not null && s.W == point[3])?.Id;
                                    contacts.Add(new(item.Id, part.Id, element.Id, other.Id, Box4.Vec(point), Box4.Vec(normal), section));
                                }
                        }
                    }
            }
            if (contacts.Count > 0)
                yield return new(LoadErrorCode.BuildingInterference, item.Source with { Property = $"{item.Source.Property}.Building" }, item.Id, null, null,
                    "Turned object intersects another building.", Array.AsReadOnly(contacts.GroupBy(c => (c.Obstacle, c.ObstaclePart, c.MovingPart)).Select(g => g.First()).OrderBy(c => c.Obstacle.Value, StringComparer.Ordinal).ThenBy(c => c.ObstaclePart.Value, StringComparer.Ordinal).ThenBy(c => c.MovingPart.Value, StringComparer.Ordinal).ToArray()));
        }
    }
    private static double[] Unit(int i) { var a = new double[4]; a[i] = 1; return a; }
    private static IEnumerable<Pose4> Sweep(TurnableDefinition item, double sign)
    {
        var r = item.Pose.Rotation; var axis = new[] { r.X, r.Y, r.Z }[(int)item.Interaction.LongAxis];
        var pivot = World.Add(item.Pose.Position, World.Transform(r, item.Interaction.Hinge));
        var count = World.SamplingSteps(item, item.Pose, pivot, axis, Math.PI / 2);
        for (var i = 0; i <= count; i++)
        {
            var angle = sign * Math.PI / 2 * i / count;
            Vec4 Turn(Vec4 value) => World.Rotate(value, axis, angle);
            yield return new(World.Add(pivot, Turn(World.Sub(item.Pose.Position, pivot))), new(Turn(r.X), Turn(r.Y), Turn(r.Z), Turn(r.W)));
        }
    }
}
