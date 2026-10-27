namespace FourD;

public abstract record UpdateResult
{
    public sealed record Updated : UpdateResult;
    public sealed record Rejected(Failure Failure) : UpdateResult;
}
public abstract record SlicePresenceResult
{
    public sealed record Present(bool HasVolume) : SlicePresenceResult;
    public sealed record Rejected(Failure Failure) : SlicePresenceResult;
}
