using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Godot;

namespace Game.UI;

/// <summary>
/// The backpack's slots down the right edge of the screen, numbered as the keys that take things out.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class BackpackBar : VBoxContainer
{
    private readonly Label[] slots = Enumerable.Range(0, Backpack.Slots).Select(_ => new Label()).ToArray();

    /// <summary>The text of each slot.</summary>
    public IReadOnlyList<string> Texts => slots.Select(slot => slot.Text).ToArray();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = AnchorRight = 1;
        AnchorTop = AnchorBottom = .5f;
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Both;
        OffsetRight = -24;
        foreach (var slot in slots)
        {
            slot.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddThemeFontSizeOverride("font_size", 22);
            slot.AddThemeColorOverride("font_shadow_color", Colors.Black);
            slot.AddThemeConstantOverride("shadow_offset_x", 2);
            slot.AddThemeConstantOverride("shadow_offset_y", 2);
            AddChild(slot);
        }
        SetItems([]);
    }

    /// <summary>Shows the names of what is in the backpack, one to a slot, in order.</summary>
    internal void SetItems(IReadOnlyList<string> items)
    {
        for (var i = 0; i < slots.Length; i++)
            slots[i].Text = $"{i + 1}  {(i < items.Count ? items[i] : "—")}";
    }
}
