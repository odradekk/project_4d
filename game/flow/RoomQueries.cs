using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Adapter;
using Godot;

namespace Game.Flow;

/// <summary>What the room scene asks the engine. It reads the world through the engine's generic queries and nothing else.</summary>
internal sealed class RoomQueries(World world)
{
    internal const string PageI = "Ⅰ";
    internal const string PageII = "Ⅱ";
    internal const string PageIII = "Ⅲ";
    // Page II's depth in this region, and half the thickness of an ordinary object (docs/frame/principles.md).
    private const double PageIIDepth = .42;
    private const double Epsilon = .006;
    // An entity at rest sits at the height the engine finds for it, within this much.
    private const double RestMargin = .01;

    private readonly Dictionary<(string, string, string?), ((Pose4, SectionId?, double?, bool) State, Aabb? Bounds)> known = [];

    internal EntityState State(string entity) => ((EntityReadResult.Found)world.ReadEntity(new EntityId(entity))).Entity;

    /// <summary>Where a device is on its way: its angle for a hinge, its distance for a slide.</summary>
    internal double DevicePosition(string device) => State(device).DevicePosition!.Value;

    /// <summary>
    /// The bounds of an entity's slice in a section, or of one of its parts; null when there is none. The engine cuts the whole
    /// world to answer, which takes milliseconds, and the slice of an entity depends on nothing but its own state, so an answer
    /// is kept until the entity changes.
    /// </summary>
    internal Aabb? Bounds(string entity, string section, string? part = null)
    {
        var state = State(entity);
        var signature = (state.Pose, state.Section, state.DevicePosition, state.Rotating);
        if (known.TryGetValue((entity, section, part), out var kept) && kept.State.Equals(signature)) return kept.Bounds;
        var bounds = world.GetSliceBounds(new(entity), new(section), part is null ? null : new PartId(part)) is SliceBoundsResult.Bounds box
            ? new Aabb(V(box.Min), V(box.Max) - V(box.Min)) : (Aabb?)null;
        known[(entity, section, part)] = (signature, bounds);
        return bounds;
    }

    /// <summary>Whether the entity lies still on something that holds it up: not in a hand, not turning, and where it would come to rest.</summary>
    internal bool IsResting(string entity)
    {
        var state = State(entity);
        return state.Holder is null && !state.Rotating && world.FindSupport(new(entity)) is SupportResult.Supported support
            && Math.Abs(support.RestingOriginY - state.Pose.Position.Y) < RestMargin;
    }

    /// <summary>The entities that fill any of a box in page II, the box being given by its middle and half sizes.</summary>
    internal IReadOnlyList<string> InPageII(Vector3 center, Vector3 half)
    {
        var pose = SceneReader.PoseOf(new Transform3D(Basis.Identity, center), PageIIDepth);
        var box = new OccupancyBox(pose, new(half.X, half.Y, half.Z, Epsilon));
        return world.QueryOccupancy(box, []) is OccupancyResult.Occupancy occupancy
            ? occupancy.Hits.Select(hit => hit.Entity.Value).Distinct().ToArray() : [];
    }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
