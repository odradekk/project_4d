using System;
using System.Collections.Generic;
using System.Linq;
using FourD;

namespace Game.Adapter;

/// <summary>
/// The player's backpack. The engine keeps what is in it; the game decides how many things fit.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class Backpack(World world, EntityId actor)
{
    /// <summary>How many small things fit. The engine takes the free slots from the game.</summary>
    internal const int Slots = 4;

    /// <summary>What is in the backpack, in the engine's order.</summary>
    internal IReadOnlyList<EntityId> Items => State(actor).CarriedItems.Where(item => State(item).InBackpack).ToArray();

    internal int FreeSlots => Math.Max(0, Slots - Items.Count);

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;
}
