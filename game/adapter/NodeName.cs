using Godot;

namespace Game.Adapter;

internal static class NodeName
{
    /// <summary>
    /// The name a node gets when it is named after an ID. Godot replaces the characters a node name cannot hold, such as a dot,
    /// so the ID alone does not find the node that was named after it. The string extension that Godot's C# has for this removes
    /// those characters instead, so the name is asked of Godot itself.
    /// </summary>
    internal static string Of(string id)
    {
        var node = new Node { Name = id };
        var name = node.Name.ToString();
        node.Free();
        return name;
    }
}
