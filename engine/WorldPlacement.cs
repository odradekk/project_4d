namespace FourD;

public sealed partial class World
{
    /// <summary>Atomically places an unheld turnable at a validated full four-dimensional pose.</summary>
    public UpdateResult PlaceEntity(EntityId entity, Pose4 pose)
    {
        UpdateResult Reject(FailureCode code, string detail, IReadOnlyList<Contact>? contacts = null) =>
            new UpdateResult.Rejected(Fail(code, detail, entity, contacts));
        if (!dynamicEntities.TryGetValue(entity, out var item))
            return Reject(KindOrUnknown(entity), "Unknown or unsupported entity.");
        if (rotations.ContainsKey(entity)) return Reject(FailureCode.Busy, "Entity is rotating.");
        if (!WorldValidation.IsValidPose(pose)) return Reject(FailureCode.InvalidInput, "Invalid pose.");

        var origin = item.Origin ?? item.Section!.Value;
        var section = sections.Single(candidate => candidate.Id == origin);
        var flat = WorldValidation.IsFlat(pose.Rotation);
        if (flat && pose.Position.W != section.W)
            return Reject(FailureCode.WrongSection, "Flat object must return to its origin section.");
        var unchanged = pose.Rotation == item.Pose.Rotation && pose.Position.W == item.Pose.Position.W;
        var proposed = item with
        {
            Pose = pose,
            Section = flat ? origin : null,
            Origin = flat ? null : origin,
            Upright = unchanged ? item.Upright : null
        };
        if (!flat && Slice(proposed, section.W) is null)
            return Reject(FailureCode.NoSlice, "Object must retain a slice in its origin section.");

        var boxes = Box4.From(item.Definition, pose);
        if (boxes.Any(box => !box.Representable()))
            return Reject(FailureCode.InvalidInput, "Pose cannot represent nondegenerate box geometry.");
        var longAxis = LocalAxis(pose.Rotation, item.Definition.Interaction.LongAxis);
        if (Math.Abs(Math.Abs(longAxis.W) - 1) <= RoundingMargin && boxes.Any(EndsNearSection))
            return Reject(FailureCode.InvalidInput, "Upright part end is too close to a section.");
        var owner = buildings.Single(building => building.Id == item.Definition.Building);
        if (boxes.Any(box => box.WSpan.Min < owner.WRange.Min || box.WSpan.Max > owner.WRange.Max))
            return Reject(FailureCode.OutOfRange, "Object extends outside its building.");
        if (IntersectsReservation(boxes, entity))
            return Reject(FailureCode.TranslationBlocked, "Target intersects a reserved rotation sweep.");

        var obstacles = Boxes().Where(box => box.Entity != entity).ToArray();
        var contacts = boxes
            .SelectMany(moving => obstacles.Select(obstacle => Box4.Touch(moving, obstacle)).OfType<Contact>())
            .ToList();
        if (contacts.Count > 0) return Reject(FailureCode.TranslationBlocked, "Target pose is occupied.", contacts.AsReadOnly());

        dynamicEntities[entity] = proposed;
        revision++;
        return new UpdateResult.Updated();
    }

    // A part end within the section clearance would lose its slice to rounding error.
    private bool EndsNearSection(Box4 box)
    {
        var span = box.WSpan;
        return sections.Any(section =>
            Math.Abs(section.W - span.Min) < SectionClearance || Math.Abs(section.W - span.Max) < SectionClearance);
    }

    /// <summary>Reports whether a given entity has a three-dimensional slice at the section.</summary>
    public SlicePresenceResult HasSlice(EntityId entity, SectionId section)
    {
        var target = sections.FirstOrDefault(candidate => candidate.Id == section);
        if (target is null || ReadEntity(entity) is EntityReadResult.Missing)
            return new SlicePresenceResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity or section.", entity));
        var geometry = (SectionGeometryResult.Geometry)GetGeometryAtW(target.W);
        return new SlicePresenceResult.Present(geometry.Entities.Any(slice => slice.Entity == entity));
    }
}
