using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Audio;
using Game.Player;
using Godot;
using static Game.Tests.ItemChecks;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The hanging cabinet and the sliding window of the item test region: E moves a device step by step, the lens and the player
/// show what stops it, and the other section hears it. Expected values are worked out by hand from the devices' shapes.
/// </summary>
public partial class DeviceChecks : Node
{
    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(null, Scene);
            CheckSectionsOfDevices();
            await CheckCabinetOpensAndCloses();
            // The block exists from W = .3 on, in the second section only, on the arc that the end of the plank sweeps.
            await play.Load(r => AddBlock(r, "BlockII", new(-.93f, 1.25f, -3f), new(.3f, .3f, .4f), .3), Scene);
            await CheckCabinetStopped();
            await play.Load(null, Scene);
            await CheckWindowBlockedByStandingPencil();
            await play.Unload();
            GD.Print("Device checks passed: sections, glow, cabinet, thud, stopped cabinet, window and pencil.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void CheckSectionsOfDevices()
    {
        // The flap shows in the first section, 40 cm high at the hinge x = -3; the plank, in the second, leaning toward +x.
        var flap = play.SliceBounds("Ⅰ", "cabinet");
        Check(Mathf.Abs(flap.Size.Y - .4f) < .001f && Mathf.Abs(flap.Position.Y - 2f) < .001f, $"the flap must stand 40 cm up from the hinge; it is {flap}");
        var plank = play.SliceBounds("Ⅱ", "cabinet");
        Check(plank.End.Y > 2.9f && plank.End.X > -1.1f, $"the plank must lean toward the ceiling at the other side; it is {plank}");
        foreach (var section in new[] { "Ⅰ", "Ⅱ" })
        {
            Check(play.Mask(section, "cabinet") != Colors.Black, $"the cabinet must glow in the lens of {section}");
            Check(play.Mask(section, "sash") != Colors.Black, $"the window must glow in the lens of {section}");
            Check(play.Region.HasNode($"Generated/{section}/Dynamic/cabinet/Collision"), $"the cabinet must have a collision body in {section}");
            Check(play.Region.HasNode($"Generated/{section}/Dynamic/sash/Collision"), $"the window must have a collision body in {section}");
        }
    }

    private async Task CheckCabinetOpensAndCloses()
    {
        play.LookAt(new(-1.9f, .02f, -3f), new(-3f, 2.2f, -3f));
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("拉动"), $"aiming at a device must offer pulling it; the hint is '{player.Hud.HintText}'");

        // E lets go of what is in the hand first, and puts down what is in the arms before that; the hint says the same.
        Check(play.Region.World!.PlaceEntity(new("ball"), new(new(-1.5, .25, -2.5, .21), SceneReader.Identity)) is FourD.UpdateResult.Updated, "the ball must be put near her");
        Check(play.Actions.Hold(new("ball")) is null, "the ball must come into the hand");
        await play.Frames(3);
        Check(player.Hud.HintText.Contains("放下") && !player.Hud.HintText.Contains("拉动"), $"with the hand full the hint must not offer pulling; it is '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(5);
        Check(play.Actions.Held is null && play.State("cabinet").DevicePosition == 0, "E with the hand full must let go and not pull");
        var puttingDown = false;
        player.PutDown = () => puttingDown = true;
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("放下") && !player.Hud.HintText.Contains("拉动"), $"with the arms full the hint must not offer pulling; it is '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(5);
        Check(puttingDown && play.State("cabinet").DevicePosition == 0, "E with the arms full must put down and not pull");
        player.PutDown = null;
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(30);
        var halfway = (double)play.State("cabinet").DevicePosition!;
        Check(halfway < -.3 && halfway > -1.3, $"the flap must be on its way after half a second; it is at {halfway}");
        await play.Frames(60);
        Check(Math.Abs(play.State("cabinet").DevicePosition!.Value + Math.PI / 2) < 1e-9, "the cabinet must open to a quarter turn");

        // The flap lies along +x from the hinge at y = 2, 2 cm thick. The plank, which leaned to x = 1.97 and y = .98 above the
        // hinge, has turned to x = .97 to .99 and y = -1.97 to -1.96 from it: a slope that ends .028 m above the floor.
        var flap = play.SliceBounds("Ⅰ", "cabinet");
        Check(Mathf.Abs(flap.Size.X - .4f) < .001f && Mathf.Abs(flap.Position.Y - 1.99f) < .001f && Mathf.Abs(flap.Size.Y - .02f) < .001f,
            $"the flap must lie flat along +x; it is {flap}");
        var plank = play.SliceBounds("Ⅱ", "cabinet");
        Check(Mathf.Abs(plank.Position.Y - .0278f) < .002f && Mathf.Abs(plank.End.X - -2.0072f) < .002f, $"the plank must lie as a slope to the floor; it is {plank}");

        // The knock is heard from the other section, filtered by the distance to it.
        var knocks = Knocks().ToArray();
        Check(knocks.Length == 1, $"one muffled knock must come from the other section; {knocks.Length} sounds play");
        var knock = knocks[0];
        var bus = AudioServer.GetBusIndex(knock.Bus);
        Check(AudioServer.GetBusVolumeDb(bus) < -3 && ((AudioEffectLowPassFilter)AudioServer.GetBusEffect(bus, 0)).CutoffHz < 5000,
            "the knock from the other section must be quieter and duller");
        Check(knock.GlobalPosition.DistanceTo(plank.GetCenter()) < .1f, $"the knock must come from the plank, at {plank.GetCenter()}; it comes from {knock.GlobalPosition}");

        Press(Key.E);
        await play.Frames(90);
        Check(Math.Abs(play.State("cabinet").DevicePosition!.Value) < 1e-9, "E again must close the cabinet");
        Check(play.SliceBounds("Ⅱ", "cabinet").End.Y > 2.9f, "the plank must be back under the ceiling");
    }

    private async Task CheckCabinetStopped()
    {
        play.LookAt(new(-1.9f, .02f, -3f), new(-3f, 2.2f, -3f));
        await play.Frames(2);
        player.Hud.ClearText();
        Press(Key.E);
        var knocked = await UntilStopped("cabinet");
        var stopped = play.State("cabinet").DevicePosition!.Value;
        Check(stopped < -.3 && stopped > -1.4, $"the cabinet must stop against the block in the other section; it is at {stopped}");
        Check(player.Hud.SubtitleText.Contains("动不了") && player.Hud.SubtitleText.Contains("那一页"), $"the player must say that the other section is in the way; saw '{player.Hud.SubtitleText}'");
        Check(knocked, "a cabinet that is stopped knocks in the other section, as a window does");

        // Pulled again, a stopped cabinet goes back to where it started, since the block would stop it again on the way out.
        Press(Key.E);
        await play.Frames(90);
        Check(Math.Abs(play.State("cabinet").DevicePosition!.Value) < 1e-9, "a stopped cabinet must go back to where it started");
    }

    // Waits until a device that was pulled stands still, and says whether it knocked on the way. How many physics steps a
    // frame holds depends on the machine, so the marks and the knock are looked at the moment it stops.
    private async Task<bool> UntilStopped(string device)
    {
        var knocked = false;
        var before = play.State(device).DevicePosition;
        for (var i = 0; i < 300; i++)
        {
            await play.Frames(1);
            knocked |= Knocks().Any();
            var now = play.State(device).DevicePosition;
            if (now == before && i > 0) return knocked;
            before = now;
        }
        throw new InvalidOperationException($"{device} must stop");
    }

    // The sounds of devices that are playing now, which come from the sections that the player is not in.
    private IEnumerable<SectionSound> Knocks() => play.Region.GetChildren().OfType<SectionSound>().Where(sound => sound.Playing);

    private async Task CheckWindowBlockedByStandingPencil()
    {
        // The pencil lies on the sill and the pane clears it in the first section. Stood up toward the second section it is a
        // thin plate, x = .78 +- .006, whose W reaches to .46; the pane's lower edge dips below its top at W = .3 and on.
        play.LookAt(new(1f, .02f, -2.7f), new(1f, .92f, -3.2f));
        await play.Frames(2);
        Button(MouseButton.Right, true);
        Press(Key.F);
        await play.Frames(TurnFrames);
        Button(MouseButton.Right, false);
        Check(play.State("pencil").UprightOrigin?.Value == "Ⅰ", "F must stand the pencil up");

        // The pane is 40 cm wide with its right edge at x = .4 + the way it has gone, and meets the plate at x = .774: .374.
        play.LookAt(new(.2f, .02f, -2.2f), new(.2f, 1.4f, -3.2f));
        player.Hud.ClearText();
        await play.Frames(2);
        Press(Key.E);
        var knocked = await UntilStopped("sash");
        Check(play.Region.GetNode("Generated/Ⅱ/Overlay/Contacts").GetChildCount() > 0, "the lens must mark where the window was stopped");
        Check(knocked, "a window that is stopped knocks in the other section");
        var way = play.State("sash").DevicePosition!.Value;
        Check(Math.Abs(way - .374) < .01, $"the window must stop at the standing pencil, .374 m along; it is at {way}");
        Check(player.Hud.SubtitleText.Contains("动不了"), $"the player must say that the window is stopped; saw '{player.Hud.SubtitleText}'");

        // Laid down again, the pencil lets the window through to the end of its way.
        var slice = play.SliceBounds("Ⅰ", "pencil").GetCenter();
        play.LookAt(new(slice.X, .02f, slice.Z + .5f), slice + new Vector3(0, .01f, 0));
        await play.Frames(2);
        Press(Key.F);
        await play.Frames(TurnFrames);
        Check(play.State("pencil").UprightOrigin is null, "F must lay the pencil down");
        // The window that was stopped goes back to where it started when it is pulled, and the next pull closes it.
        play.LookAt(new(.5f, .02f, -2.2f), new(.5f, 1.4f, -3.2f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(90);
        Check(Math.Abs(play.State("sash").DevicePosition!.Value) < 1e-9, "a window that was stopped must go back to where it started");
        play.LookAt(new(.2f, .02f, -2.2f), new(.2f, 1.4f, -3.2f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(90);
        Check(Math.Abs(play.State("sash").DevicePosition!.Value - 1) < 1e-9, "the window must close once the pencil lies down");
        Check(Mathf.Abs(play.SliceBounds("Ⅰ", "sash").End.X - 1.4f) < .001f, "the pane must have slid 1 m along its track");
    }
}
