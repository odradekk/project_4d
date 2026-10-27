namespace FourD;

// Decision note: 2026-10-26-device-translation

public sealed record DeviceDefinition(EntityId Id, Pose4 Pose, Source Source, LoftShape Shape, DeviceMotion Motion)
    : EntityDefinition(Id, Pose, Source);

public sealed record LoftShape(IReadOnlyList<LoftSegment> Segments);
public sealed record LoftSegment(IReadOnlyList<LoftKey> Keys);
public sealed record LoftKey(double W, IReadOnlyList<ConvexPart> Parts);
public sealed record ConvexPart(PartId Id, IReadOnlyList<Vec3> Vertices, IReadOnlyList<ConvexFace> Faces);
public sealed record ConvexFace(IReadOnlyList<int> Vertices, FaceUv Uv);

public abstract record DeviceMotion;
public sealed record DeviceRotation(RotationPlane Plane, Vec4 Pivot, Interval Limits, double Initial) : DeviceMotion;
public sealed record DeviceTranslation(Axis3 Axis, Interval Limits, double Initial) : DeviceMotion;

public abstract record DriveDeviceResult
{
    public sealed record Driven(double Position, IReadOnlyList<Contact> Contacts) : DriveDeviceResult;
    public sealed record Rejected(Failure Failure) : DriveDeviceResult;
}

public sealed record DeviceMotionStarted(EntityId Entity, double From, double Target) : SpaceEvent;
public sealed record DeviceMotionEnded(EntityId Entity, double Position) : SpaceEvent;
