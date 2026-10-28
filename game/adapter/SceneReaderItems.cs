using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

// The four-dimensional items and the devices of a region.
// Decision note: 2026-10-26-region-scene-authoring
internal sealed partial class SceneReader
{
    // Engine errors under a prefix of an entity's definition belong to one exported property of its node.
    private readonly List<(Source Prefix, Source Target)> prefixes = [];

    private Source Locate(Source source)
    {
        if (sources.TryGetValue(source, out var exact)) return exact;
        foreach (var (prefix, target) in prefixes)
            if (source.NodePath == prefix.NodePath && source.Property.StartsWith(prefix.Property, StringComparison.Ordinal))
                return target;
        return source;
    }

    private void MapPrefix(Node node, string root, string suffix, Node target, string property) =>
        prefixes.Add((At(node, $"{root}.{suffix}"), At(target, property)));

    private void ReadBall(SphereItem3D ball, List<EntityDefinition> entities)
    {
        if (ball.Interaction is null)
        {
            Error(ball, "Interaction", "Assign an interaction resource.");
            return;
        }
        var id = new EntityId(ball.EntityId);
        MapItem(ball);
        Map(ball, "EntityId", "Shape.Part", ball, "PartId");
        Map(ball, "EntityId", "Shape.Radius", ball, "Radius");
        Materials[(id, new(ball.PartId))] = ball.Material ?? new StandardMaterial3D();
        entities.Add(new ItemDefinition(id, Pose(ball, ball.W, ball.Rotation4), At(ball, "EntityId"), new(ball.BuildingId),
            new Sphere4(new(ball.PartId), ball.Radius), Properties(ball.Interaction)));
    }

    private void ReadRingItem(HiddenRingItem3D nut, List<EntityDefinition> entities)
    {
        if (nut.Interaction is null || nut.Ring is null)
        {
            Error(nut, nut.Interaction is null ? "Interaction" : "Ring", "Assign the resource.");
            return;
        }
        var id = new EntityId(nut.EntityId);
        var ring = nut.Ring;
        MapItem(nut);
        MapPrefix(nut, "EntityId", "Shape.Ring", nut, "Ring");
        Materials[(id, new(ring.PartId))] = ring.Material ?? new StandardMaterial3D();
        var shape = new HiddenRingItem(Shape(nut, id, "Shape.Body."), new Ring4(new(ring.PartId), Point(ring.Center, nut, "Ring"),
            ring.Rotation4.Length == 0 ? Identity : Matrix(ring.Rotation4, nut, "Ring"), ring.MajorRadius, ring.TubeRadius));
        entities.Add(new ItemDefinition(id, Pose(nut, nut.W, nut.Rotation4), At(nut, "EntityId"), new(nut.BuildingId), shape,
            Properties(nut.Interaction)));
    }

    private void ReadDevice(LoftDevice3D device, List<EntityDefinition> entities)
    {
        if (device.Motion is null)
        {
            Error(device, "Motion", "Assign a motion resource.");
            return;
        }
        var id = new EntityId(device.EntityId);
        Map(device, "EntityId", "Pose", device, "Transform");
        Map(device, "EntityId", "Pose.Position.W", device, "W");
        MapPrefix(device, "EntityId", "Shape", device, "Segments");
        MapPrefix(device, "EntityId", "Motion", device, "Motion");
        var segments = NonNull(device.Segments, device, "Segments")
            .Select(segment => new LoftSegment(NonNull(segment.Keys, device, "Segments")
                .Select(key => new LoftKey(key.W, NonNull(key.Parts, device, "Segments").Select(part => ConvexPartOf(id, part, device)).ToArray()))
                .ToArray()))
            .ToArray();
        DeviceMotion motion = device.Motion switch
        {
            DeviceRotationResource rotation => new DeviceRotation(rotation.Plane, Point(rotation.Pivot, device, "Motion"),
                new(rotation.Min, rotation.Max), rotation.Initial),
            DeviceTranslationResource slide => new DeviceTranslation(slide.Axis, new(slide.Min, slide.Max), slide.Initial),
            _ => throw new NotSupportedException("Unknown device motion resource.")
        };
        entities.Add(new DeviceDefinition(id, Pose(device, device.W, device.Rotation4), At(device, "EntityId"), new LoftShape(segments), motion));
    }

    private ConvexPart ConvexPartOf(EntityId device, ConvexPartResource part, Node owner)
    {
        Materials.TryAdd((device, new(part.PartId)), part.Material ?? new StandardMaterial3D());
        var faces = NonNull(part.Faces, owner, "Segments")
            .Select(face => new ConvexFace(face.Vertices, face.Uv is null ? MissingUv(owner) : Uv(face.Uv)))
            .ToArray();
        return new(new(part.PartId), part.Vertices.Select(V).ToArray(), faces);
    }

    private FaceUv MissingUv(Node owner)
    {
        Error(owner, "Segments", "A face UV resource is missing.");
        return new(Axis3.X, false, new(1, 1), new(0, 0));
    }

    private void MapItem(Node3D item)
    {
        Map(item, "EntityId", "Pose", item, "Transform");
        Map(item, "EntityId", "Pose.Position.W", item, "W");
        Map(item, "EntityId", "Building", item, "BuildingId");
        Map(item, "EntityId", "Interaction", item, "Interaction");
    }

    private IEnumerable<T> NonNull<T>(IEnumerable<T?> items, Node node, string property) where T : class
    {
        foreach (var item in items)
            if (item is null) Error(node, property, "A resource is missing.");
            else yield return item;
    }

    private static TurnableProperties Properties(TurnableResource interaction) => new(interaction.Holdable, interaction.Packable,
        interaction.HorizontalRadius, interaction.LongAxis, V(interaction.Hinge), interaction.Reach);

    private static FaceUv Uv(FaceUvResource face) =>
        new(face.Axis, face.Positive, new(face.Scale.X, face.Scale.Y), new(face.Offset.X, face.Offset.Y));

    /// <summary>No rotation in four dimensions.</summary>
    internal static readonly Rotation4 Identity = new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));

    // The pose of a node with the depth the entity lies at. Without a Rotation4 the node's own rotation gives the pose's; with
    // one, the node must not be rotated as well, since the two would not be told apart.
    private Pose4 Pose(Node3D node, double w, double[] rotation4)
    {
        var pose = Pose(node, w);
        if (rotation4.Length == 0) return pose;
        if (!node.Transform.Basis.IsEqualApprox(Basis.Identity)) Error(node, "Rotation4", "Give a Rotation4 or rotate the node, not both.");
        return pose with { Rotation = Matrix(rotation4, node, "Rotation4") };
    }

    private Rotation4 Matrix(double[] values, Node node, string property)
    {
        if (values.Length != 16)
        {
            Error(node, property, "Give 16 numbers: the columns X, Y, Z and W, each as X, Y, Z, W.");
            return Identity;
        }
        return new(new(values[0], values[1], values[2], values[3]), new(values[4], values[5], values[6], values[7]),
            new(values[8], values[9], values[10], values[11]), new(values[12], values[13], values[14], values[15]));
    }

    private Vec4 Point(double[] values, Node node, string property)
    {
        if (values.Length == 4) return new(values[0], values[1], values[2], values[3]);
        Error(node, property, "Give a point as 4 numbers: X, Y, Z, W.");
        return default;
    }
}
