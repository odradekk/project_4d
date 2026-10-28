using System.Collections.Generic;
using Game.Characters;
using Godot;

namespace Game.Flow;

/// <summary>
/// What a fall out of the room comes to in scenes 8 and 9: she goes back to where she stood half a second before she left the
/// floor, and the cat likewise. Nothing in these rooms catches either of them, since where something is missing or eaten there is
/// no floor.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class SafeSpots(HomeScene home, Character3D cat)
{
    // Lower than this, below the floor of the room, is a fall.
    private const float FallLimit = -3;
    private const double SafeSeconds = .5;

    private readonly Trail player = new(), catTrail = new();

    /// <summary>Takes where they stand now as the place to go back to.</summary>
    internal void Start(double clock)
    {
        player.Set(home.Player.Position, clock);
        catTrail.Set(cat.Position, clock);
    }

    /// <summary>Puts the cat's place to go back to somewhere it was put by hand, such as a perch.</summary>
    internal void CatAt(Vector3 position, double clock) => catTrail.Set(position, clock);

    /// <summary>Call every physics frame with the scene's clock.</summary>
    internal void Recover(double clock)
    {
        var body = home.Player;
        if (body.IsOnFloor()) player.Stand(body.Position, clock);
        else if (body.Position.Y < FallLimit)
        {
            body.Position = player.Spot;
            body.Velocity = Vector3.Zero;
            home.Hud.Say("小满", "差点掉下去！", 2.5);
        }
        if (cat.IsOnFloor()) catTrail.Stand(cat.Position, clock);
        else if (cat.Position.Y < FallLimit) cat.PlaceAt(catTrail.Spot, cat.Rotation.Y);
    }

    // The places something stood on the floor in the last half second; the oldest is far enough back from an edge it fell over.
    private sealed class Trail
    {
        private readonly Queue<(double Time, Vector3 Position)> places = new();
        internal Vector3 Spot => places.Peek().Position;

        internal void Set(Vector3 position, double time)
        {
            places.Clear();
            places.Enqueue((time, position));
        }

        internal void Stand(Vector3 position, double time)
        {
            places.Enqueue((time, position));
            while (places.Count > 1 && time - places.Peek().Time > SafeSeconds) places.Dequeue();
        }
    }
}
