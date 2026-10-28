using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

// Fixed objects put in a loaded region and taken out of it, such as chairs a scene turns upside down.
// Decision note: 2026-10-26-region-scene-authoring
public partial class Region3D
{
    /// <summary>
    /// Adds fixed objects to a section: the scene's root is a FixedObject3D with BoxPart3D children, and each is placed at its
    /// transform in the region. The engine checks each as it checks a fixed object that was loaded, and refuses one that would
    /// cut into something; the ones before it stay. Returns null when all are added, otherwise why the engine refused one.
    /// </summary>
    public FailureCode? AddFixed(PackedScene scene, string section, IEnumerable<(string Id, Transform3D At)> placements)
    {
        var added = new List<EntityId>();
        FailureCode? refused = null;
        foreach (var (id, at) in placements)
        {
            refused = AddOne(scene, id, section, at);
            if (refused is not null) break;
            added.Add(new(id));
        }
        // The slices of the whole batch are cut once for each section.
        if (added.Count > 0)
            foreach (var sectionScene in sectionScenes)
            {
                var geometry = (SectionGeometryResult.Geometry)World!.GetSectionGeometry(sectionScene.Id);
                foreach (var slice in geometry.Entities.Where(slice => added.Contains(slice.Entity)))
                {
                    var node = SliceMesh.Entity(slice, loader!.Materials, sectionScene.MaskIds, new StaticBody3D { Name = "Collision" });
                    viewports[sectionScene.Id.Value].GetNode("Geometry").AddChild(node);
                    sectionScene.StaticNodes[slice.Entity] = node;
                }
            }
        return refused;
    }

    /// <summary>Takes a fixed object out of the region. Returns null when it is gone, otherwise why the engine refused.</summary>
    public FailureCode? RemoveFixed(string entityId)
    {
        var id = new EntityId(entityId);
        if (World!.RemoveEntity(id) is RemoveEntityResult.Rejected rejected) return rejected.Failure.Code;
        foreach (var sectionScene in sectionScenes)
            if (sectionScene.StaticNodes.Remove(id, out var node)) node.Free();
        return null;
    }

    // The instance is read where it will stand, under the section it is added to, and then thrown away: the engine and
    // the section worlds hold what it describes.
    private FailureCode? AddOne(PackedScene scene, string entityId, string section, Transform3D at)
    {
        var instance = scene.Instantiate<FixedObject3D>();
        instance.EntityId = entityId;
        instance.Transform = at;
        GetNode($"Sections/{section}/Fixed").AddChild(instance);
        var reader = new SceneReader(this);
        var definitions = new List<EntityDefinition>();
        reader.ReadFixed(instance, definitions);
        instance.GetParent().RemoveChild(instance);
        instance.Free();
        if (reader.Errors.Count > 0)
        {
            foreach (var error in reader.Errors) GD.PushError($"{error.Source.NodePath}.{error.Source.Property}: {error.Detail}");
            return FailureCode.InvalidInput;
        }
        if (World!.AddEntity(definitions.Single()) is AddEntityResult.Rejected rejected) return rejected.Failure.Code;
        foreach (var (key, material) in reader.Materials) loader!.Materials[key] = material;
        return null;
    }
}
