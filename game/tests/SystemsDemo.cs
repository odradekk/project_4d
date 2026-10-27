using Game.Adapter;
using Game.Flow;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Tests;

/// <summary>Opening state for the debug integration scene.</summary>
public partial class SystemsDemo : Node
{
    public SceneCheckpoint Checkpoint { get; private set; } = null!;
    public override void _Ready()
    {
        var region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
        region.Name = "Region"; AddChild(region);
        var player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        var prism = region.GetNode<PrismView>("Generated/Prism");
        player.Hud.UnlockPage(1); player.Hud.UnlockPage(2); player.Hud.NotebookAvailable = true;
        var entrance = player.Transform;
        Checkpoint = new SceneCheckpoint(); AddChild(Checkpoint);
        Checkpoint.Restarting += () => { player.Hud.ClearText(); prism.SetRaised(false); };
        Checkpoint.Set(() => { player.Transform = entrance; player.Velocity = Vector3.Zero; });
    }
}
