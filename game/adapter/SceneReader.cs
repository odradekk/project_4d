using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

internal sealed partial class SceneReader(Region3D region)
{
    internal readonly List<LoadError> Errors = [];
    internal readonly Dictionary<(EntityId, PartId), Material> Materials = [];
    internal readonly List<CreatureSpawn3D> Spawns = [];
    internal readonly Dictionary<EntityId, Label3D[]> Lettering = [];
    internal readonly List<SeamMark> Seams = [];

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
                .Select(e => e with { Source = Locate(e.Source) }));
        else if (result is LoadResult.Loaded loaded) TryOpenShapes(loaded.World);
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
        var seams = new List<SeamDefinition>();
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
            else if (node is TurnableObject3D turnable)
                ReadTurnable(turnable, entities);
            else if (node is CreatureSpawn3D spawn)
            {
                var section = Ancestor<Section3D>(spawn);
                if (section is null) Error(spawn, "EntityId", "A spawn must belong to a Section3D.");
                else entities.Add(Creature(spawn, section));
                if (spawn.CharacterScene is null) Error(spawn, "CharacterScene", "Assign a character scene.");
                Spawns.Add(spawn);
            }
            else if (node is SphereItem3D ball)
                ReadBall(ball, entities);
            else if (node is HiddenRingItem3D nut)
                ReadRingItem(nut, entities);
            else if (node is LoftDevice3D device)
                ReadDevice(device, entities);
            else if (node is Grazer3D grazer)
                entities.Add(ReadGrazer(grazer));
            else if (node is Seam3D seam)
                seams.Add(ReadSeam(seam));
            else if (node is BoxPart3D part && part.GetParent() is not BuildingElement3D
                && part.GetParent() is not FixedObject3D && part.GetParent() is not TurnableObject3D
                && part.GetParent() is not HiddenRingItem3D)
                Error(part, "PartId", "A BoxPart3D must be a direct child of its entity.");
        }
        foreach (var building in nodes.OfType<Building3D>()) MapRange(building, "BuildingId");
        return new(sections.Select(s => new SectionDefinition(new(s.SectionId), s.W, At(s, "W"))).ToArray(),
            nodes.OfType<Building3D>().Select(b => new BuildingDefinition(new(b.BuildingId), new(b.WMin, b.WMax), At(b, "BuildingId"))).ToArray(),
            entities, seams, [], ReadOperations(nodes));
    }

    private SeamDefinition ReadSeam(Seam3D seam)
    {
        Map(seam, "SeamId", "Id", seam, "SeamId");
        Map(seam, "SeamId", "Position", seam, "Transform");
        Map(seam, "SeamId", "HorizontalNormal", seam, "HorizontalNormal");
        Map(seam, "SeamId", "Sections", seam, "Sections");
        var position = RelativeTransform(seam, region).Origin;
        Seams.Add(new(seam.SeamId, position, seam.Sections, seam.Mirrored, (float)seam.VisualHeight));
        return new(new(seam.SeamId), V(position), V(seam.HorizontalNormal),
            seam.Sections.Select(id => new SectionId(id)).ToArray(), seam.Mirrored, seam.Open, seam.Severed, At(seam, "SeamId"));
    }

    private void ReadTurnable(TurnableObject3D turnable, List<EntityDefinition> entities)
    {
        var section = Ancestor<Section3D>(turnable);
        if (section is null)
        {
            Error(turnable, "EntityId", "A turnable object must belong to a Section3D.");
            return;
        }
        var interaction = turnable.Interaction;
        if (interaction is null)
        {
            Error(turnable, "Interaction", "Assign an interaction resource.");
            return;
        }
        var id = new EntityId(turnable.EntityId);
        Map(turnable, "EntityId", "Section", section, "SectionId");
        Map(turnable, "EntityId", "Building", turnable, "BuildingId");
        Map(turnable, "EntityId", "Pose", turnable, "Transform");
        Map(turnable, "EntityId", "Pose.Position.W", section, "W");
        Map(turnable, "EntityId", "Interaction", turnable, "Interaction");
        var properties = Properties(interaction);
        Lettering[id] = turnable.GetChildren().OfType<Label3D>().ToArray();
        entities.Add(new TurnableDefinition(id, Pose(turnable, section.W), At(turnable, "EntityId"), new(section.SectionId),
            new(turnable.BuildingId), Shape(turnable, id), properties));
    }

    // The spawn's direction is the creature's ordinary yaw, which the host passes on every frame;
    // its four-dimensional rotation starts as none.
    private Creature3Definition Creature(CreatureSpawn3D spawn, Section3D section)
    {
        Map(spawn, "EntityId", "Pose", spawn, "Transform");
        Map(spawn, "EntityId", "Section", section, "SectionId");
        Map(spawn, "EntityId", "Pose.Position.W", section, "W");
        Map(spawn, "EntityId", "PassageRadius", spawn, "PassageRadius");
        Map(spawn, "EntityId", "PassageHeight", spawn, "PassageHeight");
        var position = RelativeTransform(spawn, region).Origin;
        var pose = new Pose4(new(Typed(position.X), Typed(position.Y), Typed(position.Z), section.W), Identity);
        return new(new(spawn.EntityId), pose, At(spawn, "EntityId"), new(section.SectionId),
            spawn.PassageRadius, spawn.PassageHeight, spawn.CanUseSeamsWithoutPrism);
    }

    // `path` is where the assembly sits in the engine's definition of the entity, with a trailing dot, such as "Shape.Body.".
    private BoxAssembly Shape(Node3D entity, EntityId id, string path = "")
    {
        var result = new List<BoxPart>();
        foreach (var part in entity.GetChildren().OfType<BoxPart3D>())
        {
            var prefix = $"{path}Parts[{result.Count}]";
            Map(entity, "EntityId", prefix, part, "Mesh");
            Map(entity, "EntityId", prefix + ".Id", part, "PartId");
            Map(entity, "EntityId", prefix + ".Center", part, "Transform");
            Map(entity, "EntityId", prefix + ".Rotation", part, "Transform");
            Map(entity, "EntityId", prefix + ".HalfSize", part, "Mesh");
            Map(entity, "EntityId", prefix + ".FaceUvs", part, "FaceUvs");
            if (part.Mesh is not BoxMesh box)
            {
                Error(part, "Mesh", "Assign a BoxMesh.");
                unavailableParts.Add(At(entity, $"EntityId.{path}Parts[{result.Count}]"));
                result.Add(null!);
                continue;
            }
            var uv = new List<FaceUv>();
            foreach (var face in part.FaceUvs)
                if (face is null) Error(part, "FaceUvs", "A face UV resource is missing.");
                else uv.Add(Uv(face));
            result.Add(new(new(part.PartId), V(part.Position), V(box.Size / 2), Rotation(part.Basis), uv));
            Materials[(id, new(part.PartId))] = part.MaterialOverride ?? box.Material ?? new StandardMaterial3D();
        }
        return new(result);
    }

    private Pose4 Pose(Node3D node, double w) => PoseOf(RelativeTransform(node, region), w);

    internal static Pose4 PoseOf(Transform3D transform, double w)
    {
        var r = Rotation(transform.Basis);
        return new(new(Typed(transform.Origin.X), Typed(transform.Origin.Y), Typed(transform.Origin.Z), w),
            new(new(r.X.X, r.X.Y, r.X.Z, 0), new(r.Y.X, r.Y.Y, r.Y.Z, 0), new(r.Z.X, r.Z.Y, r.Z.Z, 0), new(0, 0, 0, 1)));
    }

    // Normalize the host's float quaternion in double precision before crossing the engine boundary.
    private static Rotation3 Rotation(Basis basis)
    {
        if (!basis.IsFinite() || !basis.IsEqualApprox(basis.Orthonormalized()))
            return new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var q = basis.GetRotationQuaternion();
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        var norm = Math.Sqrt(x * x + y * y + z * z + w * w);
        x /= norm; y /= norm; z /= norm; w /= norm;
        return new(new(1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)),
            new(2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)),
            new(2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)));
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
    private static Vec3 V(Vector3 v) => new(Typed(v.X), Typed(v.Y), Typed(v.Z));

    // Godot stores the decimal an author types as the nearest float, such as 0.46f = 0.4600000083446503. The engine checks
    // sizes and reach to 1e-10 m, so positions, sizes and the hinge go to it as the decimal: the shortest text that reads
    // as the same float.
    private static double Typed(float value) => double.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
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
