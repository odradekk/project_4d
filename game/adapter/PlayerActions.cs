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
    private const float WallMargin = .1f;
    // A flat object in the hand is kept out of walls this way; an upright slice is stopped by the engine instead.
    private const uint WorldLayer = 1;
    private const double PreviewInterval = .15;

    private World world = null!;
    private EntityId actor;
    private FirstPersonPlayer player = null!;
    private PrismView prism = null!;
    private IReadOnlyList<SectionScene> scenes = [];
    private DynamicSlices slices = null!;
    private LensOverlay overlay = null!;
    private AudioStreamPlayer scratch = null!;
    private string? aimed;
    private Vector3 grip;
    private Vector3 lastHand;
    private bool wasRotating;
    private double previewDelay;

    /// <summary>The entity in the hand: a flat object carried, or the slice of an upright one gripped.</summary>
    public string? Held => HeldId()?.Value;
    public bool Holding => HeldId() is not null;
    public bool HeldIsUpright => HeldId() is { } id && State(id).UprightOrigin is not null;
    public string CurrentSection => State(actor).Section?.Value ?? "";

    internal void Configure(World world, string actor, FirstPersonPlayer player, PrismView prism,
        IReadOnlyList<SectionScene> scenes, DynamicSlices slices, LensOverlay overlay)
    {
        this.world = world;
        this.actor = new(actor);
        this.player = player;
        this.prism = prism;
        this.scenes = scenes;
        this.slices = slices;
        this.overlay = overlay;
        scratch = new AudioStreamPlayer { Name = "Scratch", Stream = GD.Load<AudioStream>("res://audio/scratch.wav") };
        AddChild(scratch);
        // After the player has moved, so that the hand is where the player is this frame.
        ProcessPhysicsPriority = 1;
        lastHand = Hand();
        slices.Refresh();
    }

    /// <summary>Names the entity the crosshair is on, or null. Call once per physics frame.</summary>
    public void Aim(string? entity) => aimed = entity;

    /// <summary>Whether the player may pick up, grip and turn the entity.</summary>
    public bool IsHoldable(string entity) => State(new EntityId(entity)).Definition is TurnableDefinition { Interaction.Holdable: true };

    /// <summary>The upright state of an entity: its slice stands in W instead of lying in its section.</summary>
    public bool IsUpright(string entity) => State(new EntityId(entity)).UprightOrigin is not null;

    /// <summary>E: lets go of what is held, otherwise picks up or grips the aimed entity.</summary>
    public Refusal? Use()
    {
        if (HeldId() is { } held)
        {
            return world.ReleaseEntity(held) is CarryResult.Rejected released ? Refuse(released.Failure) : null;
        }
        if (aimed is null) return null;
        var item = new EntityId(aimed);
        if (world.HoldEntity(actor, item) is CarryResult.Rejected rejected) return Refuse(rejected.Failure);
        // A flat object comes to the hand; a slice stays where it is and moves with the hand from there.
        var position = Position(State(item));
        grip = IsUpright(aimed) ? position - Hand() : Vector3.Zero;
        wasRotating = false;
        // The held slice has no collision body, which the hand's check against walls must not run into on the next frame.
        slices.Refresh();
        return null;
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
        world.Step(delta);
        slices.Refresh();
        overlay.Tick(delta);
        UpdatePreview(delta);
    }

    private EntityId? Subject() => HeldId() ?? (aimed is null ? null : new EntityId(aimed));

    private EntityId? HeldId()
    {
        foreach (var id in slices.Entities)
            if (State(id).Holder == actor) return id;
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

    private Refusal Refuse(Failure failure) =>
        new(failure.Code, failure.Contacts.Count > 0 ? SectionShowing(failure.Contacts[0].Obstacle) : null);

    // The player's section when the entity has a slice there, otherwise the first other section that has one.
    private string? SectionShowing(EntityId entity)
    {
        var current = State(actor).Section;
        foreach (var scene in scenes.OrderBy(scene => scene.Id == current ? 0 : 1))
            if (world.HasSlice(entity, scene.Id) is SlicePresenceResult.Present { HasVolume: true }) return scene.Id.Value;
        return null;
    }

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
        var target = state.UprightOrigin is null ? StopAtWall(hand) + grip : hand + grip;
        var delta = target - position;
        var moved = world.MoveEntity(new MoveRequest(id, new(delta.X, delta.Y, delta.Z)));
        var contacts = moved is MoveEntityResult.Moved applied ? applied.Contacts : ((MoveEntityResult.Rejected)moved).Failure.Contacts;
        if (contacts.Count == 0 || !handMoved) return;
        overlay.MarkContacts(contacts);
        if (!scratch.Playing) scratch.Play();
    }

    // The engine does not check a flat object in the hand against walls, so the hand stops in front of them.
    private Vector3 StopAtWall(Vector3 hand)
    {
        var eyes = player.Camera.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(eyes, hand);
        query.CollisionMask = WorldLayer;
        query.Exclude = [player.GetRid()];
        var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? hand : hit["position"].AsVector3() - (hand - eyes).Normalized() * WallMargin;
    }

    // Draws, in the section the lens shows, the sweep of what F and R would do to the held or aimed entity.
    private void UpdatePreview(double delta)
    {
        if (!prism.Raised || prism.TargetSection is not { } target || Subject() is not { } id)
        {
            previewDelay = 0;
            overlay.ClearPreview();
            return;
        }
        previewDelay -= delta;
        if (previewDelay > 0) return;
        previewDelay = PreviewInterval;
        var section = new SectionId(target);
        RotationRequest?[] requests = [StandOrLayRequest(id), new Flip(actor, id)];
        var previews = new List<RotationPreview>();
        foreach (var request in requests.OfType<RotationRequest>())
            if (PlanOf(world.PlanRotation(request)) is { } plan && world.GetRotationPreview(plan, section) is PreviewResult.Preview preview)
                previews.Add(preview.Value);
        overlay.ShowPreview(section, previews);
    }
}
