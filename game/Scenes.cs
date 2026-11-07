using System.Collections.Generic;
using Godot;

namespace Game;

/// <summary>
/// Loads scenes by path and keeps each one until the game quits, so that a scene that is freed and loaded again is the same
/// scene, still loaded. Every scene that code loads comes from here.
/// </summary>
internal static class Scenes
{
    // Loaded again after it was freed, a scene would take its resources back from Godot's cache while the GC may have collected
    // their C# objects and not yet finalized them. Godot 4.7.2's C# bridge then loses track of those objects: a plain resource
    // logs "Handle is not initialized" each time it is referenced again, and a resource with a C# script reads as null
    // (godotengine/godot#112067). A kept scene keeps its resources referenced, so the GC cannot collect their C# objects.
    private static readonly Dictionary<string, PackedScene> loaded = [];

    internal static PackedScene Load(string path)
    {
        if (!loaded.TryGetValue(path, out var scene)) loaded[path] = scene = GD.Load<PackedScene>(path);
        return scene;
    }
}
