using System;
using FourD;
using Game.Adapter;
using Game.Player;
using Godot;

namespace Game.Characters;

public enum Posture { Standing, Sitting, Lying }

/// <summary>
/// A capsule character that walks in the physics world of its section and reports its position to the engine.
/// What it does is a command: <see cref="Do"/> runs a step function every physics frame until the function says it is
/// done, and <see cref="ActionFinished"/> follows. Behavior that only some characters have, such as a cat's jump or a
/// teacher's patrol, is a child node that gives its parent commands. The scene has a child node Visual that holds
/// the meshes, which this scales for posture and mirror image.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
[GlobalClass]
public partial class Character3D : CharacterBody3D
{
    /// <summary>The physics layer of the bodies of characters, which the crosshair looks through.</summary>
    public const uint BodyLayer = 2;

    /// <summary>The physics layer of what a character is picked up by: seen by the crosshair, and not by what looks for things to hide a seam.</summary>
    public const uint PickupLayer = 4;

    /// <summary>How close to a point the character has to come for a walk to it to be done.</summary>
    public const float ArrivalRadius = .1f;

    private const float Gravity = 9.8f;

    [Signal] public delegate void ActionFinishedEventHandler();

    /// <summary>The character was told to do something else, or to stop, while it was doing something.</summary>
    [Signal] public delegate void ActionReplacedEventHandler();

    /// <summary>The speaker shown with the lines it says.</summary>
    [Export] public string DisplayName { get; set; } = "";
    [Export] public float WalkSpeed { get; set; } = 1.2f;
    [Export] public float RunSpeed { get; set; } = 3f;
    public Posture Posture { get; set; }

    private CreatureLink link = null!;
    private Node3D visual = null!;
    private CapsuleShape3D capsule = null!;
    private Func<double, bool>? action;
    private Vector3 intent;
    private bool warped;

    internal CreatureLink Link => link;
    public bool Busy => action is not null;
    public float Radius => capsule.Radius;
    public float Height => capsule.Height;

    /// <summary>The section the engine has the character in.</summary>
    public string Section => link.Section;

    /// <summary>Whether the engine has the character's body turned into its mirror image.</summary>
    public bool Mirrored => link.Handedness == Handedness.Mirrored;

    /// <summary>The player, whose lines share the screen with the character's and whose section the character can be in.</summary>
    public FirstPersonPlayer Player { get; private set; } = null!;

    /// <summary>The region the character is in, which the sounds that follow it need; null for a character placed by hand.</summary>
    public Region3D? Region { get; private set; }

    /// <summary>Whether the engine has the character in the same section as the player.</summary>
    public bool WithPlayer => link.WithPlayer;

    internal void Bind(CreatureLink creature, FirstPersonPlayer player, Region3D? region = null)
    {
        link = creature;
        Player = player;
        Region = region;
    }

    public override void _Ready()
    {
        visual = GetNode<Node3D>("Visual");
        capsule = (CapsuleShape3D)GetNode<CollisionShape3D>("Shape").Shape;
        // The scene of a character can be played on its own, where nothing has bound it to the engine.
        SetPhysicsProcess(link is not null);
    }

    /// <summary>Runs <paramref name="next"/> with the frame's time in place of what the character was doing, until it returns true.</summary>
    public void Do(Func<double, bool> next) => Replace(next);

    public void Stop() => Replace(null);

    /// <summary>Walks, or runs, to the point and stands there. The height of the point does not count.</summary>
    public void GoTo(Vector3 point, bool run = false)
    {
        Posture = Posture.Standing;
        Do(_ => WalkTo(point, run ? RunSpeed : WalkSpeed));
    }

    public void Stand()
    {
        Stop();
        Posture = Posture.Standing;
    }

    public void Sit()
    {
        Stop();
        Posture = Posture.Sitting;
    }

    public void Say(string line, double seconds) => Player.Hud.Say(DisplayName, line, seconds);

    /// <summary>Puts the character down at a place and a direction, as at the start of a scene.</summary>
    public void PlaceAt(Vector3 position, float yaw)
    {
        Rotation = new(0, yaw, 0);
        Warp(position);
        link.Sync(position, yaw);
    }

    /// <summary>Takes the character through a seam; null when it crossed, otherwise why the engine refused.</summary>
    public FailureCode? CrossSeam(string seam, string target)
    {
        var refused = link.Cross(this, seam, target);
        if (refused is null) Warp(Position);
        return refused;
    }

    /// <summary>Turns toward a point, as far as the ground plane goes.</summary>
    public void Face(Vector3 point)
    {
        var offset = point - Position;
        if (new Vector2(offset.X, offset.Z).LengthSquared() > 1e-6f) Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
    }

    /// <summary>
    /// Asks for one frame's walk toward the point, which the character takes after the step that asked. True once it is
    /// there. The step functions of commands call it every frame; <see cref="GoTo"/> is the command.
    /// </summary>
    internal bool WalkTo(Vector3 point, float speed)
    {
        var offset = point - Position;
        offset.Y = 0;
        var distance = offset.Length();
        if (distance <= ArrivalRadius) return true;
        Face(point);
        intent = offset / distance * Mathf.Min(speed, distance / (float)GetPhysicsProcessDeltaTime());
        return false;
    }

    private void Replace(Func<double, bool>? next)
    {
        var interrupted = action is not null;
        action = next;
        if (interrupted) EmitSignal(SignalName.ActionReplaced);
    }

    /// <summary>Moves the character without physics this frame, such as along a jump.</summary>
    internal void Warp(Vector3 position)
    {
        Position = position;
        Velocity = Vector3.Zero;
        warped = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        var current = action;
        if (current is not null && current(delta) && action == current)
        {
            action = null;
            EmitSignal(SignalName.ActionFinished);
        }
        if (!warped)
        {
            Velocity = new(intent.X, IsOnFloor() ? 0 : Velocity.Y - Gravity * (float)delta, intent.Z);
            MoveAndSlide();
        }
        warped = false;
        intent = Vector3.Zero;
        link.Sync(Position, Rotation.Y);
        visual.Scale = new(Mirrored ? -1 : 1, Posture switch { Posture.Sitting => .6f, Posture.Lying => .35f, _ => 1f }, 1);
    }
}
