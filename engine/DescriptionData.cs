namespace FourD;

public readonly record struct SharedValueId(string Value);
public sealed record SeamDefinition(
    SeamId Id, Vec3 Position, Vec3 HorizontalNormal,
    IReadOnlyList<SectionId> Sections, bool Mirrored, bool Open, bool Severed, Source Source);
public sealed record SharedValueDefinition(SharedValueId Id, Interval Range, double InitialValue, Source Source);
public sealed record ExpectedOperation(OperationId Id, Source Source, ExpectedSetup Setup, IReadOnlyList<ExpectedStep> Steps);
public sealed record ExpectedSetup(
    IReadOnlyList<EntityDefinition> EntityOverrides, IReadOnlyList<SeamOverride> Seams,
    IReadOnlyList<SharedValueOverride> SharedValues, IReadOnlyList<CarrySetup> Carry);
public sealed record SeamOverride(SeamId Id, bool Open, bool Severed);
public sealed record SharedValueOverride(SharedValueId Id, double Value);
public enum CarryLocation { Hand, Backpack }
public sealed record CarrySetup(EntityId Actor, EntityId Item, CarryLocation Location);
public abstract record ExpectedStep;
public sealed record ExpectedRotation(RotationRequest Request, FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedMove(MoveRequest Request, Vec3 ExpectedApplied, FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedDevice(EntityId Device, double Target, double ExpectedPosition, FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedCrossing(CrossSeamRequest Request, FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedAdvance(double Delta) : ExpectedStep;
public abstract record RotationRequest(EntityId Actor, EntityId Entity);
public sealed record StandUp(EntityId Actor, EntityId Entity, SectionId Target) : RotationRequest(Actor, Entity);
public sealed record LayDown(EntityId Actor, EntityId Entity) : RotationRequest(Actor, Entity);
public sealed record Flip(EntityId Actor, EntityId Entity) : RotationRequest(Actor, Entity);
public sealed record MoveRequest(EntityId Entity, Vec3 Delta);
public sealed record CrossSeamRequest(EntityId Actor, SeamId Seam, SectionId Target, bool HasPrism, int FreeBackpackSlots);
