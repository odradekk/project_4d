using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// One engine creature, seen from the character that stands for it: where the engine has it, its handedness, its
/// seam crossings, and the slice queries it looks at things with. The character reports its position here every
/// physics frame.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
/// <param name="player">The creature the engine knows as the player, whose section decides <see cref="WithPlayer"/>.</param>
/// <param name="sectionNode">The node that holds the characters of a section, in that section's physics world.</param>
internal sealed class CreatureLink(World world, string id, string player, Func<string, Node> sectionNode)
{
    // Half the W thickness of an ordinary object, docs/frame/principles.md. A box that tests a creature's path has it.
    internal const double Epsilon = .006;

    internal string Section => State(id).Section!.Value.Value;
    internal Handedness Handedness => State(id).Handedness;
    internal bool WithPlayer => State(id).Section == State(player).Section;

    internal void Sync(Vector3 position, float yaw) => world.SynchronizeActor(new EntityId(id), new(position.X, position.Y, position.Z), yaw);

    /// <summary>
    /// Takes the character through a seam into the target section, and puts it where the engine has it afterwards.
    /// Null when it crossed, otherwise why the engine refused. A creature crosses without a prism if its spawn allows it.
    /// </summary>
    internal FailureCode? Cross(Node3D body, string seam, string target)
    {
        var request = new CrossSeamRequest(new EntityId(id), new SeamId(seam), new SectionId(target), HasPrism: false, FreeBackpackSlots: 0);
        if (world.CrossSeam(request) is CrossSeamResult.Rejected rejected) return rejected.Failure.Code;
        body.Reparent(sectionNode(target), false);
        body.Position = V(State(id).Pose.Position);
        return null;
    }

    /// <summary>The bounds of an entity's slice, or of one part of it, in the creature's section; null when it has none there.</summary>
    internal Aabb? SliceBounds(string entity, string? part = null)
    {
        var result = world.GetSliceBounds(new EntityId(entity), new SectionId(Section), part is null ? null : new PartId(part));
        return result is SliceBoundsResult.Bounds bounds ? new Aabb(V(bounds.Min), V(bounds.Max) - V(bounds.Min)) : null;
    }

    /// <summary>The axis the plane of two local axes of a part of an entity has in the creature's section; null when there is none.</summary>
    internal Vector3? SlicePlaneNormal(string entity, string part, Axis3 first, Axis3 second)
    {
        var result = world.GetSlicePlaneNormal(new EntityId(entity), new SectionId(Section), first, second, new PartId(part));
        return result is SliceDirectionResult.Direction direction ? V(direction.UnitDirection) : null;
    }

    /// <summary>
    /// Whether nothing solid is in the box at <paramref name="center"/>, turned by <paramref name="yaw"/>, in the creature's
    /// section, not counting the entities in <paramref name="ignored"/>.
    /// </summary>
    internal bool IsClear(Vector3 center, float yaw, Vector3 halfSize, IEnumerable<string> ignored)
    {
        var w = State(id).Pose.Position.W;
        var pose = SceneReader.PoseOf(new Transform3D(new Basis(Vector3.Up, yaw), center), w);
        var box = new OccupancyBox(pose, new(halfSize.X, halfSize.Y, halfSize.Z, Epsilon));
        var excluded = ignored.Distinct().Select(entity => new EntityId(entity)).ToArray();
        return world.QueryOccupancy(box, excluded) is OccupancyResult.Occupancy { Hits.Count: 0 };
    }

    private EntityState State(string entity) => ((EntityReadResult.Found)world.ReadEntity(new EntityId(entity))).Entity;

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vector3 V(Vec4 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
