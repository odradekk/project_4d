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
/// The bus outside the compound's gate: called, it comes down the motor lane, pulls in to stop beside the sidewalk, folds
/// its doors open and says it has arrived, then waits; sent off, it folds them shut and leaves south until it is hidden.
/// E from the sidewalk on an open door says she boards; E does nothing while the bus is away, opening or closing.
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class BusChecks : Node
{
    // The sidewalk's top and its edge; the motor lane the bus keeps to away from the stop; the bus's half size.
    private const float Walk = -.1f, Edge = 84, LaneWest = 91, LaneEast = 98.5f, Half = 1.25f, End = 5.5f;

    private TurningFixture play = null!;
    private Bus3D bus = null!;
    private int boardings, arrivals;
    private readonly List<Bus3D.BusState> states = [];
    private bool foldedPartway;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/compound.tscn");
            bus = play.Region.Props("Ⅰ")!.GetNode<Bus3D>("Bus");
            bus.Boarding += () => boardings++;
            bus.Arrived += () => arrivals++;
            var doorways = bus.GetChildren().OfType<Interactable3D>().ToArray();
            var leaves = bus.FindChildren("Leaf*", nameof(Node3D), true, false).Cast<Node3D>().ToArray();
            Check(doorways.Length == 2 && leaves.Length == 8, $"the bus has two doors of four leaves; it has {doorways.Length} doorways and {leaves.Length} leaves");
            Check(bus.State == Away && !bus.Visible, $"the bus starts away and hidden; it is {bus.State}, visible {bus.Visible}");
            await PressE(74.3f, "with no bus at the stop");

            bus.Arrive();
            states.Add(bus.State);
            await Follow(Opening);
            Check(Fold(leaves) < .02f, "the doors are shut when the bus stops");
            await PressE(74.3f, "while the doors are opening");
            Check(arrivals == 0, "the bus does not say it has arrived before its doors are open");
            await Follow(Waiting, leaves);
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
            foldedPartway = false;
            await PressE(74.3f, "while the doors are closing");
            await Follow(Away, leaves);
            Check(foldedPartway, "the doors fold shut over time");
            Check(!bus.Visible && bus.GlobalPosition.Z > 70 + 300, $"the bus is hidden more than 300 m past the stop; it is at {bus.GlobalPosition}, visible {bus.Visible}");
            Bus3D.BusState[] order = [Arriving, Opening, Waiting, Closing, Departing, Away];
            Check(states.SequenceEqual(order), $"the bus goes {string.Join(", ", order)}; it went {string.Join(", ", states)}");
            await PressE(74.3f, "once the bus has gone");
            GD.Print("Bus checks passed: arrives along the motor lane, stops beside the sidewalk, folds its doors open, says it arrived, waits, takes E on open doors only, folds them shut, leaves south and is hidden 300 m on.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Engine.TimeScale = 1;
        }
    }

    // Plays on until the bus is in the given state, quickly while it drives, recording each state it enters and checking
    // where it drives: never over the sidewalk, and within the motor lane away from the stop.
    private async Task Follow(Bus3D.BusState until, Node3D[]? leaves = null)
    {
        for (var frame = 0; bus.State != until; frame++)
        {
            Check(frame < 60 * 60, $"the bus must reach {until}; it is {bus.State} at {bus.GlobalPosition}");
            Engine.TimeScale = bus.State is Arriving or Departing ? 8 : 1;
            await play.Frames(1);
            if (bus.State != states[^1]) states.Add(bus.State);
            if (leaves is not null && Fold(leaves) is > .02f and < .1f) foldedPartway = true;
            if (!bus.Visible) continue;
            var corners = Corners();
            Check(corners.All(c => c.X >= Edge), $"the bus keeps off the sidewalk; it is at {bus.GlobalPosition}");
            if (Mathf.Abs(bus.GlobalPosition.Z - 70) > 46)
                Check(corners.All(c => c.X >= LaneWest && c.X <= LaneEast), $"away from the stop the bus keeps to the motor lane; it is at {bus.GlobalPosition}");
        }
        Engine.TimeScale = 1;
    }

    private Vector3[] Corners() =>
        [.. new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) }.Select(c => bus.ToGlobal(new(c.Item1 * Half, 0, c.Item2 * End)))];

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
