using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Loads one authored region and owns its engine world and generated section worlds.
/// Decision note: 2026-10-26-region-scene-authoring
/// Decision note: 2026-10-26-game-layer-csharp
/// </summary>
[Tool, GlobalClass]
public partial class Region3D : Node3D
{
    private World? liveWorld;
    private Dictionary<string, double> sectionPositions = new();
    public string CurrentSection { get; private set; } = "";
    public PlayerActions? Actions { get; private set; }
    internal World? World => liveWorld;

    /// <summary>Returns the engine's distance between the current section and a source section.</summary>
    public double SectionDistance(string sourceSection)
    {
        if (liveWorld is null) throw new InvalidOperationException("Region is not loaded.");
        return liveWorld.Distance4(new(0, 0, 0, sectionPositions[CurrentSection]),
            new(0, 0, 0, sectionPositions[sourceSection]));
    }

    private double previewW;
    private bool prismEnabled = true;
    private Node? generated;
    private PrismView? prism;
    private World3D? previousWorld;
    private World3D? currentWorld;
    private bool previewPending;
    [Export]
    public double PreviewW
    {
        get => previewW;
        set
        {
            previewW = value;
            if (IsInsideTree() && Engine.IsEditorHint() && !previewPending)
            {
                previewPending = true;
                Callable.From(Rebuild).CallDeferred();
            }
        }
    }
    [Export]
    public bool PrismEnabled
    {
        get => prismEnabled;
        set { prismEnabled = value; prism?.SetEnabled(value); }
    }
    public bool Loaded { get; private set; }
    public string Diagnostics { get; private set; } = "";

    public override void _Ready() => Rebuild();

    private void Rebuild()
    {
        previewPending = false;
        if (!IsInsideTree()) return;
        Loaded = false;
        liveWorld = null;
        Actions = null;
        sectionPositions.Clear();
        CurrentSection = "";
        if (generated is not null) { RemoveChild(generated); generated.Free(); }
        generated = null;
        var reader = new SceneReader(this);
        var result = reader.Load();
        var description = reader.Description!;
        foreach (var part in SceneReader.Descendants(this).OfType<BoxPart3D>()) part.Visible = false;
        if (result is LoadResult.Invalid invalid) { Fail(invalid.Errors); return; }
        var world = ((LoadResult.Loaded)result).World;
        generated = Engine.IsEditorHint() ? new Node3D { Name = "Generated" } : new Node { Name = "Generated" };
        AddChild(generated);
        if (Engine.IsEditorHint())
        {
            var preview = world.GetGeometryAtW(PreviewW);
            if (preview is SectionGeometryResult.Rejected rejected)
            { Fail([new(LoadErrorCode.InvalidNumber, new(".", "PreviewW"), null, null, null, rejected.Failure.Detail, [])]); return; }
            generated.AddChild(SliceMesh.Build((SectionGeometryResult.Geometry)preview, reader.Materials, false, []));
            Loaded = true;
            return;
        }
        if (reader.Spawns.Count != 1)
        { Fail([new(LoadErrorCode.InvalidGeometry, new("Sections", "Spawns"), null, null, null, "This static stage requires exactly one player spawn.", [])]); return; }
        var spawn = reader.Spawns[0];
        var character = spawn.CharacterScene!.Instantiate();
        if (character is not FirstPersonPlayer player)
        {
            character.Free();
            Fail([new(LoadErrorCode.InvalidGeometry, new(GetPathTo(spawn), "CharacterScene"), null, null, null, "Assign a FirstPersonPlayer scene.", [])]);
            return;
        }
        // The authored lettering stays in the scene as a template; the adapter puts a copy on the flat object.
        foreach (var label in reader.Lettering.Values.SelectMany(labels => labels)) label.Visible = false;
        var movable = reader.Lettering.Keys.ToHashSet();
        var worlds = new Dictionary<string, World3D>();
        var scenes = new List<SectionScene>();
        foreach (var section in description.Sections)
        {
            var viewport = new SubViewport
            {
                Name = section.Id.Value,
                Size = new Vector2I(2, 2),
                World3D = new World3D(),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
            };
            generated.AddChild(viewport);
            var scene = new SectionScene(section.Id, section.W);
            scenes.Add(scene);
            viewport.AddChild(SliceMesh.Build((SectionGeometryResult.Geometry)world.GetSectionGeometry(section.Id), reader.Materials, true,
                scene.MaskIds, movable));
            viewport.AddChild(scene.Dynamic);
            viewport.AddChild(scene.Overlay);
            viewport.AddChild(new WorldEnvironment
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.Color,
                    BackgroundColor = new Color(0.10f, 0.13f, 0.17f),
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = Colors.White,
                    AmbientLightEnergy = 0.65f
                }
            });
            viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -25, 0), LightEnergy = 0.8f });
            worlds.Add(section.Id.Value, viewport.World3D);
        }
        var home = SceneReader.Ancestor<Section3D>(spawn)!;
        liveWorld = world;
        sectionPositions = description.Sections.ToDictionary(s => s.Id.Value, s => s.W);
        CurrentSection = home.SectionId;
        previousWorld = GetViewport().World3D;
        currentWorld = worlds[home.SectionId];
        GetViewport().World3D = currentWorld;
        player.Transform = SceneReader.RelativeTransform(spawn, this);
        generated.AddChild(player);
        prism = new PrismView { Name = "Prism" };
        generated.AddChild(prism);
        prism.Configure(player.Camera, description.Sections.Where(s => s.Id.Value != home.SectionId)
            .OrderBy(s => world.Distance4(new(0, 0, 0, s.W), new(0, 0, 0, home.W))).ThenBy(s => s.W).ThenBy(s => s.Id.Value, StringComparer.Ordinal)
            .Select(s => (s.Id.Value, worlds[s.Id.Value])).ToArray(), PrismEnabled);
        Actions = new PlayerActions { Name = "Actions" };
        generated.AddChild(Actions);
        Actions.Configure(world, spawn.EntityId, player, prism, scenes,
            new DynamicSlices(world, scenes, reader.Materials, reader.Lettering), new LensOverlay(scenes));
        player.Bind(prism, Actions);
        Diagnostics = "";
        Loaded = true;
    }

    private void Fail(IReadOnlyList<LoadError> errors)
    {
        Diagnostics = string.Join("\n", errors.OrderBy(e => e.Source.NodePath, StringComparer.Ordinal)
            .ThenBy(e => e.Source.Property, StringComparer.Ordinal).ThenBy(e => e.Code.ToString(), StringComparer.Ordinal)
            .Select(e => $"{e.Code} {e.Source.NodePath}.{e.Source.Property} {e.Entity?.Value} {e.Operation?.Value}[{e.StepIndex}]: {e.Detail}"));
        foreach (var line in Diagnostics.Split('\n')) GD.PushError(line);
        if (!Engine.IsEditorHint())
        {
            if (generated is not null) { RemoveChild(generated); generated.Free(); }
            generated = new CanvasLayer { Name = "LoadFailure" };
            AddChild(generated);
            generated.AddChild(new Label { Position = new Vector2(20, 20), Text = OS.IsDebugBuild() ? Diagnostics : "区域载入失败" });
        }
    }

    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint() && currentWorld is not null && GetViewport().World3D == currentWorld)
        {
            GetViewport().World3D = previousWorld;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
