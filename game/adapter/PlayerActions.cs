using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Adapter;

/// <summary>Why the engine refused a player action, and the section that shows the first obstacle, null when none does.</summary>
public sealed record Refusal(FailureCode Code, string? Section);

/// <summary>
/// The player's hands. Holds, moves and turns objects through the engine, and advances the engine once per
/// physics frame, keeping the generated section worlds in step with it.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public sealed partial class PlayerActions : Node
{
    // The hand is a point ahead of the body whose height follows where the player looks.
    private const float HandReach = .6f;
    private const float HandRest = 1f;
    private const float HandSwing = .7f;
    private const float LowestHand = .12f;
    private const float HighestHand = 1.6f;
    // The physics layer of the slices and colliders that the player and what the player carries run into.
    private const uint WorldLayer = 1;
    // An object this far from the eyes is let go: past the 2 m the crosshair reaches, with room for a grip at its edge.
    private const float LetGoDistance = 2.5f;
    private const double PreviewInterval = .15;

    private World world = null!;
    private EntityId actor;
    private FirstPersonPlayer player = null!;
    private PrismView prism = null!;
    private IReadOnlyList<SectionScene> scenes = [];
    private DynamicSlices slices = null!;
    private LensOverlay overlay = null!;
    private AudioStreamPlayer scratch = null!;
    private CharacterBody3D carrier = null!;
    private string? aimed;
    private Vector3 grip;
    private Vector3 lastHand;
    private bool wasRotating;
    private double previewDelay;
    private DeviceDrive devices = null!;
    private BallShadows shadows = null!;
    // What the preview on show was made for. The engine's plan is read again only when this changes, and at most every
    // PreviewInterval, since planning a turn of a nut takes tens of milliseconds.
    private (string Subject, string Target, Vector3 Feet, int Version)? previewed;

    /// <summary>The entity in the hand: a flat object carried, or the slice of an upright one gripped.</summary>
    internal string? Held => HeldId()?.Value;
    public bool Holding => HeldId() is not null;
    public bool HeldIsUpright => HeldId() is { } id && State(id).UprightOrigin is not null;
    public string CurrentSection => State(actor).Section?.Value ?? "";

    /// <summary>Whether the player is a mirror image, as the engine has her: it turns the view and the controls over.</summary>
    public bool Mirrored => State(actor).Handedness == Handedness.Mirrored;

    /// <summary>Raised with the reason when a device that the player pulled is stopped by something in its way.</summary>
    internal event Action<Refusal>? DeviceBlocked;

    internal void Configure(World world, string actor, FirstPersonPlayer player, PrismView prism,
        IReadOnlyList<SectionScene> scenes, DynamicSlices slices, LensOverlay overlay, Region3D region)
    {
        this.world = world;
        this.actor = new(actor);
        this.player = player;
        this.prism = prism;
        this.scenes = scenes;
        this.slices = slices;
        this.overlay = overlay;
        devices = new DeviceDrive(world, scenes, slices, overlay, this, region, refusal => DeviceBlocked?.Invoke(refusal));
        shadows = new BallShadows(world, scenes, slices);
        scratch = new AudioStreamPlayer { Name = "Scratch", Stream = GD.Load<AudioStream>("res://audio/scratch.wav") };
        AddChild(scratch);
        // A body that is never seen: the shape of the carried object is put on it, and it is moved to test the way.
        carrier = new CharacterBody3D { Name = "Carrier", CollisionLayer = 0, CollisionMask = WorldLayer };
        carrier.AddCollisionExceptionWith(player);
        AddChild(carrier);
        // After the player has moved, so that the hand is where the player is this frame.
        ProcessPhysicsPriority = 1;
        lastHand = Hand();
        slices.Refresh();
    }

    /// <summary>Names the entity the crosshair is on, or null. Call once per physics frame.</summary>
    public void Aim(string? entity) => aimed = entity;

    /// <summary>Whether the player may pick up, grip and turn the entity.</summary>
    public bool IsHoldable(string entity) => State(new EntityId(entity)).Definition switch
    {
        TurnableDefinition turnable => turnable.Interaction.Holdable,
        ItemDefinition item => item.Interaction.Holdable,
        _ => false
    };

    /// <summary>Whether the entity is a device, which E moves and nothing picks up.</summary>
    public bool IsDevice(string entity) => devices.IsDevice(new EntityId(entity));

    /// <summary>E on the aimed device: it starts moving to the far end of its way, or back. Why it stops comes with DeviceBlocked.</summary>
    public void Pull()
    {
        if (aimed is not null && devices.IsDevice(new EntityId(aimed))) devices.Pull(new EntityId(aimed));
    }

    /// <summary>The upright state of an entity: its slice stands in W instead of lying in its section.</summary>
    public bool IsUpright(string entity) => State(new EntityId(entity)).UprightOrigin is not null;

    /// <summary>E: lets go of what is held, otherwise picks up or grips the aimed entity.</summary>
    public Refusal? Use()
    {
        if (HeldId() is { } held)
        {
            return world.ReleaseEntity(held) is CarryResult.Rejected released ? Refuse(released.Failure) : null;
        }
        return aimed is null ? null : Hold(new EntityId(aimed));
    }

    /// <summary>Takes an entity into the hand from the world, or from the backpack.</summary>
    internal Refusal? Hold(EntityId item)
    {
        if (world.HoldEntity(actor, item) is CarryResult.Rejected rejected) return Refuse(rejected.Failure);
        Regrip();
        // The held slice has no collision body, which the hand's check against walls must not run into on the next frame.
        slices.Refresh();
        return null;
    }

    /// <summary>
    /// Makes the held entity stay where it is relative to the hand. A flat object comes to the hand; a slice stays
    /// where it is and moves with the hand from there, as a long item does that stood up in a seam.
    /// </summary>
    internal void Regrip()
    {
        var item = HeldId()!.Value;
        grip = State(item).UprightOrigin is not null ? Position(State(item)) - Hand() : Vector3.Zero;
        wasRotating = false;
    }

    /// <summary>F: stands the held or aimed entity toward the prism's target section, or lays an upright one down.</summary>
    public Refusal? StandOrLay()
    {
        if (Subject() is not { } id) return null;
        var request = StandOrLayRequest(id);
        return request is null ? null : Rotate(request);
    }

    /// <summary>R: flips the held or aimed entity.</summary>
    public Refusal? Flip() => Subject() is { } id ? Rotate(new Flip(actor, id)) : null;

    public override void _PhysicsProcess(double delta)
    {
        var position = player.Position;
        world.SynchronizeActor(actor, new(position.X, position.Y, position.Z), player.Rotation.Y);
        Follow();
        devices.Step(delta);
        world.Step(delta);
        slices.Refresh();
        shadows.Refresh(slices.Version, delta);
        overlay.Tick(delta);
        UpdatePreview(delta);
    }

    private EntityId? Subject() => HeldId() ?? (aimed is null ? null : new EntityId(aimed));

    // What is in the backpack is not in the hand.
    private EntityId? HeldId()
    {
        foreach (var id in slices.Entities)
            if (State(id) is { InBackpack: false } held && held.Holder == actor) return id;
        return null;
    }

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;

    private static Vector3 Position(EntityState state) =>
        new((float)state.Pose.Position.X, (float)state.Pose.Position.Y, (float)state.Pose.Position.Z);

    private RotationRequest? StandOrLayRequest(EntityId id)
    {
        if (State(id).UprightOrigin is not null) return new LayDown(actor, id);
        return prism.TargetSection is { } target ? new StandUp(actor, id, new SectionId(target)) : null;
    }

    private Refusal? Rotate(RotationRequest request)
    {
        var planned = world.PlanRotation(request);
        if (PlanOf(planned) is not { } plan) return Refuse(((RotationPlanResult.Rejected)planned).Failure);
        return world.StartRotation(plan) is StartRotationResult.Rejected rejected ? Refuse(rejected.Failure) : null;
    }

    // The plan of a request even when it is blocked, which the preview draws in red; null when no path exists at all.
    private static RotationPlan? PlanOf(RotationPlanResult result) => result switch
    {
        RotationPlanResult.Allowed allowed => allowed.Plan,
        RotationPlanResult.Blocked blocked => blocked.Plan,
        _ => null
    };

    /// <summary>
    /// Why the engine refused an action. The engine says in which section the blocking contact is; only a contact between
    /// sections leaves that open, and then the section that shows the obstacle is the answer.
    /// </summary>
    internal Refusal Refuse(Failure failure) => failure.Contacts.Count > 0 && failure.Contacts[0].Section is { } section
        ? new(failure.Code, section.Value)
        : new(failure.Code, failure.Contacts.Count > 0 ? SectionShowing(failure.Contacts[0].Obstacle) : null);

    // The player's section when the entity has a slice there, otherwise the first other section that has one.
    private string? SectionShowing(EntityId entity)
    {
        var current = State(actor).Section;
        if (current is { } here && Shows(entity, here)) return here.Value;
        foreach (var scene in scenes)
            if (scene.Id != current && Shows(entity, scene.Id)) return scene.Id.Value;
        return null;
    }

    private bool Shows(EntityId entity, SectionId section) =>
        world.HasSlice(entity, section) is SlicePresenceResult.Present { HasVolume: true };

    // The point ahead of the body, at the height the player looks toward.
    private Vector3 Hand()
    {
        var height = Mathf.Clamp(HandRest + HandSwing * Mathf.Sin(player.Camera.Rotation.X), LowestHand, HighestHand);
        return player.Position + player.Basis * new Vector3(0, height, -HandReach);
    }

    // Moves the held entity to where the hand is, the same way in every section.
    private void Follow()
    {
        var hand = Hand();
        var handMoved = !hand.IsEqualApprox(lastHand);
        lastHand = hand;
        if (HeldId() is not { } id) return;
        var state = State(id);
        if (state.Rotating)
        {
            wasRotating = true;
            return;
        }
        var position = Position(state);
        // A turn swings the entity about its hinge, so it ends up away from the hand: keep it there.
        if (wasRotating) grip = position - hand;
        wasRotating = false;
        if (position.DistanceTo(player.Camera.GlobalPosition) > LetGoDistance)
        {
            world.ReleaseEntity(id);
            return;
        }
        var delta = hand + grip - position;
        // A flat object lies in the section, where the host's physics checks it too; the engine alone checks what reaches into
        // other sections, such as a ball or an upright slice.
        if (state.Section is not null) delta = CarryThroughWorld(id, delta);
        var moved = world.MoveEntity(new MoveRequest(id, new(delta.X, delta.Y, delta.Z)));
        var contacts = moved is MoveEntityResult.Moved applied ? applied.Contacts : ((MoveEntityResult.Rejected)moved).Failure.Contacts;
        if (contacts.Count == 0 || !handMoved) return;
        overlay.MarkContacts(contacts);
        if (!scratch.Playing) scratch.Play();
    }

    // The engine leaves a flat object in the hand to the host's physics. The carrier, with the shapes of the object's parts,
    // moves along x, z and y in turn, the order of the engine's own sweep, and stops where it would touch anything, so it
    // slides along what blocks it. It also pushes itself out of what it starts in, such as the platform it was resting on.
    private Vector3 CarryThroughWorld(EntityId id, Vector3 delta)
    {
        if (State(actor).Section is not { } section) return delta;
        var hulls = slices.HullsOf(section, id);
        if (hulls.Count == 0) return delta;
        foreach (var old in carrier.GetChildren()) old.Free();
        foreach (var hull in hulls) carrier.AddChild(new CollisionShape3D { Shape = hull });
        carrier.Position = Vector3.Zero;
        foreach (var axis in new[] { 0, 2, 1 })
        {
            var step = Vector3.Zero;
            step[axis] = delta[axis];
            var collision = carrier.MoveAndCollide(step, testOnly: true, recoveryAsCollision: true);
            carrier.Position += collision is null ? step : collision.GetTravel();
        }
        return carrier.Position;
    }

    // Draws, in the section the lens shows, the sweep of what F would do to the held or aimed entity, so that red means F; the
    // sweep of R when F has no path. The fold plane of R is drawn too, and the dashed ring that standing a nut up would bring.
    private void UpdatePreview(double delta)
    {
        if (!prism.Raised || prism.TargetSection is not { } target || Subject() is not { } id)
        {
            previewDelay = 0;
            previewed = null;
            overlay.ClearPreview();
            return;
        }
        previewDelay = Math.Max(0, previewDelay - delta);
        var inputs = (id.Value, target, player.Position, slices.Version);
        if (inputs.Equals(previewed) || previewDelay > 0) return;
        previewed = inputs;
        previewDelay = PreviewInterval;
        var section = new SectionId(target);
        var stand = StandOrLayRequest(id);
        var (planned, sweep, ring) = stand is null ? (false, null, null) : PreviewOf(stand, section);
        var (_, flip, _) = PreviewOf(new Flip(actor, id), section);
        overlay.ShowPreview(section, planned ? sweep : flip, flip?.FoldPlane ?? [], ring);
    }

    // What the lens shows of the turn the request makes, in the section, whether or not the turn is blocked; whether the turn
    // has a path at all; and for a nut that is stood up, the ring that it leaves there. A nut shows its ring and not the sweep,
    // since cutting the tilted ring at every step takes tens of milliseconds.
    private (bool Planned, RotationPreview? Sweep, RotationPreview? Ring) PreviewOf(RotationRequest request, SectionId section)
    {
        if (PlanOf(world.PlanRotation(request)) is not { } plan) return (false, null, null);
        if (request is StandUp && Shown(world.GetHiddenStructurePreview(plan, section)) is { } ring) return (true, null, ring);
        return (true, Shown(world.GetRotationPreview(plan, section)), null);
    }

    private static RotationPreview? Shown(PreviewResult result) => (result as PreviewResult.Preview)?.Value;
}
