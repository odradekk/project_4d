using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Player;
using Game.Rendering;
using Game.UI;
using Godot;

namespace Game.Adapter;

/// <summary>
/// The player's way between sections: crossing a seam, taking things out of the backpack, and what changes in the
/// picture while she is a mirror image. The engine decides every crossing and keeps the backpack and her handedness;
/// this node moves her to where the engine puts her and shows the result.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public sealed partial class SeamTravel : Node
{
    // A click means the nearest seam within this distance of her feet; the engine says whether she is close enough to cross.
    private const float ApproachDistance = 2f;

    private World world = null!;
    private EntityId actor;
    private FirstPersonPlayer player = null!;
    private PrismView prism = null!;
    private PlayerActions actions = null!;
    private Action<string, string> enterSection = null!;
    private MirrorView mirror = null!;
    private BackpackBar bar = null!;
    private string?[] shownItems = [];

    internal Backpack Backpack { get; private set; } = null!;
    internal SeamDisplay Display { get; private set; } = null!;

    /// <summary>
    /// Whether a click now would try a seam that is open, within the reach of a click. Whether she is close enough to it to
    /// cross is the engine's to say.
    /// </summary>
    public bool CanCross => prism.Raised && Nearest() is { } seam && Display.IsUsable(seam);

    /// <param name="enterSection">Called with the section she left and the section she is in now, once she has crossed.</param>
    internal void Configure(World world, string actor, FirstPersonPlayer player, PrismView prism, PlayerActions actions,
        SeamDisplay display, Action<string, string> enterSection)
    {
        this.world = world;
        this.actor = new(actor);
        this.player = player;
        this.prism = prism;
        this.actions = actions;
        Display = display;
        this.enterSection = enterSection;
        Backpack = new(world, this.actor);
        mirror = new MirrorView { Name = "Mirror" };
        AddChild(mirror);
        bar = new BackpackBar { Name = "Backpack" };
        player.Hud.AddChild(bar);
        prism.MirrorBands = display.BandsIn;
        // After the actions have stepped the engine, so that the picture shows this frame's state.
        ProcessPhysicsPriority = 2;
    }

    public override void _PhysicsProcess(double delta)
    {
        Display.Refresh(actions.CurrentSection, player.Position);
        mirror.Flipped = prism.Flipped = actions.Mirrored;
        var items = Backpack.Contents.Select(item => item?.Value).ToArray();
        if (items.SequenceEqual(shownItems)) return;
        shownItems = items;
        bar.SetItems(items);
    }

    /// <summary>Left click: crosses the nearest seam to the section the lens shows. Null when nothing is tried or it worked.</summary>
    internal Refusal? Cross()
    {
        if (!prism.Raised || prism.TargetSection is not { } target || Nearest() is not { } seam) return null;
        // She holds the prism up to cross: the lens is raised.
        var result = world.CrossSeam(new(actor, new(seam.Id), new(target), true, Backpack.FreeSlots));
        if (result is CrossSeamResult.Rejected rejected) return actions.RefuseCrossing(rejected.Failure);
        var crossed = (CrossSeamResult.Crossed)result;
        // A mirror seam puts her on the other side of its plane, where the engine says she is.
        var position = State().Pose.Position;
        player.Position = new((float)position.X, (float)position.Y, (float)position.Z);
        enterSection(crossed.From.Value, crossed.To.Value);
        if (crossed.WedgedItem is not null) actions.Regrip();
        return null;
    }

    /// <summary>Takes the thing in a numbered slot out of the backpack into the hand. Null when the slot is empty or it worked.</summary>
    internal Refusal? TakeOut(int slot) => Backpack.Contents[slot] is { } item ? actions.Hold(item) : null;

    // The seam from the player's section to the lens's, nearest on the floor plan, if one is within reach of a click.
    private SeamMark? Nearest()
    {
        if (prism.TargetSection is not { } target) return null;
        return Display.Between(actions.CurrentSection, target)
            .Where(seam => Distance(seam) <= ApproachDistance)
            .OrderBy(Distance)
            .FirstOrDefault();
    }

    private float Distance(SeamMark seam) => new Vector2(player.Position.X - seam.Foot.X, player.Position.Z - seam.Foot.Z).Length();

    private EntityState State() => ((EntityReadResult.Found)world.ReadEntity(actor)).Entity;
}
