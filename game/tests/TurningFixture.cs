using System;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Godot;

namespace Game.Tests;

/// <summary>The test region with its player, and the helpers the turning checks use to play and observe it.</summary>
internal sealed class TurningFixture(Node host)
{
    // A turn lasts at most 1.4 s; physics runs at 60 frames per second.
    internal const int TurnFrames = 90;

    internal Region3D Region { get; private set; } = null!;
    internal FirstPersonPlayer Player { get; private set; } = null!;
    internal PlayerActions Actions => Region.Actions!;

    /// <summary>
    /// Replaces the region with a fresh instance of the test region, or of another region scene, after <paramref name="change"/>
    /// has edited it.
    /// </summary>
    internal async Task Load(Action<Region3D>? change = null, string scene = "res://regions/section_test.tscn")
    {
        if (Region is not null)
        {
            host.RemoveChild(Region);
            Region.Free();
        }
        Region = GD.Load<PackedScene>(scene).Instantiate<Region3D>();
        change?.Invoke(Region);
        host.AddChild(Region);
        Check(Region.Loaded, $"the region {scene} must load: {Region.Diagnostics}");
        Player = Region.GetNode<FirstPersonPlayer>("Generated/Player");
        await Frames(3);
    }

    /// <summary>Removes the region and waits for the audio server to let go of the sounds that were playing in it.</summary>
    internal async Task Unload()
    {
        host.RemoveChild(Region);
        Region.Free();
        // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
        await host.ToSignal(host.GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>Puts the player's feet at <paramref name="feet"/> with the view on <paramref name="target"/>.</summary>
    internal void LookAt(Vector3 feet, Vector3 target)
    {
        Player.Position = feet;
        Player.Velocity = Vector3.Zero;
        var offset = target - (feet + new Vector3(0, 1.3f, 0));
        Player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        Player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
    }

    /// <summary>Moves the player to <paramref name="feet"/> half a metre per frame, as a walk, not a jump.</summary>
    internal async Task Walk(Vector3 feet)
    {
        while (Player.Position.DistanceTo(feet) > .001f)
        {
            Player.Position = Player.Position.MoveToward(feet, .5f);
            await Frames(1);
        }
    }

    internal EntityState State(string id) => ((EntityReadResult.Found)Region.World!.ReadEntity(new EntityId(id))).Entity;

    internal Vector3 Position(string id)
    {
        var position = State(id).Pose.Position;
        return new((float)position.X, (float)position.Y, (float)position.Z);
    }

    internal bool HasSlice(string section, string id) => Region.HasNode($"Generated/{section}/Dynamic/{id}");

    internal Aabb SliceBounds(string section, string id) =>
        Region.GetNode<MeshInstance3D>($"Generated/{section}/Dynamic/{id}/Surface").Mesh.GetAabb();

    /// <summary>The color of an entity in the lens's outline mask: black unless it has a glowing outline.</summary>
    internal Color Mask(string section, string id)
    {
        var group = Region.HasNode($"Generated/{section}/Dynamic/{id}") ? "Dynamic" : "Geometry";
        return ((StandardMaterial3D)Region.GetNode<MeshInstance3D>($"Generated/{section}/{group}/{id}/PrismMask").MaterialOverride).AlbedoColor;
    }

    internal async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await host.ToSignal(host.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    internal static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    internal static void Button(MouseButton button, bool pressed) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed });

    internal static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(actual.DistanceTo(expected) < .01f, $"{message}: expected {expected}, got {actual}");

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
