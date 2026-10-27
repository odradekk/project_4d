using Godot;

namespace Game.Adapter;

/// <summary>The collision body of one slice of a movable entity; the player aims at it to pick the entity.</summary>
public partial class EntityBody3D : StaticBody3D
{
    public string Entity { get; set; } = "";
}
