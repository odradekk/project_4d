using System;
using System.Linq;
using FourD;
using Game.Interaction;
using Godot;

namespace Game.Characters;

/// <summary>
/// What a cat does, as commands to the <see cref="Character3D"/> it is a child of. Each command replaces the one before it,
/// and ends the carrying if the player had the cat in the arms. Walking, running and crossing a seam are the character's own
/// commands. A cat can use seams without a prism, and the engine tells whether it is mirrored after it has.
/// The cat goes straight at where it is told to go and slides along what is in the way. Where the way bends, round a hole
/// or up a stair, the script gives it the points to go through: <see cref="Follow"/> takes them, and <see cref="Chase"/> takes
/// any function for its target.
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
    // The ring is looked at head on when the cat's line to it is within about 37 degrees of the ring's axis.
    private const float FacesCat = .8f;
    // How far before the ring the cat starts its leap and how far past it the leap ends, longest first.
    private static readonly float[] Reaches = [1.2f, 1f, .8f, .6f];
    // The length of a box that tests the body along the arc.
    private const float LegLength = .1f;
    // How far either side of the ring's plane the body is tested against the ring itself, which is thinner than that.
    private const float RingDepth = .1f;
    private const float PathMargin = .02f;
    // Where the cat is held: in front of the chest.
    private static readonly Vector3 HeldAt = new(0, 1, -.45f);

    private Character3D body = null!;
    private Interactable3D pickup = null!;
    private uint pickupLayer;

    /// <summary>Emitted when the player picks the cat up.</summary>
    [Signal] public delegate void PickedUpEventHandler();

    /// <summary>Emitted when the player no longer has the cat in the arms: put down, or the cat was told to do something else.</summary>
    [Signal] public delegate void PutDownEventHandler();

    /// <summary>Whether the player has the cat in the arms.</summary>
    public bool Held { get; private set; }

    public override void _Ready()
    {
        body = GetParent<Character3D>();
        pickup = body.GetNode<Interactable3D>("Pickup");
        pickupLayer = pickup.CollisionLayer;
        pickup.Interacted += PickUp;
        body.ActionReplaced += Release;
        SetPhysicsProcess(body.Player is not null);
    }

    // The pick-up sphere is there for the crosshair only while the arms are free. With a cat or an object in them it is not
    // found, so that E does what the prompt says.
    public override void _PhysicsProcess(double delta) =>
        pickup.CollisionLayer = body.Player.HoldsItem || body.Player.PutDown is not null ? 0 : pickupLayer;

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

    /// <summary>
    /// Keeps close to the player, running when it falls far behind. It waits while the engine has the player in another section.
    /// <paramref name="around"/> turns the player's place into the place to walk to first, where the way is not straight.
    /// </summary>
    public void Follow(Func<Vector3, Vector3>? around = null)
    {
        var walking = false;
        Begin(_ =>
        {
            if (!body.WithPlayer) return false;
            var player = body.Player.Position;
            var distance = Flat(player - body.Position).Length();
            walking = distance > (walking ? FollowDistance : FollowDistance + FollowSlack);
            if (walking) body.WalkTo(around?.Invoke(player) ?? player, distance > RunDistance ? body.RunSpeed : body.WalkSpeed);
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

    /// <summary>Keeps its eyes on a point, in the posture it has.</summary>
    public void Stare(Vector3 point) => Stare(() => point);

    /// <summary>Keeps its eyes on a moving target, in the posture it has. It stays as it faces while there is no target.</summary>
    public void Stare(Func<Vector3?> target) => body.Do(_ =>
    {
        if (target() is { } at) body.Face(at);
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
    /// Runs up to a ring and leaps through its hole, landing as far on the other side as it started on this one, or nearer
    /// if there is a wall behind the ring. Does nothing and says false when the ring has no slice in the cat's section, is
    /// not seen head on, or when the cat's body would meet something on the way along the arc. The entities in
    /// <paramref name="ignored"/>, such as the toy that leads the cat, do not count as in the way; the ring itself always
    /// does, and an ID that names no entity makes the leap impossible, since the engine refuses such a query.
    /// </summary>
    public bool JumpThroughRing(string entity, string part, params string[] ignored)
    {
        var link = body.Link;
        ignored = [.. ignored.Where(other => other != entity)];
        if (link.SliceBounds(entity, part) is not { } ring || link.SlicePlaneNormal(entity, part, Axis3.X, Axis3.Y) is not { } axis) return false;
        var hole = ring.GetCenter();
        // The ring is head on when the line from the cat to the hole runs along its axis, from either side.
        var axisOnGround = Flat(axis).Normalized();
        var alignment = axisOnGround.Dot(Flat(hole - body.Position).Normalized());
        if (Mathf.Abs(alignment) < FacesCat) return false;
        var along = axisOnGround * Mathf.Sign(alignment);
        // The top of the arc is over the ring's plane, with the middle of the cat level with the middle of the hole.
        var ground = body.Position.Y;
        var rise = Mathf.Max(hole.Y - body.Height / 2 - ground, JumpRise);
        var level = new Vector3(hole.X, ground, hole.Z);
        // A wall behind the ring leaves less room to land: the leap is shortened until its path is free.
        foreach (var reach in Reaches)
        {
            if (!LeapIsClear(level, along, reach, rise, ignored)) continue;
            var start = level - along * reach;
            Func<double, bool>? jump = null;
            Begin(delta =>
            {
                if (jump is null)
                {
                    if (!body.WalkTo(start, body.RunSpeed)) return false;
                    // The run-up stops within a few centimetres of the start; the cat steps onto it, so that the leap is the one
                    // that was tested, and leaps from the next frame.
                    body.Warp(start);
                    jump = Jump(level + along * reach, rise);
                    return false;
                }
                return jump(delta);
            });
            return true;
        }
        return false;
    }

    // Whether the body gets along a leap that is `reach` long on each side of the ring: through the hole in the plane of
    // the ring, and past it on both sides in boxes along the arc, each as high as the arc is over its length. The feet
    // follow a parabola that is on the ground at both ends and `rise` above it over the ring.
    private bool LeapIsClear(Vector3 level, Vector3 along, float reach, float rise, string[] ignored)
    {
        var yaw = Mathf.Atan2(along.X, along.Z);
        var beyond = body.Radius + PathMargin;
        float Feet(float depth) => level.Y + rise * (1 - depth * depth / (reach * reach));
        bool Clear(float from, float to, float low, float high, float halfWidth) =>
            body.Link.IsClear(new Vector3(level.X, (low + high) / 2, level.Z) + along * (from + to) / 2, yaw,
                new(halfWidth, (high - low) / 2, (to - from) / 2), ignored);
        var apex = Feet(0);
        if (!Clear(-RingDepth, RingDepth, apex - PathMargin, apex + body.Height + PathMargin, beyond)) return false;
        foreach (var side in new[] { -1, 1 })
            for (var near = RingDepth; near < reach; near += LegLength)
            {
                // The body is up to its radius beyond its middle, which is in the leg.
                var far = Mathf.Min(near + LegLength, reach);
                var inner = Mathf.Max(near - beyond, RingDepth);
                var outer = far + beyond;
                var low = Mathf.Max(Feet(far), level.Y) + PathMargin;
                var high = Feet(near) + body.Height + PathMargin;
                if (!Clear(Mathf.Min(side * inner, side * outer), Mathf.Max(side * inner, side * outer), low, high, beyond)) return false;
            }
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
        var player = body.Player;
        body.Posture = Posture.Standing;
        body.Do(_ =>
        {
            // Crossing a seam takes both hands for the prism: a cat held when the player leaves its section is put down.
            if (!body.WithPlayer) LetGo();
            else body.Warp(player.Position + player.Basis * HeldAt);
            return false;
        });
        Held = true;
        player.PutDown = LetGo;
        EmitSignal(SignalName.PickedUp);
    }

    // The player puts the cat down, or the cat is let go for want of the player's section.
    private void LetGo()
    {
        Release();
        body.Stop();
    }

    // Any other command to the cat or its body ends the carrying, whoever gives it.
    private void Release()
    {
        if (!Held) return;
        Held = false;
        body.Player.PutDown = null;
        EmitSignal(SignalName.PutDown);
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
