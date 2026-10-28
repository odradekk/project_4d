using System;
using Game.Rendering;
using Game.UI;
using Godot;
using static Game.Flow.RoomQueries;

namespace Game.Flow;

// The way in, the sounds of her body, the grazer's chase and the end of the scene.
public partial class DeepRoom
{
    // The grazer is this near, as the engine measures it from her feet to the body origin (her height over the floor is in it),
    // when she is in its mouth, which is caught while it is still at its bites: the cat tears loose and she can no longer move.
    private const double EngulfDistance = 2.6;
    // When it is on its way to her, the same happens at this distance; and she screams when it is at the last.
    private const double FleeDistance = 6;
    private const double ScreamDistance = 3.6;
    // If it has not come this near by then it has been coming this long, and she can no longer move, so that the scene ends.
    private const double ChaseSeconds = 40;
    // The cat has been loose and the grazer has gone for her at least this long, a leg and a little over, before she screams.
    private const double MinChaseSeconds = 3.5;
    private const double ScreamSeconds = 1.8;
    private const double BlackSeconds = 3;
    private const double TitleSeconds = 5;
    private const string Title = "【片名】";

    private AudioStreamPlayer heartbeat = null!, breathing = null!, shriek = null!, hiss = null!;
    private GrazerFlicker flicker = null!;
    private Blackout? blackout;
    private double corneredFor;
    private bool bodySounds, cornered;

    /// <summary>Whether the cat has torn loose from her and run.</summary>
    public bool CatFled { get; private set; }

    /// <summary>The title and the black screen are up.</summary>
    internal Blackout? Cut => blackout;

    // What scene 8 leaves, for a scene that starts here by itself: the cat sits in section III, the seam to it stands open, and
    // she has followed the cat as far as section II.
    private void AfterSceneEight()
    {
        home.Region.World!.SetSeamOpen(new(DeepGrazer.Seam), true);
        SendCat("S1", "s1", SectionII);
        SendCat("S2", "s2", SectionIII);
        catBody.PlaceAt(CatRoom.Deep, 0);
        cat.Sit(CatRoom.Deep + Vector3.Right);
        var s1 = home.SeamAt("S1");
        script.Do(() =>
        {
            Place(s1 + new Vector3(0, .02f, 0));
            prism.SetRaised(true);
        });
        // The engine is told where she stands on the next frame, and the crossing asks it.
        script.Wait(.2);
        script.Do(() =>
        {
            if (home.Region.Travel!.Cross() is { } refused) throw new InvalidOperationException($"She could not follow the cat into section II: {refused.Code}");
            prism.SetRaised(false);
            Place(home.SeamAt("S2") + new Vector3(-.7f, .02f, 0));
        });
    }

    private void SendCat(string node, string seam, string target)
    {
        catBody.PlaceAt(home.SeamAt(node), 0);
        if (catBody.CrossSeam(seam, target) is { } refused) throw new InvalidOperationException($"The cat could not cross {seam}: {refused}");
    }

    // Puts her feet at a place, looking east.
    private void Place(Vector3 feet)
    {
        var player = home.Player;
        player.Position = feet;
        player.Velocity = Vector3.Zero;
        player.Rotation = new(0, -Mathf.Pi / 2, 0);
        player.Camera.Rotation = Vector3.Zero;
    }

    private void MakeSounds()
    {
        heartbeat = Sound("heartbeat", -4);
        breathing = Sound("breathing", -8, 1.25f);
        shriek = Sound("scream", 0);
        hiss = Sound("cat", -2);
        foreach (var loop in new[] { heartbeat, breathing })
        {
            var sound = loop;
            sound.Finished += () =>
            {
                if (bodySounds) sound.Play();
            };
        }
    }

    private AudioStreamPlayer Sound(string file, float volumeDb, float pitch = 1)
    {
        var sound = new AudioStreamPlayer { Name = file, Stream = GD.Load<AudioStream>($"res://audio/{file}.wav"), VolumeDb = volumeDb, PitchScale = pitch };
        AddChild(sound);
        return sound;
    }

    private void StartBody()
    {
        bodySounds = true;
        heartbeat.Play();
        breathing.Play();
    }

    // The grazer has walked its path and now goes for her, a leg at a time toward where she stands.
    private void Grazing()
    {
        var grazers = home.Region.Grazers!;
        var distance = grazers.Distance(DeepGrazer.Id);
        if (!cornered && (distance < FleeDistance || stageClock > ChaseSeconds)) Corner();
        if (cornered && stageClock >= MinChaseSeconds && corneredFor >= MinChaseSeconds && distance < ScreamDistance)
        {
            Become(Stage.Scream);
            return;
        }
        if (grazers.IsPlaying(DeepGrazer.Id)) return;
        var leg = DeepGrazer.Leg(chaseFrom, home.Player.Position.X, home.Player.Position.Z);
        chaseFrom = DeepGrazer.End(leg);
        grazers.Play(DeepGrazer.Id, leg);
    }

    // She is caught: her keys and the mouse are off and the scene turns her eyes to the grazer, and the cat tears out of her arms.
    private void Corner()
    {
        cornered = true;
        CatFled = true;
        var player = home.Player;
        prism.SetRaised(false);
        home.Hud.CloseNotebook();
        player.Seated = true;
        player.SetProcessUnhandledInput(false);
        cat.Holdable = false;
        Say(Xiaoman, 2, "煤球——！");
        // It is in front of her chest, so it jumps down and runs off to her side, the one that is farther from the grazer. Any
        // command to the cat ends the carrying if she has it in her arms.
        var side = new Vector3(player.Basis.X.X, 0, player.Basis.X.Z).Normalized();
        var away = (catBody.Position - GrazerAt()).Dot(side) >= 0 ? side : -side;
        cat.Flee(catBody.Position - away);
        hiss.Play();
    }

    private Vector3 GrazerAt()
    {
        var at = ask.State(DeepGrazer.Id).Pose.Position;
        return new((float)at.X, (float)at.Y, (float)at.Z);
    }

    // Her eyes follow the mouth of the grazer once she is caught.
    private void LookAtGrazer(double delta)
    {
        if (!cornered) return;
        var player = home.Player;
        var offset = GrazerAt() - player.Camera.GlobalPosition;
        var blend = 1 - Mathf.Exp(-4 * (float)delta);
        player.Rotation = new(0, Mathf.LerpAngle(player.Rotation.Y, Mathf.Atan2(-offset.X, -offset.Z), blend), 0);
        player.Camera.Rotation = new(Mathf.Lerp(player.Camera.Rotation.X, Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), blend), 0, 0);
    }

    private void StartScream()
    {
        Say("", ScreamSeconds, "啊——！");
        shriek.Play();
    }

    // The scream is cut off with the picture: nothing else sounds, and the title comes up after a while.
    private void CutToBlack()
    {
        StopSounds();
        music.Free();
        flicker.Free();
        blackout = new Blackout { Name = "Blackout" };
        AddChild(blackout);
        script.Wait(BlackSeconds);
        script.Do(() => blackout.ShowTitle(Title));
        script.Wait(TitleSeconds);
        script.Do(() => EmitSignal(SignalName.Finished));
    }

    private void StopSounds()
    {
        bodySounds = false;
        foreach (var sound in new[] { heartbeat, breathing, shriek, hiss }) sound.Stop();
    }

    public override void _ExitTree()
    {
        StopSounds();
        // The cat outlives the scene when something else hosts the region.
        cat.PickedUp -= PickedUp;
    }
}
