using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Creatures;
using Game.Rendering;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Plays the timelines of the region's grazers. Each frame it gives the engine the grazer's interpolated pose and body,
/// takes the bites that are due, and keeps the grazer's slice in the section of the main view and the section the lens
/// looks at equal to the engine's. A grazer stops and blocks nothing, so its slice has no collision, and its slice is
/// not outlined in the lens, which is for through-objects.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public sealed partial class GrazerChoreography : Node
{
    private sealed class Track(GrazerTimeline timeline)
    {
        internal GrazerTimeline Timeline { get; } = timeline;
        internal double Elapsed { get; set; }
        internal int Bitten { get; set; }
        internal bool Playing { get; set; } = true;
    }

    private readonly Dictionary<EntityId, Track> tracks = [];
    private readonly List<Node3D> drawn = [];
    private World world = null!;
    private EntityId actor;
    private PrismView prism = null!;
    private IReadOnlyList<SectionScene> scenes = [];
    private IReadOnlyDictionary<(EntityId, PartId), Material> materials = null!;
    private IReadOnlyDictionary<EntityId, GrazerBody> bodies = null!;
    private HashSet<SectionId> drawnSections = [];
    private bool changed = true;

    internal void Configure(World world, string actor, PrismView prism, IReadOnlyList<SectionScene> scenes,
        IReadOnlyDictionary<(EntityId, PartId), Material> materials, IReadOnlyDictionary<EntityId, GrazerBody> bodies)
    {
        this.world = world;
        this.actor = new(actor);
        this.prism = prism;
        this.scenes = scenes;
        this.materials = materials;
        this.bodies = bodies;
    }

    /// <summary>
    /// Starts the grazer on its timeline from the beginning, or again. The grazer stays where the timeline ends. ArgumentException
    /// for a grazer that is not in the region.
    /// </summary>
    public void Play(string grazer, GrazerTimeline timeline) => tracks[Known(grazer)] = new Track(timeline);

    /// <summary>Whether the grazer is still on its timeline.</summary>
    public bool IsPlaying(string grazer) => tracks.TryGetValue(Known(grazer), out var track) && track.Playing;

    /// <summary>The distance in four dimensions from the player to the grazer's body origin, as the engine measures it.</summary>
    public double Distance(string grazer) => world.Distance4(Origin(actor), Origin(Known(grazer)));

    public override void _Process(double delta) => Advance(delta);

    /// <summary>Moves every grazer on its timeline on by <paramref name="delta"/> seconds and brings the slices up to date.</summary>
    public void Advance(double delta)
    {
        foreach (var (id, track) in tracks.Where(entry => entry.Value.Playing).ToArray())
        {
            track.Elapsed = Math.Min(track.Elapsed + delta, track.Timeline.Duration);
            track.Playing = SetFrame(id, track) && TakeBites(id, track) && track.Elapsed < track.Timeline.Duration;
        }
        Draw();
    }

    private EntityId Known(string grazer) => bodies.ContainsKey(new(grazer))
        ? new(grazer)
        : throw new ArgumentException($"The region has no grazer {grazer}.", nameof(grazer));

    private Vec4 Origin(EntityId entity) => ((EntityReadResult.Found)world.ReadEntity(entity)).Entity.Pose.Position;

    private bool SetFrame(EntityId id, Track track)
    {
        var key = track.Timeline.At(track.Elapsed);
        var pose = new Pose4(key.Position, Rotation4.FromPlaneAngles(key.Turns));
        if (world.SetGrazerFrame(id, pose, bodies[id].At(key.Mouth)) is UpdateResult.Rejected rejected) return Stop(id, rejected.Failure);
        changed = true;
        return true;
    }

    private bool TakeBites(EntityId id, Track track)
    {
        var bites = track.Timeline.Bites;
        while (track.Bitten < bites.Count && bites[track.Bitten].Time <= track.Elapsed)
        {
            var bite = bites[track.Bitten++];
            if (world.ConsumeStructure(id, bite.Elements, bite.Seams) is UpdateResult.Rejected rejected) return Stop(id, rejected.Failure);
            // The engine has taken the elements whole: they have no slice, collision or support. Their nodes follow.
            foreach (var scene in scenes)
                foreach (var element in bite.Elements) scene.Geometry.GetNodeOrNull(element.Value)?.Free();
        }
        return true;
    }

    // A script the engine refuses is the author's mistake: say so once, and leave the grazer where its last good frame put it.
    private static bool Stop(EntityId grazer, Failure failure)
    {
        GD.PushError($"The timeline of grazer {grazer.Value} stopped: {failure.Code}: {failure.Detail}");
        return false;
    }

    // The sections that are looked at: the player's, and the one the raised lens shows. Only these are cut each frame.
    private HashSet<SectionId> Looked()
    {
        var sections = new HashSet<SectionId>();
        if (((EntityReadResult.Found)world.ReadEntity(actor)).Entity.Section is { } home) sections.Add(home);
        if (prism.Raised && prism.TargetSection is { } target) sections.Add(new(target));
        return sections;
    }

    private void Draw()
    {
        var sections = Looked();
        if (!changed && sections.SetEquals(drawnSections)) return;
        changed = false;
        drawnSections = sections;
        foreach (var node in drawn) node.Free();
        drawn.Clear();
        foreach (var scene in scenes.Where(scene => sections.Contains(scene.Id)))
        {
            var geometry = (SectionGeometryResult.Geometry)world.GetSectionGeometry(scene.Id);
            foreach (var slice in geometry.Entities.Where(slice => bodies.ContainsKey(slice.Entity)))
            {
                var node = SliceMesh.Entity(slice with { ThroughObject = false }, materials, scene.MaskIds, null);
                scene.Dynamic.AddChild(node);
                drawn.Add(node);
            }
        }
    }
}
