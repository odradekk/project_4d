using System.Collections.Generic;
using FourD;
using Game.Creatures;
using Godot;

namespace Game.Adapter;

// The grazers of a region.
// Decision note: 2026-10-26-region-scene-authoring
internal sealed partial class SceneReader
{
    internal readonly Dictionary<EntityId, GrazerBody> Grazers = [];

    private readonly List<(Source Source, GrazerDefinition Grazer, MetaballShape Open)> openShapes = [];

    private GrazerDefinition ReadGrazer(Grazer3D grazer)
    {
        var id = new EntityId(grazer.EntityId);
        Map(grazer, "EntityId", "Pose", grazer, grazer.Rotation4.Length == 0 ? "Transform" : "Rotation4");
        MapPrefix(grazer, "EntityId", "Shape", grazer, "Shape");
        for (var i = 0; i < (grazer.Shape?.Balls.Count ?? 0); i++)
            foreach (var suffix in new[] { "", ".Id", ".Center", ".Radius", ".Weight" })
                Map(grazer, "EntityId", $"Shape.Balls[{i}]{suffix}", grazer, $"Shape.Balls[{i}]{suffix}");
        var shut = Metaballs(grazer, grazer.Shape, "Shape");
        var open = grazer.OpenShape is null ? null : Metaballs(grazer, grazer.OpenShape, "OpenShape");
        if (open is not null && !GrazerBody.SameBalls(shut, open))
            Error(grazer, "OpenShape", "The open shape needs the same balls as Shape, with the same IDs in the same order.");
        Grazers[id] = new(shut, open);
        Materials[(id, new("body"))] = new StandardMaterial3D
        {
            AlbedoColor = new(.82f, .76f, .84f),
            EmissionEnabled = true,
            Emission = new(.82f, .76f, .84f),
            EmissionEnergyMultiplier = .3f
        };
        var definition = new GrazerDefinition(id, Pose(grazer, grazer.W, grazer.Rotation4), At(grazer, "EntityId"), shut);
        if (open is not null && GrazerBody.SameBalls(shut, open)) openShapes.Add((At(grazer, "OpenShape"), definition, open));
        return definition;
    }

    // The engine checks the shape a grazer starts with and no other. The open shape is tried once here, so that its numbers are
    // reported when the region loads and not when a mouth first opens; the shapes between it and the shut one are mixes of
    // the two, which the engine checks on the frame that uses them.
    private void TryOpenShapes(World world)
    {
        foreach (var (source, grazer, open) in openShapes)
        {
            if (world.SetGrazerFrame(grazer.Id, grazer.Pose, open) is UpdateResult.Rejected rejected)
                Errors.Add(new(LoadErrorCode.InvalidGeometry, source, grazer.Id, null, null, rejected.Failure.Detail, []));
            world.SetGrazerFrame(grazer.Id, grazer.Pose, grazer.Shape);
        }
    }

    private MetaballShape Metaballs(Grazer3D grazer, MetaballShapeResource? shape, string property)
    {
        if (shape is null)
        {
            Error(grazer, property, "Assign a metaball shape.");
            unavailableParts.Add(At(grazer, "EntityId.Shape.Balls"));
            return new([], 1);
        }
        var balls = new List<Metaball>();
        for (var i = 0; i < shape.Balls.Count; i++)
        {
            var ball = shape.Balls[i];
            var place = $"{property}.Balls[{i}]";
            if (ball is null)
            {
                // Left out, so the balls after it count one less in the engine's errors.
                Error(grazer, place, "A metaball resource is missing.");
                continue;
            }
            // A ball with a center that cannot be read still takes its place, so that the engine's errors name the right balls.
            balls.Add(new(new(ball.Id), Point(ball.Center, grazer, place + ".Center"), ball.Radius, ball.Weight));
        }
        return new(balls, shape.Threshold);
    }
}
