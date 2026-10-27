using Godot;

namespace Game.Audio;

/// <summary>
/// The faint backward sound around a mirror seam, mixed into the environment sound. The placeholder is the
/// breathing sound played in reverse (mirror_hum.wav).
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class MirrorHum : AudioStreamPlayer3D
{
    /// <summary>How close to the seam, in metres, the hum is heard.</summary>
    public const float Range = 2;

    public override void _Ready()
    {
        Stream = GD.Load<AudioStream>("res://audio/mirror_hum.wav");
        Bus = SectionSound.EnvironmentBus;
        UnitSize = 1;
        MaxDistance = Range;
        VolumeDb = -9;
    }

    public override void _ExitTree() => Stop();

    /// <summary>Whether the hum plays.</summary>
    public bool Active
    {
        get => Playing;
        set
        {
            if (value && !Playing) Play();
            else if (!value && Playing) Stop();
        }
    }
}
