using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Flow;

/// <summary>
/// The school region as scenes 4 and 5 use it: the door, the chairs, the characters that leave, and the pencil in the
/// window rail that scene 4 leaves for scene 5. The scenes of the day play in one region, so each finds what the one
/// before left in the region's nodes and in the engine's world.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class SchoolRoom
{
    internal const string RegionPath = "res://regions/school.tscn";
    internal const string Actor = "xiaoman";
    internal const string WindowPencil = "pencilWindow";
    internal static readonly string[] Windows = ["sash1", "sash2", "sash3", "sash4"];

    /// <summary>How many seconds a walk may take.</summary>
    internal const double Patience = 25;

    private const string SectionI = "Ⅰ";
    private const string SectionII = "Ⅱ";
    private const string PushWindow = "推窗";
    private const uint Solid = 1;
    private const int DeskRows = 8;
    private const int DeskColumns = 6;
    // A chair stands .5 behind its desk, and upside down on the desk its seat is 1.23 up and .12 behind the desk's middle.
    private const float ChairBehind = .5f;
    private const float UpsideDownHeight = 1.23f;
    private const float UpsideDownBehind = .12f;
    private const double Same = 1e-6;

    private readonly PackedScene chair = GD.Load<PackedScene>("res://regions/school/chair.tscn");

    // The desks of section Ⅰ, by number, whose chairs are upside down on them. The room remembers it, since a row may be
    // turned over in two goes.
    private readonly HashSet<string> upturned = [];

    private SchoolRoom(Region3D region)
    {
        Region = region;
        Player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        Player.DevicePrompts = Windows.ToDictionary(window => window, _ => PushWindow);
        Prism = region.GetNode<PrismView>("Generated/Prism");
        Teacher = region.Character("teacher");
        Deskmate = region.Character("deskmate");
        Classmates = region.GetNode("Generated/" + SectionI).GetChildren().OfType<Character3D>()
            .Where(character => character != Teacher && character != Deskmate).ToArray();
        var props = region.Props(SectionI)!;
        var fittings = props.GetNode("Fittings");
        Door = fittings.GetNode<StaticBody3D>("Door");
        DoorLock = fittings.GetNode<Interactable3D>("DoorLock");
        Broom = fittings.GetNode<Interactable3D>("Broom");
        Chalkboard = fittings.GetNode<Interactable3D>("Chalkboard");
        RowBoxes = fittings.GetNode("Rows").GetChildren().OfType<Interactable3D>().ToArray();
        DoorIn = fittings.GetNode<Marker3D>("DoorIn").Position;
        DoorOut = fittings.GetNode<Marker3D>("DoorOut").Position;
        BoardText = props.GetNode<Label3D>("BoardText");
        Seat = region.GetNode<Node3D>("Sections/" + SectionI + "/Spawns/Xiaoman").Transform;
    }

    internal Region3D Region { get; }
    internal FirstPersonPlayer Player { get; }
    internal PrismView Prism { get; }
    internal Character3D Teacher { get; }
    internal Character3D Deskmate { get; }

    /// <summary>The class apart from the teacher and the desk mate.</summary>
    internal IReadOnlyList<Character3D> Classmates { get; }
    internal StaticBody3D Door { get; }

    /// <summary>What the crosshair finds the shut door by when it is to be locked, in front of the door.</summary>
    internal Interactable3D DoorLock { get; }
    internal Interactable3D Broom { get; }
    internal Interactable3D Chalkboard { get; }

    /// <summary>The boxes that the crosshair finds a row of chairs by, front to back.</summary>
    internal IReadOnlyList<Interactable3D> RowBoxes { get; }
    internal Vector3 DoorIn { get; }
    internal Vector3 DoorOut { get; }
    internal Label3D BoardText { get; }

    /// <summary>Where Xiaoman sits, as her spawn has it.</summary>
    internal Transform3D Seat { get; }

    /// <summary>Loads the region into <paramref name="host"/>.</summary>
    internal static SchoolRoom Open(Node host)
    {
        var region = GD.Load<PackedScene>(RegionPath).Instantiate<Region3D>();
        region.Name = "Region";
        host.AddChild(region);
        return Of(region);
    }

    internal static SchoolRoom Of(Region3D region)
    {
        if (!region.Loaded) throw new InvalidOperationException($"The school region did not load:\n{region.Diagnostics}");
        return new SchoolRoom(region);
    }

    internal EntityState State(string entity) =>
        ((EntityReadResult.Found)Region.World!.ReadEntity(new EntityId(entity))).Entity;

    /// <summary>Whether the entity has been flipped and has stopped turning.</summary>
    internal bool IsFlipped(string entity) => State(entity) is { Rotating: false, Handedness: Handedness.Mirrored };

    /// <summary>Whether the entity stands in W, as a thin plate in its section, and has stopped turning.</summary>
    internal bool IsUpright(string entity) => State(entity) is { Rotating: false, UprightOrigin: not null };

    internal bool IsLying(string entity) => State(entity) is { Rotating: false, UprightOrigin: null };

    /// <summary>Whether a window has been pushed all the way shut. A window starts open, at the low end of its way.</summary>
    internal bool IsShut(string window)
    {
        var state = State(window);
        var way = (DeviceTranslation)((DeviceDefinition)state.Definition).Motion;
        return Math.Abs(state.DevicePosition!.Value - way.Limits.Max) < Same;
    }

    /// <summary>Opens the door: nobody is stopped by it.</summary>
    internal void OpenDoor()
    {
        Door.Visible = false;
        Door.CollisionLayer = 0;
        DoorLock.CollisionLayer = 0;
    }

    /// <summary>Shuts the door, which stops her. The crosshair finds it only when it is to be locked.</summary>
    internal void ShutDoor(bool lockable = false)
    {
        Door.Visible = true;
        Door.CollisionLayer = Solid;
        DoorLock.CollisionLayer = lockable ? PhysicsLayers.Pickups : 0;
    }

    /// <summary>Lets the crosshair find a prop, or stops it.</summary>
    internal static void Offer(Interactable3D prop, bool on) => prop.CollisionLayer = on ? PhysicsLayers.Pickups : 0;

    /// <summary>
    /// Sends a character along a route. <paramref name="done"/> is told whether it got to the end. It is also called, once, when
    /// the character is given something else to do on the way, so that a scene never waits for a walk that is gone. A walk
    /// that takes longer than <see cref="Patience"/>, held up by a body in the way, ends at the end of the route.
    /// </summary>
    internal static void Walk(Character3D who, IReadOnlyList<Vector3> route, bool run, Action<bool> done)
    {
        void Finished() => Over(true);
        void Replaced() => Over(false);
        void Over(bool arrived)
        {
            who.ActionFinished -= Finished;
            who.ActionReplaced -= Replaced;
            done(arrived);
        }
        who.Posture = Posture.Standing;
        var next = 0;
        var waited = 0.0;
        who.Do(delta =>
        {
            if (who.WalkTo(route[next], run ? who.RunSpeed : who.WalkSpeed)) next++;
            if (next < route.Count && (waited += delta) > Patience) next = route.Count;
            if (next == route.Count) who.PlaceAt(route[^1], who.Rotation.Y);
            return next == route.Count;
        });
        // After the walk has been given, so that what it replaces is not taken for something that replaces the walk.
        who.ActionFinished += Finished;
        who.ActionReplaced += Replaced;
    }

    /// <summary>Puts a character back at its desk, sitting.</summary>
    internal static void SitDown(Character3D who, Transform3D place)
    {
        who.Visible = true;
        who.PlaceAt(place.Origin, place.Basis.GetEuler().Y);
        who.Sit();
    }

    /// <summary>Puts a character outside the door, out of sight, where nothing of the room can push it.</summary>
    internal void SendHome(Character3D who)
    {
        who.Visible = false;
        who.PlaceAt(DoorOut, 0);
    }

    /// <summary>Puts a chair behind every desk of section Ⅰ, on the floor.</summary>
    internal void PutChairsOnFloor() => Require(Add(SectionI, Desks("deskI").Select(desk =>
        ($"chairI{desk.Number}", new Transform3D(Basis.Identity, desk.At + new Vector3(0, 0, ChairBehind))))), "A chair cannot be put on the floor.");

    /// <summary>
    /// Turns the chairs of a row upside down on their desks, which is where section Ⅱ has all of them. A chair goes up before
    /// its place on the floor is given up, so that one the engine refuses, because something lies where it would stand, stays
    /// on the floor. Returns the numbers of the desks whose chairs stay.
    /// </summary>
    internal IReadOnlyList<string> UpturnRow(int row)
    {
        var half = new Basis(new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1));
        var desks = Desks("deskI").Where(desk => desk.Number.StartsWith(row.ToString()) && !upturned.Contains(desk.Number)).ToArray();
        var refused = Add(SectionI, desks.Select(desk => ($"chairI{desk.Number}U", new Transform3D(half, desk.At + new Vector3(0, UpsideDownHeight, UpsideDownBehind)))));
        Require(refused.Where(chair => chair.Code != FailureCode.TranslationBlocked).ToArray(), "A chair cannot be turned upside down.");
        var stays = desks.Where(desk => refused.Any(chair => chair.Id == $"chairI{desk.Number}U")).Select(desk => desk.Number).ToArray();
        foreach (var desk in desks.Where(desk => !stays.Contains(desk.Number)))
        {
            Require(Region.RemoveFixed($"chairI{desk.Number}"), $"The chair {desk.Number} of section Ⅰ cannot be taken away.");
            upturned.Add(desk.Number);
        }
        return stays;
    }

    /// <summary>Puts every chair of section Ⅱ, which stand on their desks, on the floor behind them.</summary>
    internal void PutChairsOfSectionIIOnFloor()
    {
        var desks = Desks("deskII").ToArray();
        Require(Add(SectionII, desks.Select(desk => ($"chairII{desk.Number}F", new Transform3D(Basis.Identity, desk.At + new Vector3(0, 0, ChairBehind))))), "A chair cannot be put on the floor.");
        foreach (var desk in desks) Require(Region.RemoveFixed($"chairII{desk.Number}"), $"The chair {desk.Number} of section Ⅱ cannot be taken away.");
    }

    /// <summary>
    /// Puts the pencil on the sill back where it lay whenever it has been moved from there while it lies: picked up, or flipped
    /// across the inner face of the sill into the room, where it would fall to the floor. Taken out of the window it would
    /// not come back.
    /// </summary>
    internal void KeepWindowPencilOnSill()
    {
        var state = State(WindowPencil);
        if (state.Rotating || state.UprightOrigin is not null || (state.Holder is null && state.Pose == state.Definition.Pose)) return;
        var id = new EntityId(WindowPencil);
        if (state.Holder is not null) Region.World!.ReleaseEntity(id);
        if (Region.World!.PlaceEntity(id, state.Definition.Pose) is UpdateResult.Rejected rejected)
            GD.PushError($"The pencil cannot be put back on the sill: {rejected.Failure.Detail}");
    }

    /// <summary>Stands the pencil in the window rail up if it lies there, as scene 4 leaves it.</summary>
    internal void StandWindowPencilIfLying()
    {
        if (!IsLying(WindowPencil)) return;
        var world = Region.World!;
        if (world.PlanRotation(new StandUp(new(Actor), new(WindowPencil), new(SectionII))) is not RotationPlanResult.Allowed plan
            || world.StartRotation(plan.Plan) is StartRotationResult.Rejected)
            GD.PushError("The pencil in the window rail cannot stand up.");
    }

    private IReadOnlyList<(string Id, FailureCode Code)> Add(string section, IEnumerable<(string Id, Transform3D At)> chairs) =>
        Region.AddFixed(chair, section, chairs);

    // The desks of a section by the number of their row and column, and where each stands on the floor; the extra desk is 49.
    private IEnumerable<(string Number, Vector3 At)> Desks(string prefix)
    {
        var numbers = Enumerable.Range(0, DeskRows * DeskColumns).Select(n => $"{n / DeskColumns}{n % DeskColumns}");
        foreach (var number in prefix == "deskII" ? numbers.Append("49") : numbers)
        {
            var position = State(prefix + number).Pose.Position;
            yield return (number, new((float)position.X, (float)position.Y, (float)position.Z));
        }
    }

    // A refusal means that the room is laid out otherwise than the scene expects, which a check of the scene catches by the error.
    private static void Require(FailureCode? refused, string message)
    {
        if (refused is { } code) GD.PushError($"{message} ({code})");
    }

    private static void Require(IReadOnlyList<(string Id, FailureCode Code)> refused, string message)
    {
        foreach (var (id, code) in refused) GD.PushError($"{message} {id}: {code}");
    }
}
