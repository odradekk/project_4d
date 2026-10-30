using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Game.Audio;
using Game.Interaction;
using Game.Player;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// The home region as the day's scenes use it: the props that a scene switches on and off, the flat's doors, and the
/// sounds of the flat. Scenes 1 and 7 play in the compound; scenes 8 and 9 still play in the old home's room.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
internal sealed class HomeScene
{
    internal const string RegionPath = "res://regions/compound.tscn";
    internal const string RoomPath = "res://regions/room.tscn";
    internal const string Section = "Ⅰ";
    /// <summary>The height of the flat's floor in the compound, on the second storey.</summary>
    internal const float Floor = 3.2f;

    private readonly Dictionary<string, uint> layers = [];

    private HomeScene(Region3D region)
    {
        Region = region;
        Player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        foreach (var prop in region.Props(Section)!.GetChildren().OfType<Interactable3D>())
            layers[prop.Name] = prop.CollisionLayer;
        region.GetViewport().AudioListenerEnable3D = true;
    }

    internal Region3D Region { get; }
    internal FirstPersonPlayer Player { get; }
    internal GameHud Hud => Player.Hud;

    /// <summary>
    /// Loads the region into <paramref name="host"/>, after <paramref name="change"/> has edited it. The region is the
    /// compound by default, or the room of scenes 8 and 9.
    /// </summary>
    internal static HomeScene Open(Node host, Action<Region3D>? change = null, string path = RegionPath)
    {
        var region = GD.Load<PackedScene>(path).Instantiate<Region3D>();
        region.Name = "Region";
        change?.Invoke(region);
        host.AddChild(region);
        if (!region.Loaded) throw new InvalidOperationException($"The home region did not load:\n{region.Diagnostics}");
        return new HomeScene(region);
    }

    /// <summary>The scene's view of a home region that an earlier scene has loaded and left in its state.</summary>
    internal static HomeScene Of(Region3D region) => new(region);

    /// <summary>Where the seam node of that name stands, on the floor.</summary>
    internal Vector3 SeamAt(string node) => SceneReader.RelativeTransform(Region.GetNode<Seam3D>($"Seams/{node}"), Region).Origin;

    internal T Prop<T>(string name, string section = Section) where T : Node => Region.Props(section)!.GetNode<T>(name);

    internal Interactable3D Prop(string name) => Prop<Interactable3D>(name);

    /// <summary>One of the flat's doors in the compound, such as FrontDoor.</summary>
    internal Door3D Door(string name) => Prop<Door3D>($"HomeDoors/{name}");

    /// <summary>Lets the crosshair find the prop, or stops it.</summary>
    internal void Enable(string name, bool on) => Prop(name).CollisionLayer = on ? layers[name] : 0;

    /// <summary>Shows a prop and lets the crosshair find it, or hides it altogether.</summary>
    internal void Show(string name, bool on)
    {
        Prop(name).Visible = on;
        Enable(name, on);
    }

    /// <summary>A sound from a place in the home section, played again whenever it ends, for as long as <paramref name="wanted"/> says so.</summary>
    internal SectionSound Loop(string file, Vector3 position, float volumeDb, float pitch = 1, Func<bool>? wanted = null)
    {
        var sound = new SectionSound
        {
            Stream = GD.Load<AudioStream>($"res://audio/{file}.wav"),
            Position = position,
            VolumeDb = volumeDb,
            PitchScale = pitch,
            UnitSize = 3,
            MaxDistance = 14
        };
        Region.AddChild(sound);
        sound.Configure(Region, Section);
        sound.Finished += () =>
        {
            if (wanted?.Invoke() ?? true) sound.Play();
        };
        sound.Play();
        return sound;
    }
}
