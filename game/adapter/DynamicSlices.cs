using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Keeps the slices of the movable entities in every section world equal to the engine's, and puts an object's
/// lettering on it while it lies flat. An entity's slices are rebuilt only when the entity has changed.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
/// <param name="movable">The entities that move, which the engine gives new slices each time they change.</param>
/// <param name="lettering">The Label3D lettering authored on some of them.</param>
internal sealed class DynamicSlices(World world, IReadOnlyList<SectionScene> scenes,
    IReadOnlyDictionary<(EntityId, PartId), Material> materials, IReadOnlyCollection<EntityId> movable,
    IReadOnlyDictionary<EntityId, Label3D[]> lettering)
{
    // Render layers: 1 is the main view and 2 the lens, as set on the slice meshes.
    private const uint MainAndLens = 3;

    private readonly Dictionary<EntityId, (Pose4 Pose, SectionId? Section, EntityId? Holder, bool Rotating)> seen = [];
    private readonly Dictionary<(SectionId, EntityId), Node3D> nodes = [];

    internal IReadOnlyCollection<EntityId> Entities => movable;

    /// <summary>The convex hull of an entity's slice in a section, where the slice is now; null when it has none there.</summary>
    internal Shape3D? HullOf(SectionId section, EntityId entity) =>
        nodes.TryGetValue((section, entity), out var node) ? node.GetNode<MeshInstance3D>("Surface").Mesh.CreateConvexShape() : null;

    internal void Refresh()
    {
        var changed = new List<EntityState>();
        foreach (var id in movable)
        {
            var state = ((EntityReadResult.Found)world.ReadEntity(id)).Entity;
            var now = (state.Pose, state.Section, state.Holder, state.Rotating);
            if (seen.TryGetValue(id, out var before) && before == now) continue;
            seen[id] = now;
            changed.Add(state);
        }
        if (changed.Count == 0) return;
        foreach (var scene in scenes)
        {
            var geometry = (SectionGeometryResult.Geometry)world.GetSectionGeometry(scene.Id);
            foreach (var state in changed)
                Rebuild(scene, state, geometry.Entities.FirstOrDefault(slice => slice.Entity == state.Id));
        }
    }

    private void Rebuild(SectionScene scene, EntityState state, SliceGeometry? slice)
    {
        if (nodes.Remove((scene.Id, state.Id), out var old)) old.Free();
        if (slice is null) return;
        // A held slice moves with the hand, and must not push the player who holds it.
        var body = state.Holder is null ? new EntityBody3D { Name = "Collision", Entity = state.Id.Value } : null;
        var node = SliceMesh.Entity(slice, materials, scene.MaskIds, body);
        if (state.Section == scene.Id && !state.Rotating) AddLettering(node, state);
        scene.Dynamic.AddChild(node);
        nodes.Add((scene.Id, state.Id), node);
    }

    // The engine's pose gives the lettering its place; a flip turns the pose into a mirror image, and the text reads backwards.
    private void AddLettering(Node3D node, EntityState state)
    {
        if (!lettering.TryGetValue(state.Id, out var labels)) return;
        var rotation = state.Pose.Rotation;
        var pose = new Transform3D(
            new Basis(Axis(rotation.X), Axis(rotation.Y), Axis(rotation.Z)),
            new Vector3((float)state.Pose.Position.X, (float)state.Pose.Position.Y, (float)state.Pose.Position.Z));
        foreach (var label in labels)
        {
            var copy = (Label3D)label.Duplicate();
            copy.Visible = true;
            copy.Layers = MainAndLens;
            copy.Transform = pose * label.Transform;
            node.AddChild(copy);
        }
    }

    private static Vector3 Axis(Vec4 axis) => new((float)axis.X, (float)axis.Y, (float)axis.Z);
}
