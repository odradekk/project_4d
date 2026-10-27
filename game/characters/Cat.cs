using System;
using FourD;
using Game.Interaction;
using Godot;

namespace Game.Characters;

/// <summary>
/// What a cat does, as commands to the <see cref="Character3D"/> it is a child of. Each command replaces the one before it.
/// Walking, running and crossing a seam are the character's own commands. A cat can use seams without a prism, and the
/// engine tells whether it is mirrored after it has.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
[GlobalClass]
public partial class Cat : Node
{
    // The cat stops this far from the player and starts again when it is this much farther.
    private const float FollowDistance = 1f;
    private const float FollowSlack = .5f;
    private const float RunDistance = 3f;
    // The distance at which a chasing cat has reached its target.
    private const float ChaseReach = .3f;
    private const float JumpRise = .4f;
    private const float JumpSpeed = 3f;
    private const float FleeDistance = 3f;
    private const double FleeSeconds = 3;
    // The ring is looked at head on when the cat's line to it is within about 35 degrees of the ring's axis.
    private const float FacesCat = .8f;
    // How far before the ring the cat starts its leap, and how far past it that leap ends.
    private const float RunUp = 1.2f;
    private const float PathMargin = .02f;
    // Where the cat is held: in front of the chest.
    private static readonly Vector3 HeldAt = new(0, 1, -.45f);

    private Character3D body = null!;
    private Interactable3D pickup = null!;
    private uint pickupLayer;

    public override void _Ready()
    {
        body = GetParent<Character3D>();
        pickup = body.GetNode<Interactable3D>("Pickup");
        pickupLayer = pickup.CollisionLayer;
        pickup.Interacted += PickUp;
    }

    /// <summary>Walks to random places within the radius of a point, and rests between them.</summary>
    public void Wander(Vector3 center, float radius)
    {
        var random = new RandomNumberGenerator { Seed = 1 };
        var target = body.Position;
        var rest = 0.0;
        Begin(delta =>
        {
            if (rest > 0)
            {
                rest -= delta;
                return false;
            }
            if (!body.WalkTo(target, body.WalkSpeed)) return false;
            rest = random.RandfRange(1, 3);
            var angle = random.RandfRange(0, Mathf.Tau);
            target = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * random.RandfRange(0, radius);
            return false;
        });
    }

    /// <summary>Keeps close to the player, running when it falls far behind. It waits while the engine has the player in another section.</summary>
    public void Follow()
    {
        var walking = false;
        Begin(_ =>
        {
            if (!body.Link.WithPlayer) return false;
            var player = body.Link.Player.Position;
            var distance = Flat(player - body.Position).Length();
            walking = distance > (walking ? FollowDistance : FollowDistance + FollowSlack);
            if (walking) body.WalkTo(player, distance > RunDistance ? body.RunSpeed : body.WalkSpeed);
            return false;
        });
    }

    /// <summary>Runs after a moving target, wherever it is now. It stands still while there is no target.</summary>
    public void Chase(Func<Vector3?> target)
    {
        Begin(_ =>
        {
            if (target() is { } at && Flat(at - body.Position).Length() > ChaseReach) body.WalkTo(at, body.RunSpeed);
            return false;
        });
    }

    /// <summary>The middle of an entity's slice, or of one part of it, in the cat's section; null when it has none there.</summary>
    public Vector3? SliceCenter(string entity, string part) => body.Link.SliceBounds(entity, part)?.GetCenter();

    /// <summary>Leaps along an arc to a point.</summary>
    public void JumpTo(Vector3 point) => Begin(Jump(point, JumpRise));

    /// <summary>Leaps to a high place and lies down there.</summary>
    public void Perch(Vector3 point)
    {
        var jump = Jump(point, JumpRise);
        Begin(delta =>
        {
            if (!jump(delta)) return false;
            body.Posture = Posture.Lying;
            return true;
        });
    }

    /// <summary>Sits down facing a point.</summary>
    public void Sit(Vector3 facing)
    {
        body.Sit();
        body.Face(facing);
    }

    /// <summary>Stands and keeps its eyes on a point.</summary>
    public void Stare(Vector3 point) => Begin(_ =>
    {
        body.Face(point);
        return false;
    });

    /// <summary>Runs a few metres straight away from a point.</summary>
    public void Flee(Vector3 from)
    {
        var away = Flat(body.Position - from);
        if (away.LengthSquared() < 1e-6f) away = -body.Basis.Z;
        var goal = body.Position + away.Normalized() * FleeDistance;
        var elapsed = 0.0;
        Begin(delta =>
        {
            elapsed += delta;
            return body.WalkTo(goal, body.RunSpeed) || elapsed > FleeSeconds;
        });
    }

    /// <summary>
    /// Runs up to a ring and leaps through its hole, landing on the other side. Does nothing and says false when the
    /// ring has no slice in the cat's section, is not seen head on, or when the cat's body would meet something on the way.
    /// </summary>
    public bool JumpThroughRing(string entity, string part)
    {
        var link = body.Link;
        if (link.SliceBounds(entity, part) is not { } ring || link.SlicePlaneNormal(entity, part, Axis3.X, Axis3.Y) is not { } axis) return false;
        var hole = ring.GetCenter();
        // The ring is head on when the line from the cat to the hole runs along its axis, from either side.
        var axisOnGround = Flat(axis).Normalized();
        var alignment = axisOnGround.Dot(Flat(hole - body.Position).Normalized());
        if (Mathf.Abs(alignment) < FacesCat) return false;
        var along = axisOnGround * Mathf.Sign(alignment);
        var halfSize = new Vector3(body.Radius + PathMargin, body.Height / 2 + PathMargin, RunUp);
        if (!link.IsClear(hole, Mathf.Atan2(along.X, along.Z), halfSize)) return false;
        var level = new Vector3(hole.X, body.Position.Y, hole.Z);
        var start = level - along * RunUp;
        var landing = level + along * RunUp;
        // The top of the arc is over the ring's plane, with the middle of the cat level with the middle of the hole.
        var rise = Mathf.Max(hole.Y - body.Height / 2 - body.Position.Y, JumpRise);
        Func<double, bool>? jump = null;
        Begin(delta =>
        {
            if (jump is null)
            {
                if (!body.WalkTo(start, body.RunSpeed)) return false;
                jump = Jump(landing, rise);
            }
            return jump(delta);
        });
        return true;
    }

    private void Begin(Func<double, bool> action)
    {
        body.Posture = Posture.Standing;
        body.Do(action);
    }

    // A step function that moves the cat along an arc from where it is to the point, rising above the straight line.
    // A leap up onto a ledge arcs higher by half the climb, so that it clears the edge.
    private Func<double, bool> Jump(Vector3 to, float rise)
    {
        var from = body.Position;
        var lift = rise + Mathf.Max(0, to.Y - from.Y) / 2;
        var duration = Mathf.Clamp(from.DistanceTo(to) / JumpSpeed, .4f, 1.2f);
        var elapsed = 0.0;
        return delta =>
        {
            elapsed += delta;
            var t = (float)Math.Min(elapsed / duration, 1);
            body.Face(to);
            body.Warp(from.Lerp(to, t) + Vector3.Up * 4 * lift * t * (1 - t));
            return t >= 1;
        };
    }

    private void PickUp()
    {
        var player = body.Link.Player;
        pickup.CollisionLayer = 0;
        player.PutDown = PutDown;
        Begin(_ =>
        {
            // Crossing a seam takes both hands for the prism: a cat held when the player leaves its section is put down.
            if (!body.Link.WithPlayer) PutDown();
            else body.Warp(player.Position + player.Basis * HeldAt);
            return false;
        });
    }

    private void PutDown()
    {
        pickup.CollisionLayer = pickupLayer;
        body.Link.Player.PutDown = null;
        body.Stop();
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
