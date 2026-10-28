using System;
using System.Linq;
using Game.Adapter;
using Game.Audio;
using Game.Interaction;
using Game.Player;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// The street region as the scene scripts of the school road and the way home use it: its props and marks by name, the
/// sounds of the street, and buying. The region lists the props with the prompts left to the scripts, which know the prices.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class Street
{
    private const double BellInterval = 8;

    internal Street(Node host)
    {
        Region = GD.Load<PackedScene>("res://regions/street.tscn").Instantiate<Region3D>();
        Region.Name = "Region";
        host.AddChild(Region);
        if (!Region.Loaded) throw new InvalidOperationException($"The street did not load: {Region.Diagnostics}");
        Player = Region.GetNode<FirstPersonPlayer>("Generated/Player");
        var broadcast = Sound("broadcast", "Speaker");
        broadcast.Finished += () => broadcast.Play();
        // The sounds start a moment after the scene does, and a bicycle bell rings now and then. A sound that is playing
        // when the game exits keeps its stream in use, which Godot reports as an error.
        var bell = Sound("bell", "Bell");
        var timer = new Timer { WaitTime = 1, Autostart = true };
        timer.Timeout += () =>
        {
            timer.WaitTime = BellInterval;
            bell.Play();
            if (!broadcast.Playing) broadcast.Play();
        };
        bell.AddChild(timer);
    }

    internal Region3D Region { get; }
    internal FirstPersonPlayer Player { get; }
    internal GameHud Hud => Player.Hud;

    internal Interactable3D Prop(string name) => Region.GetNode<Interactable3D>($"Props/{name}");

    internal Node3D Mark(string name) => Region.GetNode<Node3D>($"Marks/{name}");

    /// <summary>The way a node faces on the ground plane, as the yaw that characters and the player turn by.</summary>
    internal static float Yaw(Node3D node) => Mathf.Atan2(node.GlobalBasis.Z.X, node.GlobalBasis.Z.Z);

    /// <summary>Switches off every prop but the ones named; their prompts do not show and E does not reach them.</summary>
    internal void UseProps(params string[] names)
    {
        foreach (var prop in Region.GetNode("Props").GetChildren().OfType<Interactable3D>())
            if (!names.Contains(prop.Name.ToString())) prop.CollisionLayer = 0;
    }

    /// <summary>Stands the player on a mark, facing the way the mark faces.</summary>
    internal void PlacePlayer(string mark)
    {
        var at = Mark(mark);
        Player.GlobalPosition = at.GlobalPosition;
        Player.Rotation = new(0, Yaw(at), 0);
        Player.Velocity = Vector3.Zero;
    }

    /// <summary>Pays for something, and says what is left; false, and a line about it, when there is not enough.</summary>
    internal bool Buy(Wallet wallet, string what, int price)
    {
        if (!wallet.Spend(price))
        {
            Hud.Say("小满", "钱不够了。", 2);
            return false;
        }
        Hud.ShowNotice($"买了{what}，还剩 {wallet}", 2.5);
        return true;
    }

    // A sound of the street, heard from a mark. The listener is in the main viewport with the player.
    private SectionSound Sound(string file, string mark)
    {
        var sound = new SectionSound { Stream = GD.Load<AudioStream>($"res://audio/{file}.wav"), Position = Mark(mark).Position, UnitSize = 10 };
        Player.AddSibling(sound);
        sound.Configure(Region, Region.CurrentSection);
        return sound;
    }
}
