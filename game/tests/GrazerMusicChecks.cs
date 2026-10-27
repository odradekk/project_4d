using System;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Audio;
using Game.Rendering;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The layers of the grazer's music and the flicker, which follow its distance from the player on the grazer test region.
/// The clocks of the grazer, the music and the flicker are stopped, so every check moves time itself.
/// </summary>
public partial class GrazerMusicChecks : Node
{
    private TurningFixture play = null!;
    private GrazerChoreography grazers => play.Region.Grazers!;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            CheckLayers();
            await Load();
            await CheckMusic();
            await Load();
            await CheckFlicker();
            GD.Print("Grazer music checks passed: layers, music, flicker.");
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

    private async Task Load()
    {
        await play.Load(scene: GrazerDemo.Scene);
        grazers.SetProcess(false);
    }

    private static void CheckLayers()
    {
        const double far = GrazerLayers.FarDistance, near = GrazerLayers.NearDistance, danger = GrazerLayers.DangerDistance;
        Check(far > near && near > danger && danger > GrazerLayers.DuckWidth, "the layers come in at falling distances");
        Check(GrazerLayers.At(far + 5) == new GrazerLevels(0, 0, 0) && GrazerLayers.At(far) == new GrazerLevels(0, 0, 0),
            "beyond the far distance, no layer");
        Check(GrazerLayers.At((far + near) / 2) == new GrazerLevels(.5, 0, 0), "half way from far to near, half the bass");
        Check(GrazerLayers.At(near) == new GrazerLevels(1, 0, 0), "at the near distance, all the bass and no rhythm yet");
        Check(GrazerLayers.At((near + danger) / 2) == new GrazerLevels(1, .5, 0), "half way from near to danger, half the rhythm");
        Check(GrazerLayers.At(danger) == new GrazerLevels(1, 1, 0), "at the danger distance, all the rhythm and nothing ducked yet");
        Check(GrazerLayers.At(danger - GrazerLayers.DuckWidth / 2) == new GrazerLevels(1, 1, .5), "half the duck width inside, half ducked");
        Check(GrazerLayers.At(0) == new GrazerLevels(1, 1, 1) && GrazerLayers.At(danger - GrazerLayers.DuckWidth) == new GrazerLevels(1, 1, 1),
            "past the duck width, everything in");
    }

    private async Task CheckMusic()
    {
        var music = new GrazerMusic();
        AddChild(music);
        music.Configure(play.Region, GrazerDemo.Grazer);
        music.SetProcess(false);
        var environment = AudioServer.GetBusIndex(SectionSound.EnvironmentBus);
        var quiet = AudioServer.GetBusVolumeDb(environment);
        var bass = music.GetNode<AudioStreamPlayer>("Bass");
        var scrape = music.GetNode<AudioStreamPlayer>("Scrape");
        play.Player.Position = new(0, 0, 3);
        await play.Frames(3);
        // The player is at (0, 0, 3, 0), the grazer at (0, 1.4, -2.2, 11): 1.4, 5.2 and 11 metres apart.
        Check(Math.Abs(grazers.Distance("grazer") - Math.Sqrt(1.4 * 1.4 + 5.2 * 5.2 + 121)) < 1e-6, "the distance is four-dimensional, W included");
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(0);
        music.Advance(10);
        Check(music.Levels == new GrazerLevels(0, 0, 0) && !bass.Playing && music.Beats == 0, "out of earshot, nothing plays");
        grazers.Advance(2);
        music.Advance(10);
        Check(music.Levels.Bass is > 0 and < 1 && music.Levels.Rhythm == 0 && music.Levels.Duck == 0, "coming into earshot, only some bass");
        Check(bass.Playing && !scrape.Playing && music.Beats == 0, "the bass plays, with no rhythm and no scraping");
        Check(AudioServer.GetBusVolumeDb(environment) == quiet, "and the other sounds are as they were");
        var farBass = bass.VolumeDb;
        grazers.Advance(14);
        music.Advance(10);
        Check(music.Levels.Bass == 1 && music.Levels.Rhythm is > 0 and < 1, "nearer, all the bass and the first of the rhythm");
        Check(bass.VolumeDb > farBass, "the nearer the bass, the louder");
        var slow = music.Beats;
        var slowScrape = scrape.VolumeDb;
        Check(slow > 0 && scrape.Playing, "the pulse beats and the scraping comes in");
        grazers.Advance(10);
        play.Player.Position = new(0, 0, -1);
        await play.Frames(3);
        music.Advance(10);
        Check(music.Levels.Rhythm == 1 && music.Levels.Duck == 1, "inside the danger distance the rhythm is full and the rest is ducked");
        Check(music.Beats - slow > slow, "the pulse comes faster the nearer it is: more beats in the same ten seconds");
        Check(scrape.VolumeDb > slowScrape, "and the scraping is louder");
        Check(AudioServer.GetBusVolumeDb(environment) < quiet - 12, "the other ambient sounds are pushed well down");
        music.Free();
        Check(AudioServer.GetBusVolumeDb(environment) == quiet, "the other sounds come back when the music goes");
    }

    private async Task CheckFlicker()
    {
        var flicker = new GrazerFlicker();
        AddChild(flicker);
        flicker.Configure(play.Region, GrazerDemo.Grazer);
        flicker.SetProcess(false);
        flicker.Random.Seed = 1;
        var darkest = 0.0;
        for (var i = 0; i < 600; i++)
        {
            flicker.Advance(1.0 / 60);
            darkest = Math.Max(darkest, flicker.Darkness);
        }
        Check(darkest == 0, "far from the grazer the picture never flickers");
        grazers.Play("grazer", GrazerDemo.Timeline());
        grazers.Advance(26);
        play.Player.Position = new(0, 0, -1);
        await play.Frames(3);
        var dark = 0;
        for (var i = 0; i < 600; i++)
        {
            flicker.Advance(1.0 / 60);
            if (flicker.Darkness > 0) dark++;
            darkest = Math.Max(darkest, flicker.Darkness);
        }
        Check(dark > 10 && dark < 300, $"close to the grazer the picture flickers now and then: {dark} of 600 frames");
        Check(darkest is > 0 and < 1, "and it never goes black");
        flicker.Free();
    }
}
