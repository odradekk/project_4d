using System.Collections.Generic;
using System.Linq;
using Game.Audio;
using Game.Flow;
using Game.Player;
using Godot;

namespace Game.Characters;

/// <summary>
/// A teacher who writes at the board and walks the aisle to and fro, as commands to the <see cref="Character3D"/> it is
/// a child of. Chalk sounds while the teacher writes, footsteps while the teacher walks. The teacher sees the player
/// who holds the prism up in view, says so, and then the scene goes back to its checkpoint.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
[GlobalClass]
public partial class Teacher : Node
{
    private const string CaughtLine = "看哪儿呢？";
    private const double CaughtSeconds = 1.5;
    // The teacher's eyes, and how far and how wide the teacher sees.
    private const float EyeHeight = 1.5f;
    private const float SightRange = 4.5f;
    private static readonly float SightHalfAngle = Mathf.DegToRad(60);
    private const uint WorldLayer = 1;

    private enum Phase { ToBoard, Writing, Walking, Caught }

    private Character3D body = null!;
    private IReadOnlyList<Vector3> route = [];
    private Vector3[] aisle = [];
    private double writeSeconds;
    private Phase phase;
    private double timer;
    private int next;
    private SectionSound? chalk;
    private SectionSound? footsteps;

    /// <summary>Emitted when the teacher sees the player who holds up the prism.</summary>
    [Signal] public delegate void SpottedEventHandler();

    /// <summary>Restored when the player has been seen; the teacher goes back to the board.</summary>
    public SceneCheckpoint? Checkpoint { get; set; }

    public override void _Ready()
    {
        body = GetParent<Character3D>();
        // Whoever tells the teacher to do something else, the chalk and the footsteps stop.
        body.ActionReplaced += Silence;
    }

    /// <summary>Stops the patrol: the teacher stands where it is and the sounds stop.</summary>
    public void Stop() => body.Stop();

    /// <summary>
    /// Goes to the first point of the route and writes there for the given time, facing away from the second point, which
    /// is where the board is. Then walks to the last point and back, and writes again, and so on.
    /// </summary>
    public void Patrol(IReadOnlyList<Vector3> points, double seconds)
    {
        route = points;
        writeSeconds = seconds;
        aisle = route.Skip(1).Concat(route.SkipLast(1).Reverse()).ToArray();
        chalk ??= MakeSound("chalk");
        footsteps ??= MakeSound("footsteps");
        phase = Phase.ToBoard;
        body.Posture = Posture.Standing;
        body.Do(Tick);
    }

    private SectionSound MakeSound(string file)
    {
        var sound = new SectionSound { Stream = GD.Load<AudioStream>($"res://audio/{file}.wav") };
        // The listener is in the main viewport, with the player; a sound in the teacher's own viewport would not be heard.
        body.Player.AddSibling(sound);
        sound.Configure(body.Region!, body.Section);
        return sound;
    }

    private bool Tick(double delta)
    {
        if (phase != Phase.Caught && Sees(body.Player)) Catch(body.Player);
        switch (phase)
        {
            case Phase.ToBoard when body.WalkTo(route[0], body.WalkSpeed):
                Write();
                break;
            case Phase.Writing:
                timer -= delta;
                if (timer <= 0 && aisle.Length > 0)
                {
                    phase = Phase.Walking;
                    next = 0;
                }
                break;
            case Phase.Walking when body.WalkTo(aisle[next], body.WalkSpeed):
                if (++next == aisle.Length) Write();
                break;
            case Phase.Caught:
                timer -= delta;
                if (timer <= 0) Resume();
                break;
        }
        Sound(chalk!, phase == Phase.Writing);
        Sound(footsteps!, phase is Phase.ToBoard or Phase.Walking);
        return false;
    }

    private void Write()
    {
        phase = Phase.Writing;
        timer = writeSeconds;
        if (route.Count > 1) body.Face(body.Position * 2 - route[1]);
    }

    private void Catch(FirstPersonPlayer player)
    {
        phase = Phase.Caught;
        timer = CaughtSeconds;
        body.Face(player.Position);
        // What is on the screen waits for nothing: the line must be seen before the scene goes back.
        player.Hud.ClearText();
        body.Say(CaughtLine, CaughtSeconds);
        EmitSignal(SignalName.Spotted);
    }

    // After the scene has gone back, the teacher is at the board again.
    private void Resume()
    {
        Checkpoint?.Restart();
        body.PlaceAt(route[0], body.Rotation.Y);
        Write();
    }

    private void Silence()
    {
        chalk?.Stop();
        footsteps?.Stop();
    }

    private void Sound(SectionSound sound, bool wanted)
    {
        sound.Position = body.Position + Vector3.Up;
        if (wanted && !sound.Playing) sound.Play();
        else if (!wanted && sound.Playing) sound.Stop();
    }

    private bool Sees(FirstPersonPlayer player)
    {
        if (!body.WithPlayer || !player.PrismRaised) return false;
        var eyes = body.Position + Vector3.Up * EyeHeight;
        var target = player.Camera.GlobalPosition;
        var offset = target - eyes;
        if (offset.Length() > SightRange || (-body.Basis.Z).AngleTo(new Vector3(offset.X, 0, offset.Z)) > SightHalfAngle) return false;
        var ray = PhysicsRayQueryParameters3D.Create(eyes, target, WorldLayer);
        ray.Exclude = [player.GetRid()];
        return body.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count == 0;
    }
}
