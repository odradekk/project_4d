namespace Game;

/// <summary>The physics layers that more than one part of the game agrees on, as the bit values the scenes give them.</summary>
internal static class PhysicsLayers
{
    /// <summary>The bodies of characters: nothing that walks collides with them, and the crosshair looks through them.</summary>
    internal const uint Characters = 2;

    /// <summary>The spheres characters are picked up by: the crosshair finds them, and what looks for things that hide a seam does not.</summary>
    internal const uint Pickups = 4;
}
