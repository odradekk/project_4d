namespace FourD;

// Decision note: 2026-10-26-prologue-greybox

public abstract record CarryResult
{
    public sealed record Changed(EntityState Item) : CarryResult;
    public sealed record Rejected(Failure Failure) : CarryResult;
}

public abstract record MoveEntityResult
{
    public sealed record Moved(Vec3 Applied, IReadOnlyList<Contact> Contacts) : MoveEntityResult;
    public sealed record Rejected(Failure Failure) : MoveEntityResult;
}

public sealed record MovementBlocked(EntityId Entity, IReadOnlyList<Contact> Contacts) : SpaceEvent;
