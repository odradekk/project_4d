namespace FourD;

public sealed partial class World
{
    /// <summary>Atomically places an unheld turnable or item at a validated full four-dimensional pose.</summary>
    public UpdateResult PlaceEntity(EntityId entity, Pose4 pose)
    {
        UpdateResult Reject(FailureCode code, string detail) => new UpdateResult.Rejected(Fail(code, detail, entity));
        if (!dynamicEntities.TryGetValue(entity, out var item))
            return Reject(KindOrUnknown(entity), "Unknown or unsupported entity.");
        if (rotations.ContainsKey(entity)) return Reject(FailureCode.Busy, "Entity is rotating.");
        if (item.Holder is not null) return Reject(FailureCode.EntityInUse, "Entity is held.");
        if (!WorldValidation.IsValidPose(pose)) return Reject(FailureCode.InvalidInput, "Invalid pose.");

        var proposed = item with { Pose = pose };
        // A ball has no section to lie in or stand up from, so any pose is open to it.
        if (!DynamicShapes.IsBall(item.Definition))
        {
            var origin = item.Origin ?? item.Section!.Value;
            var section = sections.Single(candidate => candidate.Id == origin);
            var flat = WorldValidation.IsFlat(pose.Rotation);
            if (flat && pose.Position.W != section.W)
                return Reject(FailureCode.WrongSection, "Flat object must return to its origin section.");
            var unchanged = pose.Rotation == item.Pose.Rotation && pose.Position.W == item.Pose.Position.W;
            proposed = item with
            {
                Pose = pose,
                Section = flat ? origin : null,
                Origin = flat ? null : origin,
                Upright = unchanged ? item.Upright : null
            };
            if (!flat && Slice(proposed, section.W, bodyOnly: true) is null)
                return Reject(FailureCode.NoSlice, "Object must retain a slice in its origin section.");
        }

        if (PoseFailure(entity, proposed) is { } failure) return new UpdateResult.Rejected(failure);

        dynamicEntities[entity] = proposed;
        revision++;
        return new UpdateResult.Updated();
    }

    // Why an item cannot be in the world with the pose, section and origin of `proposed`, or null when it can. These
    // are the checks of PlaceEntity, and also those of an item that comes out of a backpack.
    private Failure? PoseFailure(EntityId entity, Dynamic proposed)
    {
        var definition = proposed.Definition;
        var pose = proposed.Pose;
        var rings = DynamicShapes.Rounds(definition, pose).Where(round => round.Major > 0);
        if (proposed.Origin is null && rings.Any(ring => sections.Any(section => ring.TooCloseToSection(section.W))))
            return Fail(FailureCode.InvalidInput, "Hidden ring is too close to a section.", entity);

        var boxes = DynamicShapes.Boxes(definition, pose);
        if (boxes.Any(box => !box.Representable()))
            return Fail(FailureCode.InvalidInput, "Pose cannot represent nondegenerate box geometry.", entity);
        var longAxis = LocalAxis(pose.Rotation, DynamicShapes.Interaction(definition).LongAxis);
        if (!DynamicShapes.IsBall(definition) && Math.Abs(Math.Abs(longAxis.W) - 1) <= RoundingMargin && boxes.Any(EndsNearSection))
            return Fail(FailureCode.InvalidInput, "Upright part end is too close to a section.", entity);
        var solids = DynamicShapes.Solids(definition, pose);
        if (LeavesBuilding(proposed, solids)) return Fail(FailureCode.OutOfRange, "Object extends outside its building.", entity);
        if (IntersectsReservation(solids, entity))
            return Fail(FailureCode.TranslationBlocked, "Target intersects a reserved rotation sweep.", entity);

        var obstacles = Solids().Where(solid => solid.Entity != entity).ToArray();
        var contacts = solids
            .SelectMany(moving => obstacles.Where(moving.BoundsMayOverlap).Select(obstacle => Collision.Touch(moving, obstacle)).OfType<Contact>())
            .ToList();
        return contacts.Count > 0 ? Fail(FailureCode.TranslationBlocked, "Target pose is occupied.", entity, contacts.AsReadOnly()) : null;
    }

    // Whether any of the solids reaches out of the W range of the building the entity is in.
    private bool LeavesBuilding(Dynamic item, IEnumerable<Solid4> solids)
    {
        var range = buildings.Single(building => building.Id == item.Building).WRange;
        return solids.Any(solid => solid.WSpan.Min < range.Min - RoundingMargin || solid.WSpan.Max > range.Max + RoundingMargin);
    }

    // A part end within the section clearance would lose its slice to rounding error.
    private bool EndsNearSection(Box4 box)
    {
        var span = box.WSpan;
        return sections.Any(section =>
            Math.Abs(section.W - span.Min) < SectionClearance || Math.Abs(section.W - span.Max) < SectionClearance);
    }
}
