using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Audio;
using Game.Rendering;
using Godot;

namespace Game.Adapter;

/// <summary>A seam as authored: where it stands, the sections it joins, and how tall its light is drawn.</summary>
internal sealed record SeamMark(string Id, Vector3 Foot, IReadOnlyList<string> Sections, bool Mirrored, float Height);

/// <summary>
/// What shows and sounds of the seams that pass through the player's section: a thin light in the lens, a
/// stub for a severed seam, and the backward hum of a mirror seam. The engine says whether a seam is open or
/// severed; the display follows it.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class SeamDisplay
{
    private const uint LensOnly = 2;
    private const float LightWidth = .02f;
    private const float StubHeight = .35f;
    private const float CapWidth = .08f;
    private const float CapThickness = .01f;
    private static readonly Color OpenLight = new(1f, .93f, .55f);
    private static readonly Color ClosedLight = new(.5f, .46f, .32f);
    private static readonly Color CutEnd = new(.78f, .78f, .84f);

    private readonly World world;
    private readonly IReadOnlyList<SectionScene> scenes;
    private readonly Dictionary<string, MirrorHum> hums = [];
    private readonly Dictionary<string, (string Section, bool Open, bool Severed)> shown = [];
    private readonly IReadOnlyList<SeamMark> marks;
    private string current = "";

    internal SeamDisplay(World world, IReadOnlyList<SeamMark> marks, IReadOnlyList<SectionScene> scenes, Node humHost)
    {
        this.world = world;
        this.marks = marks;
        this.scenes = scenes;
        foreach (var scene in scenes) scene.Overlay.AddChild(new Node3D { Name = "Seams" });
        foreach (var mark in marks.Where(mark => mark.Mirrored))
        {
            var hum = new MirrorHum { Name = $"Hum-{mark.Id}", Position = mark.Foot + Vector3.Up };
            humHost.AddChild(hum);
            this.hums.Add(mark.Id, hum);
        }
    }

    /// <summary>The seams joining both sections, which the player can cross from the first to the second.</summary>
    internal IEnumerable<SeamMark> Between(string from, string to) =>
        marks.Where(mark => mark.Sections.Contains(from) && mark.Sections.Contains(to));

    /// <summary>The engine's word on whether a seam can be crossed now: open, and not severed.</summary>
    internal bool IsUsable(SeamMark mark) => StateOf(mark) is { Open: true, Severed: false };

    /// <summary>
    /// The mirror bands to draw in the lens when it shows the target section: those of mirror seams that pass through both the
    /// player's section and the target and are not severed. A shut seam keeps its band, so that it still reads as a mirror seam.
    /// </summary>
    internal IReadOnlyList<MirrorBand> BandsIn(string target) => Between(current, target)
        .Where(mark => mark.Mirrored && !StateOf(mark).Severed)
        .Select(mark => new MirrorBand(mark.Foot, mark.Height, StateOf(mark).Open))
        .ToArray();

    /// <summary>
    /// Brings the lights and hums up to date with the player's section and place and with the seams' states. A mirror seam
    /// hums while it is usable, passes through her section, and she is near it. Call once per frame.
    /// </summary>
    internal void Refresh(string section, Vector3 feet)
    {
        current = section;
        foreach (var mark in marks)
        {
            var state = StateOf(mark);
            var look = (section, state.Open, state.Severed);
            if (!shown.TryGetValue(mark.Id, out var before) || before != look)
            {
                shown[mark.Id] = look;
                Redraw(mark, state);
            }
            if (hums.TryGetValue(mark.Id, out var hum))
                hum.Active = mark.Sections.Contains(section) && state.Open && !state.Severed
                    && new Vector2(feet.X - mark.Foot.X, feet.Z - mark.Foot.Z).Length() <= MirrorHum.Range;
        }
    }

    private SeamState StateOf(SeamMark mark) => ((SeamReadResult.Found)world.ReadSeam(new(mark.Id))).Seam;

    // The seam is drawn in every section it passes through, but only while it passes through the player's section.
    private void Redraw(SeamMark mark, SeamState state)
    {
        foreach (var scene in scenes.Where(scene => mark.Sections.Contains(scene.Id.Value)))
        {
            var seams = scene.Overlay.GetNode("Seams");
            seams.GetNodeOrNull(mark.Id)?.Free();
            if (mark.Sections.Contains(current)) seams.AddChild(Light(mark, state));
        }
    }

    private static Node3D Light(SeamMark mark, SeamState state)
    {
        var node = new Node3D { Name = mark.Id, Position = mark.Foot };
        var height = state.Severed ? StubHeight : mark.Height;
        node.AddChild(Box("Light", new(LightWidth, height, LightWidth), height / 2, state.Open ? OpenLight : ClosedLight));
        // A severed seam ends in a flat, plainly cut end.
        if (state.Severed) node.AddChild(Box("Cut", new(CapWidth, CapThickness, CapWidth), height + CapThickness / 2, CutEnd));
        return node;
    }

    private static MeshInstance3D Box(string name, Vector3 size, float centerHeight, Color color) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Size = size },
        Position = new(0, centerHeight, 0),
        Layers = LensOnly,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color }
    };
}
