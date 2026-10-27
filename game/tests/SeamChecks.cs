using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Audio;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// What the seams of the seam test region look and sound like: how an authored seam is read, what the lens shows of the seams
/// through the player's section, the bands around mirror seams, and the backward hum that goes with them.
/// </summary>
public partial class SeamChecks : Node
{
    private TurningFixture play = null!;
    private SeamPlay seam = null!;
    private Region3D region => play.Region;
    private FourD.World world => region.World!;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        seam = new SeamPlay(this, play);
        try
        {
            await seam.Load();
            CheckAuthoring();
            CheckLights();
            await CheckLightsFollowTheSection();
            CheckSeamStates();
            await seam.Load();
            await CheckBands();
            await seam.Load(MakeMirrors);
            await CheckHum();
            CheckHumIsTheBreathingBackwards();
            await seam.Load();
            CheckLensTargets();
            await play.Unload();
            GD.Print("Seam checks passed: authoring, lens lights, section filter, states, mirror bands, hum, lens targets.");
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

    private async Task RaiseAndCross()
    {
        await seam.RaiseLens();
        await seam.Click();
    }

    // The seam to III becomes a mirror seam, and the mirror seam is moved to join II and III, so that it passes through I no more.
    private static void MakeMirrors(Region3D region)
    {
        region.GetNode<Seam3D>("Seams/Skip").Mirrored = true;
        region.GetNode<Seam3D>("Seams/Glass").Sections = ["Ⅲ", "Ⅱ"];
    }

    private static bool Reports(LoadResult result, LoadErrorCode code, string node, string property) =>
        result is LoadResult.Invalid invalid &&
        invalid.Errors.Any(error => error.Code == code && error.Source.NodePath == node && error.Source.Property == property);

    private void CheckAuthoring()
    {
        Check(world.ReadSeam(new("needle")) is SeamReadResult.Found { Seam: { Open: true, Severed: false } }, "an authored seam is open unless it says otherwise");
        Check(world.ReadSeam(new("cut")) is SeamReadResult.Found { Seam.Severed: true }, "a seam authored as severed is severed in the engine");
        Check(world.ReadSeam(new("glass")) is SeamReadResult.Found, "every Seam3D node is loaded into the engine");

        var needle = region.GetNode<Seam3D>("Seams/Needle");
        var glass = region.GetNode<Seam3D>("Seams/Glass");
        needle.Sections = ["Ⅰ", "Nowhere"];
        Check(Reports(new SceneReader(region).Load(), LoadErrorCode.MissingReference, "Seams/Needle", "Sections"), "a seam through an unknown section is reported at its Sections");
        needle.Sections = ["Ⅱ", "Ⅰ"];
        Check(Reports(new SceneReader(region).Load(), LoadErrorCode.InvalidSectionOrder, "Seams/Needle", "Sections"), "sections must be listed shallowest first");
        needle.Sections = ["Ⅰ", "Ⅱ"];
        needle.HorizontalNormal = Vector3.Up;
        Check(Reports(new SceneReader(region).Load(), LoadErrorCode.InvalidGeometry, "Seams/Needle", "HorizontalNormal"), "a seam normal must be horizontal");
        needle.HorizontalNormal = Vector3.Right;
        glass.Position = needle.Position + new Vector3(.5f, 0, 0);
        Check(Reports(new SceneReader(region).Load(), LoadErrorCode.SeamTooClose, "Seams/Glass", "Transform"), "two seams of a section must be at least 0.8 m apart, reported at the node's position");
        glass.Position = new(1.5f, 0, 1.5f);
        Check(new SceneReader(region).Load() is LoadResult.Loaded, "the restored seams load again");
    }

    private string Light(string section, string seam) => $"Generated/{section}/Overlay/Seams/{seam}";

    private bool Shown(string section, string seam) => region.HasNode(Light(section, seam));

    private BoxMesh LightMesh(string section, string seam) => (BoxMesh)region.GetNode<MeshInstance3D>($"{Light(section, seam)}/Light").Mesh;

    private void CheckLights()
    {
        foreach (var seam in new[] { "needle", "glass", "cut" })
            Check(Shown("Ⅱ", seam), $"the lens at II shows the seam {seam}, which passes through I and II");
        Check(!Shown("Ⅱ", "skip") && Shown("Ⅲ", "skip"), "the seam between I and III is drawn in those sections only");

        var light = LightMesh("Ⅱ", "needle");
        Check(Mathf.IsEqualApprox(light.Size.Y, 3) && light.Size.X < .05f && light.Size.Z < .05f, "a plain seam is a thin light from the floor to the ceiling");
        var camera = play.Player.Camera;
        var lens = region.GetNode<Camera3D>("Generated/Prism/PrismViewport/PrismCamera");
        var mesh = region.GetNode<MeshInstance3D>($"{Light("Ⅱ", "needle")}/Light");
        Check((mesh.Layers & camera.CullMask) == 0 && (mesh.Layers & lens.CullMask) != 0, "the light is seen in the lens only");
        Check(Mathf.IsEqualApprox(mesh.Position.Y - light.Size.Y / 2, 0) && mesh.GetParent<Node3D>().Position.IsEqualApprox(new(-1.5f, 0, 1.5f)),
            "it stands on the floor at the seam");

        var stub = LightMesh("Ⅱ", "cut");
        var cap = (BoxMesh)region.GetNode<MeshInstance3D>($"{Light("Ⅱ", "cut")}/Cut").Mesh;
        Check(stub.Size.Y < .5f && stub.Size.Y > .1f, "a severed seam is only a short stub");
        Check(cap.Size.X > stub.Size.X && cap.Size.Y < .05f, "which ends in a flat cut, wider than the light");
        Check(!region.HasNode($"{Light("Ⅱ", "needle")}/Cut"), "an unbroken seam has no cut end");
    }

    // Only the seams through the section she is in are shown, in every section they pass.
    private async Task CheckLightsFollowTheSection()
    {
        play.LookAt(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await play.Frames(3);
        await RaiseAndCross();
        Check(region.CurrentSection == "Ⅱ", "she crossed to II");
        Check(!Shown("Ⅲ", "skip") && !Shown("Ⅰ", "skip"), "a seam that does not pass through her section is not shown");
        Check(Shown("Ⅰ", "needle") && Shown("Ⅱ", "needle") && Shown("Ⅰ", "glass"), "the seams through her section are shown where the lens can look");
        await seam.Click();
        Check(region.CurrentSection == "Ⅰ" && Shown("Ⅲ", "skip"), "back in I the seam to III is shown again");
    }

    // The engine says whether a seam is open and whether it is severed; the display follows without being told.
    private void CheckSeamStates()
    {
        var open = region.GetNode<MeshInstance3D>($"{Light("Ⅱ", "needle")}/Light").MaterialOverride as StandardMaterial3D;
        var before = open!.AlbedoColor;
        world.SetSeamOpen(new("needle"), false);
        region.Travel!.Display.Refresh("Ⅰ", play.Player.Position);
        var closed = (StandardMaterial3D)region.GetNode<MeshInstance3D>($"{Light("Ⅱ", "needle")}/Light").MaterialOverride;
        Check(closed.AlbedoColor.V < before.V, "a closed seam is dimmer");
        world.SetSeamOpen(new("needle"), true);
        region.Travel.Display.Refresh("Ⅰ", play.Player.Position);
        world.SeverSeam(new("needle"));
        region.Travel.Display.Refresh("Ⅰ", play.Player.Position);
        Check(LightMesh("Ⅱ", "needle").Size.Y < .5f && region.HasNode($"{Light("Ⅱ", "needle")}/Cut"), "a seam that is bitten through shows as a stub with a cut end");
    }

    // The lens can show another set of sections, and the one she came from is shown if it is among them.
    private void CheckLensTargets()
    {
        var prism = region.GetNode<Game.Rendering.PrismView>("Generated/Prism");
        (string, World3D)[] targets = [("A", new()), ("B", new()), ("C", new())];
        prism.SetTargets(targets, "B");
        Check(prism.TargetSection == "B", "the lens shows the section she came from");
        prism.SetTargets(targets, "elsewhere");
        Check(prism.TargetSection == "A", "otherwise the nearest section");
        prism.SetTargets(targets, null);
        Check(prism.TargetSection == "A", "and so it does when she did not come from anywhere");
    }

    private async Task CheckBands()
    {
        var display = region.Travel!.Display;
        var bands = display.BandsIn("Ⅱ");
        Check(bands.Count == 1 && bands[0].Foot.IsEqualApprox(new(1.5f, 0, 1.5f)) && Mathf.IsEqualApprox(bands[0].Height, 3),
            "the lens at II has the band of the mirror seam alone, as high as its light");
        Check(display.BandsIn("Ⅲ").Count == 0, "a seam that does not reach the section the lens shows has no band there");

        world.SetSeamOpen(new("glass"), false);
        Check(display.BandsIn("Ⅱ").Count == 0, "a mirror seam that is shut has no band");
        world.SetSeamOpen(new("glass"), true);

        play.LookAt(new(1.65f, .02f, 1.5f), new(1.5f, 1, 1.5f));
        await play.Frames(3);
        await RaiseAndCross();
        Check(region.CurrentSection == "Ⅱ" && display.BandsIn("Ⅰ").Count == 1, "after crossing, the lens at I shows the band of the seam she came through");
        world.SeverSeam(new("glass"));
        Check(display.BandsIn("Ⅰ").Count == 0, "a severed mirror seam has no band");
    }

    private MirrorHum Hum(string seamId) => region.GetNode<MirrorHum>($"Generated/Hum-{seamId}");

    // The two mirror seams: the one to III is through her section, and the one through II and III is not.
    private async Task CheckHum()
    {
        var glass = Hum("glass");
        var skip = Hum("skip");
        Check(!region.HasNode("Generated/Hum-needle"), "a plain seam has no hum");
        await play.Frames(2);
        Check(!glass.Playing && !skip.Playing, "she starts 2.1 m from the one and 4.5 m from the other, and hears neither");
        Check(skip.Stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward }, "the hum loops");
        Check(skip.Position.IsEqualApprox(new(0, 1, -1.5f)) && skip.MaxDistance == MirrorHum.Range, "it sounds from the seam, and is heard within its range");

        await seam.Stand(new(.15f, .02f, -1.5f), new(0, 1, -1.5f));
        Check(skip.Playing && !glass.Playing, "near a mirror seam through her section it hums");
        world.SetSeamOpen(new("skip"), false);
        await play.Frames(2);
        Check(!skip.Playing, "a mirror seam that is shut is silent");
        world.SetSeamOpen(new("skip"), true);
        await play.Frames(2);
        Check(skip.Playing, "and hums again when it opens");
        world.SeverSeam(new("skip"));
        await play.Frames(2);
        Check(!skip.Playing, "a severed mirror seam is silent");

        await seam.Stand(new(1.65f, .02f, 1.5f), new(1.5f, 1, 1.5f));
        Check(!glass.Playing, "a mirror seam that does not pass through her section is silent, even close by");
    }

    // The placeholder is the breathing sound played in reverse: the same header, the same samples, last to first.
    private static void CheckHumIsTheBreathingBackwards()
    {
        var breathing = FileAccess.GetFileAsBytes("res://audio/breathing.wav");
        var hum = FileAccess.GetFileAsBytes("res://audio/mirror_hum.wav");
        const int header = 44;
        Check(breathing.Length == hum.Length && breathing[..header].SequenceEqual(hum[..header]), "the hum is as long as the breathing and has its format");
        var samples = (breathing.Length - header) / 2;
        for (var i = 0; i < samples; i++)
            Check(hum[header + i * 2] == breathing[header + (samples - 1 - i) * 2] && hum[header + i * 2 + 1] == breathing[header + (samples - 1 - i) * 2 + 1],
                $"sample {i} of the hum is sample {samples - 1 - i} of the breathing");
    }
}
