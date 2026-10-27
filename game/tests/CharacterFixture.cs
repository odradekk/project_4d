using System;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Characters;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>The spawns and waits that the character checks share.</summary>
internal static class CharacterFixture
{
    internal const string CatScene = "res://characters/cat.tscn";
    internal const string TeacherScene = "res://characters/teacher.tscn";
    internal const string SchoolmateScene = "res://characters/schoolmate.tscn";

    // The physics runs at 60 frames per second.
    private const int FramesPerSecond = 60;

    /// <summary>Adds a creature spawn to a section of the test region, before the region is added to the tree.</summary>
    internal static void AddSpawn(Region3D region, string section, string id, string scene, Vector3 position,
        double radius, double height, bool seams = false, string name = "") =>
        region.GetNode($"Sections/{section}/Spawns").AddChild(new CreatureSpawn3D
        {
            Name = id,
            EntityId = id,
            Position = position,
            PassageRadius = radius,
            PassageHeight = height,
            CanUseSeamsWithoutPrism = seams,
            DisplayName = name,
            CharacterScene = GD.Load<PackedScene>(scene)
        });

    internal static void AddCat(Region3D region, string id, Vector3 position, string section = "Ⅰ") =>
        AddSpawn(region, section, id, CatScene, position, .15, .4, seams: true);

    internal static void AddSchoolmate(Region3D region, string id, Vector3 position, string name = "") =>
        AddSpawn(region, "Ⅰ", id, SchoolmateScene, position, .2, 1.35, name: name);

    internal static void AddTeacher(Region3D region, string id, Vector3 position, string section = "Ⅰ") =>
        AddSpawn(region, section, id, TeacherScene, position, .25, 1.7);

    /// <summary>Waits until the condition holds, at most the given seconds of game time.</summary>
    internal static async Task Until(this TurningFixture play, Func<bool> condition, double seconds, string message)
    {
        for (var frame = 0; !condition(); frame++)
        {
            Check(frame < seconds * FramesPerSecond, $"{message} (not within {seconds} s)");
            await play.Frames(1);
        }
    }

    internal static Task UntilIdle(this TurningFixture play, Character3D character, double seconds) =>
        play.Until(() => !character.Busy, seconds, $"{character.Name} must finish what it was told to do");

    internal static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    /// <summary>How far apart two points are on the ground plane.</summary>
    internal static float Apart(Vector3 a, Vector3 b) => Flat(a - b).Length();

    internal static Vector3 Forward(Node3D node) => -node.GlobalBasis.Z;
}
