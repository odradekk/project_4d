using System;
using System.Collections.Generic;
using System.Linq;
using FourD;

namespace Game.Adapter;

/// <summary>
/// The player's backpack. The engine keeps what is in it; the game decides how many things fit and which slot each thing is in,
/// so that a thing keeps its number while it is inside.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class Backpack(World world, EntityId actor)
{
    /// <summary>How many small things fit. The engine takes the free slots from the game.</summary>
    internal const int Slots = 4;

    private readonly EntityId?[] slots = new EntityId?[Slots];

    /// <summary>
    /// What each slot holds, null when it is empty. A thing keeps its slot until it leaves the backpack, and a thing that comes
    /// in takes the first empty slot.
    /// </summary>
    internal IReadOnlyList<EntityId?> Contents
    {
        get
        {
            var inside = State(actor).CarriedItems.Where(item => State(item).InBackpack).ToList();
            for (var i = 0; i < Slots; i++)
                if (slots[i] is { } held && !inside.Contains(held)) slots[i] = null;
            foreach (var item in inside.Where(item => !slots.Contains(item)))
            {
                var free = Array.IndexOf(slots, null);
                if (free >= 0) slots[free] = item;
            }
            return slots.ToArray();
        }
    }

    internal int FreeSlots => Contents.Count(item => item is null);

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;
}
