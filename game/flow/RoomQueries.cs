using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Adapter;
using Godot;

namespace Game.Flow;

/// <summary>What the room scene asks the engine. It reads the world through the engine's generic queries and nothing else.</summary>
internal sealed class RoomQueries(Region3D region)
{
    internal const string SectionI = "Ⅰ";
    internal const string SectionII = "Ⅱ";
    internal const string SectionIII = "Ⅲ";
    // An entity at rest sits at the height the engine finds for it, within this much.
    private const double RestMargin = .01;
    // The floor of the room is a slab under the floor plane, and a box this high in it shows whether the floor is there.
    private const float FloorProbeY = -.05f;
    private const float FloorProbeHeight = .04f;
    // The middle of a slice, as a share of its half width: a slice that hangs over the edge of a hole is not over the hole.
    private const float MiddleShare = .7f;

    private readonly World world = region.World!;

    internal EntityState State(string entity) => ((EntityReadResult.Found)world.ReadEntity(new EntityId(entity))).Entity;

    /// <summary>Whether the entity is still in the world: a building element is gone once a grazer has eaten it.</summary>
    internal bool Exists(string entity) => world.ReadEntity(new EntityId(entity)) is EntityReadResult.Found;

    /// <summary>Whether a grazer has cut the seam.</summary>
    internal bool IsSevered(string seam) => world.ReadSeam(new SeamId(seam)) is SeamReadResult.Found { Seam.Severed: true };

    /// <summary>Where a device is on its way: its angle for a hinge, its distance for a slide.</summary>
    internal double DevicePosition(string device) => State(device).DevicePosition!.Value;

    /// <summary>The bounds of an entity's slice in a section, or of one of its parts; null when there is none.</summary>
    internal Aabb? Bounds(string entity, string section, string? part = null) =>
        world.GetSliceBounds(new(entity), new(section), part is null ? null : new PartId(part)) is SliceBoundsResult.Bounds box
            ? new Aabb(V(box.Min), V(box.Max) - V(box.Min)) : null;

    /// <summary>Whether the entity lies still on something that holds it up: not in a hand, not turning, and where it would come to rest.</summary>
    internal bool IsResting(string entity)
    {
        var state = State(entity);
        return state.Holder is null && !state.Rotating && world.FindSupport(new(entity)) is SupportResult.Supported support
            && Math.Abs(support.RestingOriginY - state.Pose.Position.Y) < RestMargin;
    }

    /// <summary>Whether there is no floor under the middle of a slice in section II: it hangs over a hole.</summary>
    internal bool OverEmptySpace(Aabb slice)
    {
        var middle = slice.GetCenter();
        var reach = slice.Size.X / 2 * MiddleShare;
        return InSectionII(new(middle.X, FloorProbeY, middle.Z), new(reach, FloorProbeHeight, reach)).Count == 0;
    }

    /// <summary>The entities that fill any of a box in section II, the box being given by its middle and half sizes.</summary>
    internal IReadOnlyList<string> InSectionII(Vector3 center, Vector3 half)
    {
        var pose = SceneReader.PoseOf(new Transform3D(Basis.Identity, center), region.DepthOf(SectionII));
        var box = new OccupancyBox(pose, new(half.X, half.Y, half.Z, CreatureLink.Epsilon));
        return world.QueryOccupancy(box, []) is OccupancyResult.Occupancy occupancy
            ? occupancy.Hits.Select(hit => hit.Entity.Value).Distinct().ToArray() : [];
    }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
