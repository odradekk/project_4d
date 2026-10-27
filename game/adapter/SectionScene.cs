using System.Collections.Generic;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>What the adapter adds to one section's world beside its static geometry.</summary>
internal sealed class SectionScene(SectionId id, double w)
{
    internal SectionId Id { get; } = id;

    /// <summary>The depth of the section.</summary>
    internal double W { get; } = w;

    /// <summary>The slices of the entities that do not move, which a bite takes elements out of.</summary>
    internal Node3D Geometry { get; set; } = null!;

    /// <summary>The slices of the entities that move, rebuilt whenever they change.</summary>
    internal Node3D Dynamic { get; } = new() { Name = "Dynamic" };

    /// <summary>Lines and marks that only the lens shows, and the dots under a ball, which both views show.</summary>
    internal Node3D Overlay { get; } = new() { Name = "Overlay" };

    /// <summary>The outline mask color of each through-object, which stays the same when its slice is rebuilt.</summary>
    internal Dictionary<EntityId, int> MaskIds { get; } = [];
}
