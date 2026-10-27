using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

internal sealed class SceneReader(Region3D region)
{
    internal readonly List<LoadError> Errors = [];
    internal readonly Dictionary<(EntityId, PartId), Material> Materials = [];
    internal readonly List<CreatureSpawn3D> Spawns = [];

    private readonly HashSet<Source> unavailableParts = [];
    private readonly Dictionary<Source, Source> sources = [];
    internal WorldDescription? Description { get; private set; }
    internal LoadResult Load()
    {
        Description = Read();
        var result = World.Load(Description);
        if (result is LoadResult.Invalid invalid)
            Errors.AddRange(invalid.Errors
                .Where(e => !(e.Code == LoadErrorCode.InvalidGeometry && unavailableParts.Contains(e.Source)))
                .Select(e => sources.TryGetValue(e.Source, out var source) ? e with { Source = source } : e));
        return Errors.Count > 0 ? new LoadResult.Invalid(Errors) : result;
    }

    private WorldDescription Read()
    {
        var nodes = Descendants(region).ToArray();
        foreach (var node in nodes.OfType<Node3D>().Prepend(region))
        {
            var b = node.Transform.Basis;
            if (!b.IsFinite() || !node.Position.IsFinite() ||
                !b.IsEqualApprox(b.Orthonormalized()) || b.Determinant() < 0)
                Error(node, "Transform", "Use a finite rotation and translation without scale, shear or reflection.");
        }
        var sections = nodes.OfType<Section3D>().ToArray();
        foreach (var section in sections)
        {
            Map(section, "W", "Id", section, "SectionId");
            if (!section.Transform.IsEqualApprox(Transform3D.Identity))
                Error(section, "Transform", "Section transforms must be identity; use W for section depth.");
            if (section.GetParent() != region.GetNodeOrNull("Sections"))
                Error(section, "SectionId", "Place sections directly under Sections.");
        }
        var entities = new List<EntityDefinition>();
        foreach (var node in nodes)
        {
            if (node is BuildingElement3D element)
            {
                MapRange(element, "EntityId");
                Map(element, "EntityId", "Building", element, "BuildingId");
                Map(element, "EntityId", "Pose", element, "Transform");
                foreach (var suffix in new[] { "Gaps", "Gaps.Min", "Gaps.Max" })
                    Map(element, "EntityId", suffix, element, "Gaps");
                var gaps = new List<Interval>();
                foreach (var gap in element.Gaps)
                    if (gap is null) Error(element, "Gaps", "An interval resource is missing.");
                    else gaps.Add(new(gap.Min, gap.Max));
                entities.Add(new BuildingElementDefinition(new(element.EntityId), Pose(element, 0),
                    At(element, "EntityId"), new(element.BuildingId), new(element.WMin, element.WMax), gaps,
                    Shape(element, new(element.EntityId))));
            }
            else if (node is FixedObject3D fixedObject)
            {
                var section = Ancestor<Section3D>(fixedObject);
                Map(fixedObject, "EntityId", "Pose", fixedObject, "Transform");
                if (section is not null) Map(fixedObject, "EntityId", "Pose.Position.W", section, "W");
                if (section is null) Error(fixedObject, "EntityId", "A fixed object must belong to a Section3D.");
                else entities.Add(new FixedDefinition(new(fixedObject.EntityId), Pose(fixedObject, section.W),
                    At(fixedObject, "EntityId"), new(section.SectionId), Shape(fixedObject, new(fixedObject.EntityId))));
            }
            else if (node is CreatureSpawn3D spawn)
            {
                if (Ancestor<Section3D>(spawn) is null) Error(spawn, "EntityId", "A spawn must belong to a Section3D.");
                if (spawn.CharacterScene is null) Error(spawn, "CharacterScene", "Assign a character scene.");
                Spawns.Add(spawn);
            }
            else if (node is BoxPart3D part && part.GetParent() is not BuildingElement3D && part.GetParent() is not FixedObject3D)
                Error(part, "PartId", "A BoxPart3D must be a direct child of its entity.");
        }
        foreach (var building in nodes.OfType<Building3D>()) MapRange(building, "BuildingId");
        return new(sections.Select(s => new SectionDefinition(new(s.SectionId), s.W, At(s, "W"))).ToArray(),
            nodes.OfType<Building3D>().Select(b => new BuildingDefinition(new(b.BuildingId), new(b.WMin, b.WMax), At(b, "BuildingId"))).ToArray(),
            entities, [], [], []);
    }

    private BoxAssembly Shape(Node3D entity, EntityId id)
    {
        var result = new List<BoxPart>();
        foreach (var part in entity.GetChildren().OfType<BoxPart3D>())
        {
            var prefix = $"Parts[{result.Count}]";
            Map(entity, "EntityId", prefix, part, "Mesh");
            Map(entity, "EntityId", prefix + ".Id", part, "PartId");
            Map(entity, "EntityId", prefix + ".Center", part, "Transform");
            Map(entity, "EntityId", prefix + ".Rotation", part, "Transform");
            Map(entity, "EntityId", prefix + ".HalfSize", part, "Mesh");
            Map(entity, "EntityId", prefix + ".FaceUvs", part, "FaceUvs");
            if (part.Mesh is not BoxMesh box)
            {
                Error(part, "Mesh", "Assign a BoxMesh.");
                unavailableParts.Add(At(entity, $"EntityId.Parts[{result.Count}]"));
                result.Add(null!);
                continue;
            }
            var uv = new List<FaceUv>();
            foreach (var face in part.FaceUvs)
                if (face is null) Error(part, "FaceUvs", "A face UV resource is missing.");
                else uv.Add(new(face.Axis, face.Positive, new(face.Scale.X, face.Scale.Y), new(face.Offset.X, face.Offset.Y)));
            result.Add(new(new(part.PartId), V(part.Position), V(box.Size / 2), Rotation(part.Basis), uv));
            Materials[(id, new(part.PartId))] = part.MaterialOverride ?? box.Material ?? new StandardMaterial3D();
        }
        return new(result);
    }

    private Pose4 Pose(Node3D node, double w)
    {
        var transform = RelativeTransform(node, region);
        var r = Rotation(transform.Basis);
        return new(new(transform.Origin.X, transform.Origin.Y, transform.Origin.Z, w),
            new(new(r.X.X, r.X.Y, r.X.Z, 0), new(r.Y.X, r.Y.Y, r.Y.Z, 0), new(r.Z.X, r.Z.Y, r.Z.Z, 0), new(0, 0, 0, 1)));
    }

    // Normalize the host's float quaternion in double precision before crossing the engine boundary.
    private static Rotation3 Rotation(Basis basis)
    {
        if (!basis.IsFinite() || !basis.IsEqualApprox(basis.Orthonormalized()))
            return new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var q = basis.GetRotationQuaternion();
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        var norm = Math.Sqrt(x*x + y*y + z*z + w*w);
        x /= norm; y /= norm; z /= norm; w /= norm;
        return new(new(1-2*(y*y+z*z), 2*(x*y+z*w), 2*(x*z-y*w)),
            new(2*(x*y-z*w), 1-2*(x*x+z*z), 2*(y*z+x*w)),
            new(2*(x*z+y*w), 2*(y*z-x*w), 1-2*(x*x+y*y)));
    }

    internal static Transform3D RelativeTransform(Node3D node, Region3D region)
    {
        var transform = Transform3D.Identity;
        // Authoring groups are plain Nodes; their lack of a spatial parent must not reset region coordinates.
        for (Node? current = node; current is not null && current != region; current = current.GetParent())
            if (current is Node3D spatial) transform = spatial.Transform * transform;
        return transform;
    }

    internal static T? Ancestor<T>(Node node) where T : Node
    {
        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is T found) return found;
        return null;
    }
    internal static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static Vec3 V(Vector3 v) => new(v.X, v.Y, v.Z);
    private void MapRange(Node node, string root)
    {
        Map(node, root, "WRange", node, "WMin");
        Map(node, root, "WRange.Min", node, "WMin");
        Map(node, root, "WRange.Max", node, "WMax");
    }
    private void Map(Node node, string root, string suffix, Node target, string property) =>
        sources[At(node, $"{root}.{suffix}")] = At(target, property);
    private Source At(Node node, string property) => new(region.GetPathTo(node).ToString(), property);
    private void Error(Node node, string property, string detail) => Errors.Add(new(
        LoadErrorCode.InvalidGeometry, At(node, property), null, null, null, detail, []));
}
