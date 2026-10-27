namespace FourD;

internal static class WorldInterference
{
    internal static IEnumerable<LoadError> Check(WorldDescription description, IReadOnlyList<LoadError> existing)
    {
        var valid = description.Entities?
            .Where(entity => entity is not null && !existing.Any(error => error.Entity == entity.Id))
            .ToArray() ?? [];
        foreach (var item in valid.Where(DynamicShapes.IsDynamic))
        {
            var contacts = TurnContacts(item, valid, description.Sections);
            if (contacts.Count > 0)
                yield return new(LoadErrorCode.BuildingInterference,
                    item.Source with { Property = $"{item.Source.Property}.Building" }, item.Id, null, null,
                    "Turned object intersects another building.", World.DistinctContacts(contacts));
        }
    }

    // Where the item, turned a quarter turn either way, cuts into the solids of other buildings.
    private static List<Contact> TurnContacts(EntityDefinition item, EntityDefinition[] valid, IReadOnlyList<SectionDefinition>? sections)
    {
        var obstacles = valid.OfType<BuildingElementDefinition>()
            .Where(element => element.Building != DynamicShapes.Building(item))
            .SelectMany(Solids)
            .ToArray();
        var contacts = new List<Contact>();
        foreach (var sign in new[] { -1d, 1d })
            foreach (var pose in Sweep(item, sign))
                foreach (var moving in DynamicShapes.Solids(item, pose))
                    foreach (var obstacle in obstacles)
                        if (moving.BoundsMayOverlap(obstacle) && Collision.Touch(moving, obstacle) is { } contact)
                            contacts.Add(contact with
                            {
                                Section = sections?.FirstOrDefault(section => section is not null && section.W == contact.Point.W)?.Id
                            });
        return contacts;
    }

    private static IEnumerable<Box4> Solids(BuildingElementDefinition element)
    {
        var segments = World.RemainingSegments(element.WRange, element.Gaps);
        var solid = new StaticEntity(element.Id, element.Pose, element.Shape.Parts.ToArray(), segments, false);
        return element.Shape.Parts.SelectMany(part => segments.Select(segment => Box4.From(solid, part, segment)));
    }

    // The poses of the item turning about its hinge in the plane of its long axis and W. A ball turning about its
    // center stays where it is, so it has only its own pose.
    internal static IEnumerable<Pose4> Sweep(EntityDefinition item, double sign)
    {
        if (DynamicShapes.IsBall(item))
        {
            yield return item.Pose;
            yield break;
        }
        var axis = World.LocalAxis(item.Pose.Rotation, DynamicShapes.Interaction(item).LongAxis);
        var pivot = DynamicShapes.Hinge(item, item.Pose);
        var count = World.SamplingSteps(DynamicShapes.Solids(item, item.Pose), pivot, axis, World.QuarterTurn);
        for (var i = 0; i <= count; i++)
            yield return World.RotatedPose(item.Pose, pivot, axis, sign * World.QuarterTurn * i / count);
    }
}
