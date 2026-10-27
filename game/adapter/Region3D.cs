using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Characters;
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
    private Dictionary<string, double> sectionPositions = new();
    private readonly Dictionary<string, Character3D> characters = [];
    private Dictionary<string, World3D> worlds = new();
    internal World? World { get; private set; }
    internal PlayerActions? Actions { get; private set; }
    internal SeamTravel? Travel { get; private set; }

    /// <summary>The character that stands at the spawn with this entity ID.</summary>
    public Character3D Character(string entity) => characters[entity];

    /// <summary>The section the player is in, as the engine has it.</summary>
    public string CurrentSection => Actions?.CurrentSection ?? "";

    /// <summary>Returns the engine's distance between the current section and a source section.</summary>
    public double SectionDistance(string sourceSection)
    {
        if (World is null) throw new InvalidOperationException("Region is not loaded.");
        return World.Distance4(new(0, 0, 0, sectionPositions[CurrentSection]),
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
        World = null;
        Actions = null;
        characters.Clear();
        Travel = null;
        sectionPositions.Clear();
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
            var geometry = (SectionGeometryResult.Geometry)preview;
            // The authored lettering shows where the object has a slice at this W, and nowhere else.
            foreach (var (id, labels) in reader.Lettering)
                foreach (var label in labels) label.Visible = geometry.Entities.Any(slice => slice.Entity == id);
            generated.AddChild(SliceMesh.Build(geometry, reader.Materials, false, []));
            Loaded = true;
            return;
        }
        var spawned = reader.Spawns.Select(s => (Spawn: s, Node: s.CharacterScene!.Instantiate())).ToArray();
        var players = spawned.Where(s => s.Node is FirstPersonPlayer).ToArray();
        var strangers = spawned.Where(s => s.Node is not (FirstPersonPlayer or Character3D)).ToArray();
        if (players.Length != 1 || strangers.Length > 0)
        {
            foreach (var (_, node) in spawned) node.Free();
            Fail(players.Length != 1
                ? [new(LoadErrorCode.InvalidGeometry, new("Sections", "Spawns"), null, null, null, "This static stage requires exactly one player spawn.", [])]
                : [new(LoadErrorCode.InvalidGeometry, new(GetPathTo(strangers[0].Spawn), "CharacterScene"), null, null, null, "Assign a FirstPersonPlayer or Character3D scene.", [])]);
            return;
        }
        var spawn = players[0].Spawn;
        var player = (FirstPersonPlayer)players[0].Node;
        // The authored lettering stays in the scene as a template; the adapter puts a copy on the flat object.
        foreach (var label in reader.Lettering.Values.SelectMany(labels => labels)) label.Visible = false;
        var movable = description.Entities
            .Where(entity => entity is not (BuildingElementDefinition or FixedDefinition or Creature3Definition))
            .Select(entity => entity.Id)
            .ToHashSet();
        worlds = new Dictionary<string, World3D>();
        var sectionNodes = new Dictionary<string, Node>();
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
            sectionNodes.Add(section.Id.Value, viewport);
        }
        var home = SceneReader.Ancestor<Section3D>(spawn)!;
        World = world;
        sectionPositions = description.Sections.ToDictionary(s => s.Id.Value, s => s.W);
        previousWorld = GetViewport().World3D;
        currentWorld = worlds[home.SectionId];
        GetViewport().World3D = currentWorld;
        player.Transform = SceneReader.RelativeTransform(spawn, this);
        generated.AddChild(player);
        prism = new PrismView { Name = "Prism" };
        generated.AddChild(prism);
        prism.Configure(player.Camera, LensTargets(home.SectionId), PrismEnabled);
        Actions = new PlayerActions { Name = "Actions" };
        generated.AddChild(Actions);
        Actions.Configure(world, spawn.EntityId, player, prism, scenes,
            new DynamicSlices(world, scenes, reader.Materials, movable, reader.Lettering), new LensOverlay(scenes), this);
        Travel = new SeamTravel { Name = "Travel" };
        generated.AddChild(Travel);
        Travel.Configure(world, spawn.EntityId, player, prism, Actions, new SeamDisplay(world, reader.Seams, scenes, generated), EnterSection);
        player.Bind(prism, Actions, Travel);
        // Characters walk in the physics world of their section, so each is a child of that section's viewport.
        foreach (var (npcSpawn, node) in spawned.Where(s => s.Node is Character3D))
        {
            var character = (Character3D)node;
            character.Transform = SceneReader.RelativeTransform(npcSpawn, this);
            if (npcSpawn.DisplayName != "") character.DisplayName = npcSpawn.DisplayName;
            character.Bind(new CreatureLink(world, npcSpawn.EntityId, spawn.EntityId, id => sectionNodes[id]), player, this);
            sectionNodes[SceneReader.Ancestor<Section3D>(npcSpawn)!.SectionId].AddChild(character);
            characters.Add(npcSpawn.EntityId, character);
        }
        Diagnostics = "";
        Loaded = true;
    }

    // The sections the lens can show from a section, nearest first.
    private (string Id, World3D World)[] LensTargets(string home) => sectionPositions.Keys.Where(id => id != home)
        .OrderBy(id => World!.Distance4(new(0, 0, 0, sectionPositions[id]), new(0, 0, 0, sectionPositions[home])))
        .ThenBy(id => sectionPositions[id]).ThenBy(id => id, StringComparer.Ordinal)
        .Select(id => (id, worlds[id])).ToArray();

    // The player has crossed a seam: the view, the physics and the lens move to the section she is in now, and the lens
    // shows the one she came from.
    private void EnterSection(string from, string to)
    {
        currentWorld = worlds[to];
        GetViewport().World3D = currentWorld;
        prism!.SetTargets(LensTargets(to), from);
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
