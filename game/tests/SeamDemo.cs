using System;
using FourD;
using Game.Adapter;
using Godot;

namespace Game.Tests;

/// <summary>
/// Opening state for the seam debug scene: two small things already in the backpack and the long pole in the hand,
/// beside a plain seam, a mirror seam and a severed one.
/// </summary>
public partial class SeamDemo : Node
{
    public override void _Ready()
    {
        var region = GD.Load<PackedScene>("res://regions/seam_test.tscn").Instantiate<Region3D>();
        region.Name = "Region";
        AddChild(region);
        var world = region.World!;
        var actor = new EntityId("xiaoman");
        foreach (var item in new[] { "rubber", "ruler" })
            if (world.PackEntity(actor, new(item), Backpack.Slots) is CarryResult.Rejected packing)
                throw new InvalidOperationException($"Cannot pack {item}: {packing.Failure.Detail}");
        if (world.HoldEntity(actor, new("pole")) is CarryResult.Rejected holding)
            throw new InvalidOperationException($"Cannot hold the pole: {holding.Failure.Detail}");
    }
}
