using System;
using System.Collections.Generic;
using Game.Interaction;
using Godot;

namespace Game.Regions;

/// <summary>
/// The city bus that stops outside the gate. Called with <see cref="Arrive"/>, it comes from the north along the southbound
/// motor lane nearest the compound, pulls into the bicycle lane, stops at the bus stop with its right side 0.4 m from the
/// sidewalk, folds its two doors open and emits <see cref="ArrivedEventHandler">Arrived</see>; then it waits, and E on an
/// open door emits <see cref="BoardingEventHandler">Boarding</see>. Called with <see cref="Depart"/>, it folds its doors
/// shut, pulls back into the motor lane and speeds south into the fog, and is hidden 300 m past the stop. Away, it is
/// hidden. A passenger aboard is the caller's business: a camera under <see cref="PassengerSpot"/> rides along.
/// It is scenery that the engine does not know of, with no body that stops anything: she cannot walk off the sidewalk to
/// it. The node's y is the ground; the script sets its x, z and heading. Its local +z is its front and −x its right side.
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class Bus3D : Node3D
{
    public enum BusState { Away, Arriving, Opening, Waiting, Closing, Departing }

    [Signal] public delegate void ArrivedEventHandler();
    [Signal] public delegate void BoardingEventHandler();

    public BusState State { get; private set; }

    /// <summary>At a window seat on the left, facing the middle door, at a seated child's eye height; it moves with the bus.</summary>
    public Node3D PassengerSpot { get; private set; } = null!;

    // x of the bus's middle in the lane (x 91 to 94.75) and at the stop, where its right side is at 84.4; z of its middle
    // at the stop, with both doors along the bus shelter (z 67 to 75). It eases between the lane and the stop over Turn
    // metres of z on either side, and City3DBay.cs opens the green belt where it crosses it.
    private const double Lane = 92.875, Bay = 85.65, Stop = 70, Turn = 40;
    private const double From = 200, Gone = 300, Cruise = 11, Rate = 1, DoorTime = 1.2;  // m, m, m/s, m/s², s
    private const float Fold = 80 * Mathf.Pi / 180;

    private double z, speed, doors;  // doors: 0 shut, 1 open

    public override void _Ready()
    {
        Build();
        Visible = false;
    }

    public void Arrive()
    {
        if (State != BusState.Away) throw new InvalidOperationException($"the bus can only arrive while away; it is {State}");
        (z, State, Visible) = (Stop - From, BusState.Arriving, true);
        Place();
    }

    public void Depart()
    {
        if (State != BusState.Waiting) throw new InvalidOperationException($"the bus can only depart while waiting; it is {State}");
        State = BusState.Closing;
        SetDoorways(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (State)
        {
            case BusState.Arriving:
                // Braking at Rate for the last stretch, so it stops at the stop.
                z = Math.Min(Stop, z + Math.Min(Cruise, Math.Sqrt(2 * Rate * (Stop - z))) * delta);
                if (Stop - z < .001) (z, State) = (Stop, BusState.Opening);
                break;
            case BusState.Opening:
                doors = Math.Min(1, doors + delta / DoorTime);
                if (doors < 1) break;
                State = BusState.Waiting;
                SetDoorways(true);
                EmitSignal(SignalName.Arrived);
                break;
            case BusState.Closing:
                doors = Math.Max(0, doors - delta / DoorTime);
                if (doors == 0) (State, speed) = (BusState.Departing, 0);
                break;
            case BusState.Departing:
                speed = Math.Min(Cruise, speed + Rate * delta);
                z += speed * delta;
                if (z - Stop > Gone) (State, Visible) = (BusState.Away, false);
                break;
            default:
                return;
        }
        Place();
        foreach (var (first, second, along) in leaves)
        {
            first.Rotation = new(0, along * Fold * (float)doors, 0);
            second.Rotation = new(0, -2 * along * Fold * (float)doors, 0);
        }
    }

    // The bus's middle on its path, heading along it.
    private void Place()
    {
        var u = Math.Min(1, Math.Abs(z - Stop) / Turn);
        var slope = Math.Sign(z - Stop) * (Lane - Bay) * 6 * u * (1 - u) / Turn;
        Position = new((float)(Bay + (Lane - Bay) * u * u * (3 - 2 * u)), Position.Y, (float)z);
        Rotation = new(0, (float)Math.Atan(slope), 0);
    }

    private void SetDoorways(bool open)
    {
        foreach (var doorway in doorways) doorway.CollisionLayer = open ? PhysicsLayers.Pickups : 0;
    }

    // Each half of a folding door: the leaf on the hinge at the jamb, the leaf hinged to its far edge, and which way along
    // z the half runs from the jamb. Opening, the first leaf turns inward and the second twice as far back, so that its
    // far edge slides along the doorway to the jamb.
    private readonly List<(Node3D First, Node3D Second, int Along)> leaves = [];
    private readonly List<Interactable3D> doorways = [];
}
