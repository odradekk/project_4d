namespace FourD;
public sealed record TurnableDefinition(EntityId Id, Pose4 Pose, Source Source, SectionId Section, BuildingId Building, BoxAssembly Shape, TurnableProperties Interaction) : EntityDefinition(Id, Pose, Source);
public sealed record Creature3Definition(EntityId Id, Pose4 Pose, Source Source, SectionId Section, double PassageRadius, double PassageHeight, bool CanUseSeamsWithoutPrism) : EntityDefinition(Id, Pose, Source);
public sealed record TurnableProperties(bool Holdable, bool Packable, double HorizontalRadius, Axis3 LongAxis, Vec3 Hinge, double Reach);
public enum Handedness { Normal, Mirrored, NotComparable }
public sealed record EntityState(EntityId Id, EntityDefinition Definition, Pose4 Pose, SectionId? Section, double? ActorYaw, SectionId? UprightOrigin, EntityId? Holder, IReadOnlyList<EntityId> CarriedItems, double? DevicePosition, bool Rotating, Handedness Handedness);
public abstract record AddEntityResult { public sealed record Added(EntityState Entity) : AddEntityResult; public sealed record Rejected(Failure Failure) : AddEntityResult; }
public abstract record RemoveEntityResult { public sealed record Removed(EntityId Entity) : RemoveEntityResult; public sealed record Rejected(Failure Failure) : RemoveEntityResult; }
public abstract record EntityReadResult { public sealed record Found(EntityState Entity) : EntityReadResult; public sealed record Missing(EntityId Entity) : EntityReadResult; }
