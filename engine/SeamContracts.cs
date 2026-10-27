namespace FourD;

// Decision note: 2026-10-26-prologue-greybox

public sealed record SeamState(SeamId Id, bool Open, bool Severed);

public abstract record SeamReadResult
{
    public sealed record Found(SeamState Seam) : SeamReadResult;
    public sealed record Missing(SeamId Seam) : SeamReadResult;
}

public abstract record CrossSeamResult
{
    /// <param name="Packed">The items that went from the hand into the backpack for this crossing.</param>
    /// <param name="WedgedItem">The long item that stood up in the seam and is still held in the target section.</param>
    public sealed record Crossed(
        SectionId From, SectionId To, Handedness ActorHandedness,
        IReadOnlyList<EntityId> Packed, EntityId? WedgedItem) : CrossSeamResult;
    public sealed record Rejected(Failure Failure) : CrossSeamResult;
}

public sealed record SeamCrossed(EntityId Actor, SeamId Seam, SectionId From, SectionId To) : SpaceEvent;
public sealed record SeamSevered(SeamId Seam) : SpaceEvent;
