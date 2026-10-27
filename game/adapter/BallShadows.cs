using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// The dotted patch under each four-dimensional ball, in every section the ball has a shape in, which shows how far the ball
/// hangs above what holds it. The engine says where the ball would come to rest and whether a section has anything there; a
/// section whose floor has a hole under the ball gets no patch.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class BallShadows
{
    private const uint MainAndLens = 3;
    // Half the thickness of the layer that an ordinary object lies in.
    private const double Layer = .006;
    private const double Probe = .02;
    private const float Lift = .004f;
    private const float DotSize = .16f;
    private const double Interval = .1;

    // What the last look found of a ball in a section: where its slice was and how wide, where the ball was, and whether the
    // section has something under it.
    private sealed record Look(float X, float Z, float Width, Vec4 Ball, bool Floor);

    private readonly World world;
    private readonly IReadOnlyList<SectionScene> scenes;
    private readonly IReadOnlyList<EntityId> balls;
    private readonly Dictionary<(SectionId, EntityId), MeshInstance3D> patches = [];
    private readonly Dictionary<(SectionId, EntityId), Look> looks = [];
    private int shownVersion = -1;
    private bool lookStale;
    private double wait;

    internal BallShadows(World world, IReadOnlyList<SectionScene> scenes, IEnumerable<EntityId> movable)
    {
        this.world = world;
        this.scenes = scenes;
        balls = movable.Where(id => State(id).Definition is ItemDefinition { Shape: Sphere4 }).ToArray();
        var mesh = DotPatch();
        foreach (var scene in scenes)
        {
            var shadows = new Node3D { Name = "Shadows" };
            scene.Overlay.AddChild(shadows);
            foreach (var ball in balls)
            {
                var patch = new MeshInstance3D { Name = ball.Value, Mesh = mesh, Layers = MainAndLens, Visible = false };
                patch.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                shadows.AddChild(patch);
                patches.Add((scene.Id, ball), patch);
            }
        }
    }

    /// <summary>
    /// Moves the patches under their balls on every call after the world has changed, given its version from the dynamic slices.
    /// Where a ball's slice is and whether a section has something under it are asked of the engine at most every Interval
    /// seconds, since each question takes some milliseconds; in between, a patch moves as far as its ball has.
    /// </summary>
    internal void Refresh(int version, double delta)
    {
        wait = Math.Max(0, wait - delta);
        var moved = version != shownVersion;
        shownVersion = version;
        lookStale |= moved;
        var look = lookStale && wait <= 0;
        if (!moved && !look) return;
        if (look)
        {
            lookStale = false;
            wait = Interval;
        }
        foreach (var ball in balls)
        {
            var rest = world.FindSupport(ball) as SupportResult.Supported;
            foreach (var scene in scenes) Place(patches[(scene.Id, ball)], ball, scene, rest, look);
        }
    }

    // The patch lies on the surface that holds the ball, as wide as the ball is in this section, when that section has
    // something to lie on right there, as the last look found.
    private void Place(MeshInstance3D patch, EntityId ball, SectionScene scene, SupportResult.Supported? rest, bool look)
    {
        var key = (scene.Id, ball);
        var position = State(ball).Pose.Position;
        if (look && rest is not null) looks[key] = LookAt(ball, scene, rest, position);
        else if (look) looks.Remove(key);
        if (rest is null || !looks.TryGetValue(key, out var seen) || !seen.Floor)
        {
            patch.Visible = false;
            return;
        }
        var surface = rest.Contacts.Max(contact => contact.Point.Y);
        patch.Position = new(seen.X + (float)(position.X - seen.Ball.X), (float)surface + Lift, seen.Z + (float)(position.Z - seen.Ball.Z));
        patch.Scale = new(seen.Width / 2, 1, seen.Width / 2);
        patch.Visible = true;
    }

    private Look LookAt(EntityId ball, SectionScene scene, SupportResult.Supported rest, Vec4 position)
    {
        if (world.GetSliceBounds(ball, scene.Id) is not SliceBoundsResult.Bounds box) return new(0, 0, 0, position, false);
        var x = (float)(box.Min.X + box.Max.X) / 2;
        var z = (float)(box.Min.Z + box.Max.Z) / 2;
        var surface = rest.Contacts.Max(contact => contact.Point.Y);
        var below = new OccupancyBox(new(new(x, surface - Probe, z, scene.W), SceneReader.Identity), new(Probe, Probe, Probe, Layer));
        var floor = world.QueryOccupancy(below, [ball]) is OccupancyResult.Occupancy { Hits.Count: > 0 };
        return new(x, z, (float)(box.Max.X - box.Min.X), position, floor);
    }

    private EntityState State(EntityId id) => ((EntityReadResult.Found)world.ReadEntity(id)).Entity;


    // Dots on rings of radius 0, .5 and 1 around the center, as flat squares facing up.
    private static ArrayMesh DotPatch()
    {
        var vertices = new List<Vector3>();
        var half = DotSize / 2;
        for (var ring = 0; ring <= 2; ring++)
        {
            var count = ring * 6;
            for (var i = 0; i < Mathf.Max(count, 1); i++)
            {
                var angle = Mathf.Tau * i / Mathf.Max(count, 1);
                var center = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * .5f);
                Vector3 a = center + new Vector3(-half, 0, -half), b = center + new Vector3(half, 0, -half);
                Vector3 c = center + new Vector3(half, 0, half), d = center + new Vector3(-half, 0, half);
                vertices.AddRange([a, b, c, a, c, d]);
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(.08f, .08f, .1f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        return mesh;
    }
}
