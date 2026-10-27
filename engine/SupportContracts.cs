namespace FourD;
public sealed record SupportContact(EntityId Support, PartId Part, Vec4 Point, Vec4 Normal);
public abstract record SupportResult { public sealed record Supported(double RestingOriginY,IReadOnlyList<SupportContact> Contacts):SupportResult; public sealed record Unsupported:SupportResult; public sealed record Rejected(Failure Failure):SupportResult; }
public abstract record SpaceEvent;
public sealed record EntityAdded(EntityId Entity):SpaceEvent;
public sealed record EntityRemoved(EntityId Entity):SpaceEvent;
public sealed record EntityFell(EntityId Entity,double FromY,double ToY):SpaceEvent;
public sealed record EventOccurrence(double Time,long Sequence,SpaceEvent Event);
public abstract record StepResult { public sealed record Advanced(double Time,IReadOnlyList<EventOccurrence> Events):StepResult; public sealed record Rejected(Failure Failure):StepResult; }
