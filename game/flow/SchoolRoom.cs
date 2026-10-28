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

    private const string SectionI = "Ⅰ";
    private const string SectionII = "Ⅱ";
    private const uint Solid = 1;
    // A chair stands .5 behind its desk, and upside down on the desk its seat is .12 behind the desk's middle.
    private const float ChairShift = .38f;
    private const double Same = 1e-6;

    /// <summary>How many seconds a walk may take.</summary>
    internal const double Patience = 25;
    // How far from the rail the pencil can have been taken and still count as standing in it.
    private const double RailReach = .05;

    private readonly Node3D chairsI;
    private readonly Node3D chairsII;
    private readonly TurnableObject3D pencil;
    private bool pencilPlaced;

    private SchoolRoom(Region3D region)
    {
        Region = region;
        Player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        Prism = region.GetNode<PrismView>("Generated/Prism");
        Teacher = region.Character("teacher");
        Deskmate = region.Character("deskmate");
        Classmates = region.GetNode("Generated/" + SectionI).GetChildren().OfType<Character3D>()
            .Where(character => character != Teacher && character != Deskmate).ToArray();
        var props = region.Props(SectionI)!;
        var fittings = props.GetNode("Fittings");
        Door = fittings.GetNode<Interactable3D>("Door");
        Broom = fittings.GetNode<Interactable3D>("Broom");
        Chalkboard = fittings.GetNode<Interactable3D>("Chalkboard");
        Rows = fittings.GetNode("Rows").GetChildren().OfType<Interactable3D>().ToArray();
        DoorIn = fittings.GetNode<Marker3D>("DoorIn").Position;
        DoorOut = fittings.GetNode<Marker3D>("DoorOut").Position;
        BoardText = props.GetNode<Label3D>("BoardText");
        chairsI = props.GetNode<Node3D>("Chairs");
        chairsII = region.Props(SectionII)!.GetNode<Node3D>("Chairs");
        Seat = region.GetNode<Node3D>("Sections/" + SectionI + "/Spawns/Xiaoman").Transform;
        pencil = region.GetNode<TurnableObject3D>("Sections/" + SectionI + "/Turnables/Stationery/PencilWindow");
    }

    internal Region3D Region { get; }
    internal FirstPersonPlayer Player { get; }
    internal PrismView Prism { get; }
    internal Character3D Teacher { get; }
    internal Character3D Deskmate { get; }

    /// <summary>The class apart from the teacher and the desk mate.</summary>
    internal IReadOnlyList<Character3D> Classmates { get; }
    internal Interactable3D Door { get; }
    internal Interactable3D Broom { get; }
    internal Interactable3D Chalkboard { get; }
    internal IReadOnlyList<Interactable3D> Rows { get; }
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
        var (position, way) = Way(window);
        return Math.Abs(position - way.Limits.Max) < Same;
    }

    /// <summary>Whether a window is somewhere between open and shut.</summary>
    internal bool IsHalfway(string window)
    {
        var (position, way) = Way(window);
        return position > way.Limits.Min + Same && position < way.Limits.Max - Same;
    }

    private (double Position, DeviceTranslation Way) Way(string window)
    {
        var state = State(window);
        return (state.DevicePosition!.Value, (DeviceTranslation)((DeviceDefinition)state.Definition).Motion);
    }

    /// <summary>Opens the door: nobody is stopped by it, and it cannot be locked.</summary>
    internal void OpenDoor()
    {
        Door.Visible = false;
        Door.CollisionLayer = 0;
    }

    internal void ShutDoor()
    {
        Door.Visible = true;
        Door.CollisionLayer = Solid;
    }

    /// <summary>Lets the crosshair find the shut door as something to lock, or stops it.</summary>
    internal void OfferLocking(bool on) => Door.CollisionLayer = on ? Solid | PhysicsLayers.Pickups : Solid;

    /// <summary>Lets the crosshair find a prop, or stops it.</summary>
    internal static void Offer(Interactable3D prop, bool on) => prop.CollisionLayer = on ? PhysicsLayers.Pickups : 0;

    /// <summary>
    /// Sends a character along a route. <paramref name="done"/> is told whether it got to the end. It is also called, once, when
    /// the character is given something else to do on the way, so that a scene never waits for a walk that is gone. A walk that a
    /// body in the way holds up for longer than <see cref="Patience"/> ends at the end of the route.
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

    /// <summary>Puts the chairs behind the desks of section Ⅰ.</summary>
    internal void ShowChairs() => chairsI.Visible = true;

    /// <summary>Turns the chairs of a row upside down on their desks, which is where section Ⅱ has all of them.</summary>
    internal void UpturnRow(int row)
    {
        foreach (var chair in chairsI.GetChildren().OfType<Node3D>().Where(chair => chair.Name.ToString().StartsWith($"ChairI{row}")))
            chair.Transform = chairsII.GetNode<Node3D>("ChairII" + chair.Name.ToString()[6..]).Transform;
    }

    /// <summary>Puts every chair of section Ⅱ on the floor behind its desk.</summary>
    internal void PutChairsOfSectionIIOnFloor()
    {
        foreach (var chair in chairsII.GetChildren().OfType<Node3D>())
        {
            var upside = chair.Transform.Origin;
            chair.Transform = new Transform3D(Basis.Identity, new Vector3(upside.X, 0, upside.Z + ChairShift));
        }
    }

    /// <summary>
    /// Keeps the pencil in the window rail standing there. Call it every frame until it is true. The player may have taken the
    /// pencil elsewhere or never stood it up, in which case it is laid back where it lay and stood up again.
    /// </summary>
    internal bool KeepWindowPencilStanding()
    {
        var world = Region.World!;
        var id = new EntityId(WindowPencil);
        var state = State(WindowPencil);
        if (state.Rotating) return false;
        if (state.Holder is not null)
        {
            world.ReleaseEntity(id);
            return false;
        }
        var lying = SceneReader.RelativeTransform(pencil, Region);
        var hinge = lying.Origin + lying.Basis * pencil.Interaction!.Hinge;
        var sectionW = Region.GetNode<Section3D>("Sections/" + SectionI).W;
        if (state.UprightOrigin is not null && SlicedAround(hinge, sectionW)) return true;
        if (!pencilPlaced)
        {
            pencilPlaced = true;
            var flat = SceneReader.PoseOf(lying, sectionW);
            if (world.PlaceEntity(id, flat) is UpdateResult.Rejected placed) return Fail($"The pencil cannot be put back in the window rail: {placed.Failure.Detail}");
        }
        if (world.PlanRotation(new StandUp(new(Actor), id, new(SectionII))) is not RotationPlanResult.Allowed plan)
            return Fail("The pencil in the window rail cannot stand up.");
        if (world.StartRotation(plan.Plan) is StartRotationResult.Rejected started)
            return Fail($"The pencil in the window rail cannot start to stand up: {started.Failure.Detail}");
        pencilPlaced = false;
        return false;
    }

    // Whether the pencil has a slice around the point in its section, which is where its plate is when it stands in the rail.
    private bool SlicedAround(Vector3 point, double sectionW)
    {
        var box = new OccupancyBox(SceneReader.PoseOf(new Transform3D(Basis.Identity, point), sectionW), new(RailReach, RailReach, RailReach, .006));
        return ((OccupancyResult.Occupancy)Region.World!.QueryOccupancy(box, [])).Hits.Any(hit => hit.Entity.Value == WindowPencil);
    }

    // The scene goes on without the pencil standing; the error tells a check that something is wrong.
    private static bool Fail(string message)
    {
        GD.PushError(message);
        return true;
    }
}
