using System;
using System.Collections.Generic;
using Game.Interaction;
using Godot;

namespace Game.Regions;

/// <summary>
/// The city bus that stops outside the gate. Called with <see cref="Arrive"/>, it comes east along the north street from
/// behind the compound, turns right onto the main road's southbound lane nearest the compound, pulls into the bicycle lane,
/// stops at the bus stop with its right side 0.4 m from the sidewalk, folds its two doors open and emits
/// <see cref="ArrivedEventHandler">Arrived</see>; then it waits, and E on an open door emits
/// <see cref="BoardingEventHandler">Boarding</see>. Called with <see cref="Depart"/>, it folds its doors shut, pulls back
/// into the lane, drives south, turns right into the south street and is hidden once the compound's buildings stand
/// between it and the sidewalk. Away, it is hidden. <see cref="Seat"/> holds a camera at a passenger's eyes.
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

    // x of the bus's middle in the main road's southbound lane nearest the compound (x 91 to 94.75) and at the stop, where
    // its right side is at 84.4; z of its middle at the stop, with both doors along the bus shelter (z 67 to 75). It eases
    // between the lane and the stop over Shift metres of z on either side, and City3DBay.cs opens the green belt there.
    // It drives in the north street's eastbound half (z −26.5 to −22) and the south street's westbound half (z 205 to
    // 209.5), and turns between them and the main road about a centre Corner metres from its path.
    private const float Lane = 92.875f, Bay = 85.65f, Stop = 70, Shift = 40, North = -24.25f, South = 207.25f, Corner = 7;
    // Where it starts on the north street and is hidden on the south street: x of its middle, with the compound's
    // north-east and south-east buildings between it and the sidewalk. Hidden is also past the lane between the last two
    // columns of buildings, which looks down to the south street from the stretch of the road to the gate she walks home on.
    private const float From = 20, Hidden = 40;
    private const float Cruise = 12, Turning = 7, Accel = 1.5f, Brake = 1.8f, DoorTime = 1.2f;  // m/s, m/s², s
    private const float Fold = 80 * Mathf.Pi / 180;

    private static readonly BusRoute In = new BusRoute(new(From, North), Cruise, Turning, Brake)
        .Line(new(Lane - Corner, North)).Turn(new(Lane - Corner, North + Corner), -Mathf.Pi / 2, 0)
        .Line(new(Lane, Stop - Shift)).Ease(new(Bay, Stop)).End(0);
    private static readonly BusRoute Out = new BusRoute(new(Bay, Stop), Cruise, Turning, Brake)
        .Ease(new(Lane, Stop + Shift)).Line(new(Lane, South - Corner)).Turn(new(Lane - Corner, South - Corner), 0, Mathf.Pi / 2)
        .Line(new(Hidden, South)).End(Cruise);

    private float s, speed, doors;  // along the route; doors: 0 shut, 1 open
    private RemoteTransform3D? seat;
    private Transform3D unseated;

    public override void _Ready()
    {
        Build();
        Visible = false;
    }

    public void Arrive()
    {
        if (State != BusState.Away) throw new InvalidOperationException($"the bus can only arrive while away; it is {State}");
        (s, speed, State, Visible) = (0, Cruise, BusState.Arriving, true);
        Place(In);
    }

    public void Depart()
    {
        if (State != BusState.Waiting) throw new InvalidOperationException($"the bus can only depart while waiting; it is {State}");
        State = BusState.Closing;
        SetDoorways(false);
    }

    /// <summary>
    /// Holds <paramref name="camera"/> at a passenger's eyes, on the seat behind the middle door, facing ahead and toward the
    /// door and the window, and moves it with the bus until <see cref="Unseat"/>. The caller stops her looking about and
    /// using things meanwhile.
    /// </summary>
    public void Seat(Camera3D camera)
    {
        if (seat is not null) throw new InvalidOperationException("a camera is already seated on the bus");
        unseated = camera.Transform;
        passengerSpot.AddChild(seat = new RemoteTransform3D());
        seat.RemotePath = seat.GetPathTo(camera);
    }

    /// <summary>Lets the seated camera go, back where it was in its parent when it was seated.</summary>
    public void Unseat()
    {
        if (seat is null) throw new InvalidOperationException("no camera is seated on the bus");
        var camera = seat.GetNode<Camera3D>(seat.RemotePath);
        seat.Free();
        seat = null;
        camera.Transform = unseated;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        switch (State)
        {
            case BusState.Arriving:
                Drive(In, dt);
                if (In.Length - s < .001f) (s, State) = (In.Length, BusState.Opening);
                Place(In);
                break;
            case BusState.Opening:
                doors = Math.Min(1, doors + dt / DoorTime);
                if (doors < 1) break;
                State = BusState.Waiting;
                SetDoorways(true);
                EmitSignal(SignalName.Arrived);
                break;
            case BusState.Closing:
                doors = Math.Max(0, doors - dt / DoorTime);
                if (doors == 0) (State, s, speed) = (BusState.Departing, 0, 0);
                break;
            case BusState.Departing:
                Drive(Out, dt);
                Place(Out);
                if (s >= Out.Length) (State, Visible) = (BusState.Away, false);
                break;
            default:
                return;
        }
        foreach (var (first, second, along) in leaves)
        {
            first.Rotation = new(0, along * Fold * doors, 0);
            second.Rotation = new(0, -2 * along * Fold * doors, 0);
        }
    }

    // Speeds up at Accel, no faster than the route allows where the bus is, and moves on along it.
    private void Drive(BusRoute route, float dt)
    {
        speed = Math.Min(speed + Accel * dt, route.Limit(s));
        s = Math.Min(route.Length, s + speed * dt);
    }

    private void Place(BusRoute route)
    {
        var (at, heading) = route.At(s);
        Position = new(at.X, Position.Y, at.Y);
        Rotation = new(0, Mathf.Atan2(heading.X, heading.Y), 0);
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
