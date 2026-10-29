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
/// looks at equal to the engine's. A grazer stops and blocks nothing, so its slice has no collision.
/// <para>
/// Building elements are bitten only through a timeline, which also takes them out of the section worlds: a script that
/// calls <c>ConsumeStructure</c> itself leaves the element's nodes standing, to be seen and walked into, in a world where
/// the engine has none. <see cref="Play"/> puts the grazer at the first key of the new timeline at once, so a timeline that
/// follows another starts where the last one ended.
/// </para>
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

        // The last key that was given to the engine, so that a grazer holding still is not given the same frame again.
        internal GrazerKey? Sent { get; set; }
    }

    private readonly Dictionary<EntityId, Track> tracks = [];
    private readonly HashSet<EntityId> bitten = [];
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
    /// Starts the grazer on its timeline from the beginning, or again. The grazer stays where the timeline ends.
    /// ArgumentException, with nothing started, for a grazer that is not in the region and for a bite of an element that is
    /// not a building element of the region or of a seam that is not in it.
    /// </summary>
    public void Play(string grazer, GrazerTimeline timeline)
    {
        var id = Known(grazer);
        foreach (var bite in timeline.Bites)
        {
            foreach (var element in bite.Elements.Where(element => !bitten.Contains(element)))
                if (world.ReadEntity(element) is not EntityReadResult.Found { Entity.Definition: BuildingElementDefinition })
                    throw new ArgumentException($"{element.Value} is not a building element of the region.", nameof(timeline));
            foreach (var seam in bite.Seams)
                if (world.ReadSeam(seam) is SeamReadResult.Missing)
                    throw new ArgumentException($"{seam.Value} is not a seam of the region.", nameof(timeline));
        }
        tracks[id] = new Track(timeline);
    }

    /// <summary>Whether the grazer is still on its timeline.</summary>
    public bool IsPlaying(string grazer) => tracks.TryGetValue(Known(grazer), out var track) && track.Playing;

    /// <summary>The distance in four dimensions from the player to the grazer's body origin, as the engine measures it.</summary>
    public double Distance(string grazer) => world.Distance4(Origin(actor), Origin(Known(grazer)));

    public override void _Process(double delta) => Advance(delta);

    /// <summary>Moves every grazer on its timeline on by <paramref name="delta"/> seconds and brings the slices up to date.</summary>
    public void Advance(double delta)
    {
        foreach (var (id, track) in tracks.Where(entry => entry.Value.Playing))
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
        if (track.Sent is { } sent && sent.Position == key.Position && sent.Mouth == key.Mouth && sent.Turns.SequenceEqual(key.Turns)) return true;
        var pose = new Pose4(key.Position, Rotation4.FromPlaneAngles(key.Turns));
        if (world.SetGrazerFrame(id, pose, bodies[id].At(key.Mouth)) is UpdateResult.Rejected rejected) return Stop(id, rejected.Failure);
        track.Sent = key;
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
            foreach (var element in bite.Elements)
            {
                bitten.Add(element);
                foreach (var scene in scenes)
                    if (scene.StaticNodes.Remove(element, out var node)) node.Free();
            }
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
            foreach (var grazer in bodies.Keys)
                if (world.GetSliceGeometry(grazer, scene.Id) is SliceGeometryResult.Geometry geometry)
                {
                    var node = SliceMesh.Entity(geometry.Slice, materials, scene.MaskIds, null);
                    scene.Dynamic.AddChild(node);
                    drawn.Add(node);
                }
    }
}
