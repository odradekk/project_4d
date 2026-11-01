using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Interaction;
using Game.Regions;
using Godot;
using static Game.Regions.Bus3D.BusState;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The bus outside the compound's gate: called, it comes east along the north street, turns down the main road's lane
/// nearest the compound, pulls in to stop beside the sidewalk, folds its doors open and says it has arrived, then waits;
/// sent off, it folds them shut, moves off briskly and turns into the south street, where it is hidden. Along the whole
/// way it keeps clear of everything the city stands on the ground, and it turns smoothly, as a seated camera would see. E from the sidewalk on an open door says she boards;
/// E does nothing while the bus is away, opening or closing. A seated camera rides at the passenger's eyes and goes back
/// where it was when let go.
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class BusChecks : Node
{
    // The sidewalk's top and its edge; the motor lane the bus keeps to on the main road; the bus's half size; the
    // passenger's eyes on the bus.
    private const float Walk = -.1f, Edge = 84, LaneWest = 91, LaneEast = 98.5f, Half = 1.25f, End = 5.5f;
    // The most the bus may turn per metre it moves: 1° a frame at the 7 m/s and 60 frames a second it turns at. Its turns
    // about a 7 m centre take 8.2°/m; a step of 2° from one quarter-metre sample's direction to the next, in a frame that
    // moves it about 0.12 m, would be four times that. Measured per metre, it holds whatever the frame took.
    private const float TurnPerMetre = 1 / (7 / 60f);
    private static readonly Vector3 Eyes = new(-.8f, 2.02f, -1.3f);

    private TurningFixture play = null!;
    private Bus3D bus = null!;
    private Rect2[] obstacles = [];
    private int boardings, arrivals;
    private readonly List<Bus3D.BusState> states = [];
    private readonly List<Vector3> seen = [];
    private bool foldedPartway, seated;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/compound.tscn");
            bus = play.Region.Props("Ⅰ")!.GetNode<Bus3D>("Bus");
            obstacles = Obstacles(play.Region.Props("Ⅰ")!.GetNode<MeshInstance3D>("City/Boxes"));
            bus.Boarding += () => boardings++;
            bus.Arrived += () => arrivals++;
            var doorways = bus.GetChildren().OfType<Interactable3D>().ToArray();
            var leaves = bus.FindChildren("Leaf*", nameof(Node3D), true, false).Cast<Node3D>().ToArray();
            Check(doorways.Length == 2 && leaves.Length == 8, $"the bus has two doors of four leaves; it has {doorways.Length} doorways and {leaves.Length} leaves");
            Check(bus.State == Away && !bus.Visible, $"the bus starts away and hidden; it is {bus.State}, visible {bus.Visible}");
            await PressE(74.3f, "with no bus at the stop");

            bus.Arrive();
            states.Add(bus.State);
            await Follow(() => bus.State == Opening);
            Check(seen[0].X < 78 && seen[0].Z > -26.5f && seen[0].Z < -22, $"the bus comes into sight on the north street; it was first at {seen[0]}");
            Check(Fold(leaves) < .02f, "the doors are shut when the bus stops");
            await PressE(74.3f, "while the doors are opening");
            Check(arrivals == 0, "the bus does not say it has arrived before its doors are open");
            await Follow(() => bus.State == Waiting, leaves);
            Check(foldedPartway, "the doors fold open over time");
            Check(arrivals == 1, $"the bus says once that it has arrived when its doors are open; it said so {arrivals} times");
            var right = Corners().Min(c => c.X);
            Check(right >= Edge && right <= Edge + .5f, $"at the stop the bus's right side is within 0.5 m of the sidewalk; it is at x {right}");
            foreach (var doorway in doorways)
            {
                var z = doorway.GlobalPosition.Z;
                Check(z > 67 && z < 75, $"each door opens along the bus shelter (z 67 to 75); one is at z {z}");
            }
            Check(Fold(leaves) > .1f, $"every leaf folds into the bus; the least is {Fold(leaves)} m in");

            await play.Frames(180);
            Check(bus.State == Waiting, $"the bus waits with its doors open; it is {bus.State}");
            foreach (var doorway in doorways)
            {
                var before = boardings;
                await PressE(doorway.GlobalPosition.Z, "on an open door", "E  上车");
                Check(boardings == before + 1, $"E on the open door at z {doorway.GlobalPosition.Z} says she boards");
            }

            bus.Depart();
            var stop = bus.GlobalPosition;
            var moving = GetTree().CreateTimer(5, true, true);
            foldedPartway = false;
            await PressE(74.3f, "while the doors are closing");
            var camera = play.Player.Camera;
            var unseated = camera.Transform;
            bus.Seat(camera);
            seated = true;
            await Follow(() => moving.TimeLeft <= 0, leaves);
            var off = bus.GlobalPosition.DistanceTo(stop);
            Check(bus.State == Departing && off > 8, $"5 s after it is sent off the bus has moved off; it is {bus.State}, {off} m from the stop");
            bus.Unseat();
            seated = false;
            Check(camera.Transform.IsEqualApprox(unseated), $"the camera goes back where it was when let go; it is at {camera.Transform}");
            await Follow(() => bus.State == Away, leaves);
            Check(foldedPartway, "the doors fold shut over time");
            Check(seen[^1].X < 78 && seen[^1].Z > 205 && seen[^1].Z < 209.5f, $"the bus goes out of sight on the south street; it was last at {seen[^1]}");
            Bus3D.BusState[] order = [Arriving, Opening, Waiting, Closing, Departing, Away];
            Check(states.SequenceEqual(order), $"the bus goes {string.Join(", ", order)}; it went {string.Join(", ", states)}");
            await PressE(74.3f, "once the bus has gone");
            GD.Print("Bus checks passed: comes along the north street and down the motor lane, clear of the city, stops beside the sidewalk, folds its doors open, says it arrived, waits, takes E on open doors only, folds them shut, carries a seated camera and lets it go, moves off within 5 s and is hidden on the south street.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }

    // Plays on until done, frame by frame at the game's own speed, recording each state the bus enters and where it is
    // seen, and checking where it drives: clear of the city's obstacles, on the main road within the lane between its
    // corners and the stop, and in the streets' near halves; that it turns smoothly; and that a seated camera stays at the
    // passenger's eyes.
    private async Task Follow(Func<bool> done, Node3D[]? leaves = null)
    {
        var (front, at) = (bus.GlobalBasis.Z, bus.GlobalPosition);
        for (var frame = 0; !done(); frame++)
        {
            Check(frame < 60 * 60, $"the bus must go on; it is {bus.State} at {bus.GlobalPosition}");
            await play.Frames(1);
            var (turned, moved) = (Mathf.RadToDeg(front.AngleTo(bus.GlobalBasis.Z)), at.DistanceTo(bus.GlobalPosition));
            (front, at) = (bus.GlobalBasis.Z, bus.GlobalPosition);
            if (bus.Visible && moved > .01f)
                Check(turned / moved < TurnPerMetre, $"the bus turns smoothly; at {at} it turned {turned}° over {moved} m");
            if (bus.State != states[^1]) states.Add(bus.State);
            if (leaves is not null && Fold(leaves) is > .02f and < .1f) foldedPartway = true;
            if (seated) Check(play.Player.Camera.GlobalPosition.DistanceTo(bus.ToGlobal(Eyes)) < .01f, $"the seated camera rides at the passenger's eyes; it is at {play.Player.Camera.GlobalPosition}");
            if (!bus.Visible) continue;
            var corners = Corners();
            seen.Add(at);
            var hit = obstacles.FirstOrDefault(o => Overlaps(corners, o));
            Check(hit.Size == Vector2.Zero, $"the bus keeps clear of the city; at {at} it runs into the box from {hit.Position} to {hit.End} (x, z)");
            if (at.X > 88 && (at.Z is > -17 and < 30 || at.Z is > 110 and < 200))
                Check(corners.All(c => c.X >= LaneWest && c.X <= LaneEast), $"on the main road the bus keeps to the motor lane; it is at {at}");
            if (at.X < 80 && at.Z < 0) Check(corners.All(c => c.Z >= -26.5f && c.Z <= -22), $"on the north street the bus keeps to the eastbound half; it is at {at}");
            if (at.X < 80 && at.Z > 200) Check(corners.All(c => c.Z >= 205 && c.Z <= 209.5f), $"on the south street the bus keeps to the westbound half; it is at {at}");
        }
    }

    private Vector3[] Corners() =>
        [.. new[] { (-1, -1), (-1, 1), (1, 1), (1, -1) }.Select(c => bus.ToGlobal(new(c.Item1 * Half, 0, c.Item2 * End)))];

    // The footprints (x, z) of the city's boxes that would stand in the bus's way near its route: taller than 5 cm and
    // starting below its roof, 3 m up. The city merges its boxes into one surface per colour, 24 vertices to a box.
    private static Rect2[] Obstacles(MeshInstance3D boxes)
    {
        var mesh = (ArrayMesh)boxes.Mesh;
        var found = new List<Rect2>();
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            var vertices = (Vector3[])mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex];
            for (var i = 0; i < vertices.Length; i += 24)
            {
                var box = new Aabb(vertices[i], Vector3.Zero);
                for (var k = 1; k < 24; k++) box = box.Expand(vertices[i + k]);
                if (box.Size.Y > .05f && box.Position.Y < 3 && box.End.X > -20 && box.Position.X < 120 && box.End.Z > -50 && box.Position.Z < 230)
                    found.Add(new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z));
            }
        }
        return [.. found];
    }

    // Whether the bus's footprint, its corners in order around it, and a box's footprint overlap: no axis of either
    // separates them.
    private static bool Overlaps(Vector3[] corners, Rect2 box)
    {
        var bus = corners.Select(c => new Vector2(c.X, c.Z)).ToArray();
        Vector2[] other = [box.Position, new(box.End.X, box.Position.Y), box.End, new(box.Position.X, box.End.Y)];
        foreach (var axis in new[] { Vector2.Right, Vector2.Down, bus[1] - bus[0], bus[3] - bus[0] })
        {
            var (a, b) = (bus.Select(axis.Dot).ToArray(), other.Select(axis.Dot).ToArray());
            if (a.Max() <= b.Min() || b.Max() <= a.Min()) return false;
        }
        return true;
    }

    // How far into the bus the least folded leaf has turned, at the middle of its lower panel.
    private float Fold(Node3D[] leaves) =>
        leaves.Min(leaf => bus.ToLocal(leaf.GetChildren().OfType<MeshInstance3D>().First().GlobalPosition).X - (-Half + .05f));

    // From the sidewalk in front of the bus's doorways at z, E: with hint, it says she boards, otherwise it does nothing.
    private async Task PressE(float z, string when, string hint = "")
    {
        var before = boardings;
        play.LookAt(new(83.5f, Walk, z), new(Edge + .4f, 1.3f, z));
        await play.Frames(2);
        var shown = play.Player.Hud.HintText;
        Check(hint == "" ? !shown.StartsWith("E") : shown == hint, $"{when} the hint is \"{shown}\"");
        Press(Key.E);
        await play.Frames(2);
        if (hint == "") Check(boardings == before, $"{when} E does nothing; she boarded");
    }
}
