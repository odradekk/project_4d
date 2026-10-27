using System;
using System.Collections.Generic;
using FourD;
using Godot;

namespace Game.Adapter;

internal static class SliceMesh
{
    /// <summary>Builds the entities of a section; those in <paramref name="skip"/> are built elsewhere.</summary>
    internal static Node3D Build(SectionGeometryResult.Geometry geometry,
        IReadOnlyDictionary<(EntityId, PartId), Material> materials, bool collision,
        Dictionary<EntityId, int> maskIds, IReadOnlySet<EntityId>? skip = null)
    {
        var root = new Node3D { Name = "Geometry" };
        foreach (var slice in geometry.Entities)
            if (skip is null || !skip.Contains(slice.Entity))
                root.AddChild(Entity(slice, materials, maskIds, collision ? new StaticBody3D { Name = "Collision" } : null));
        return root;
    }

    /// <summary>
    /// Builds one entity's slice, with the collision shape added to <paramref name="body"/> when it is given.
    /// Through-objects get a color in the outline mask that stays the same for the entity in every section.
    /// </summary>
    internal static Node3D Entity(SliceGeometry slice, IReadOnlyDictionary<(EntityId, PartId), Material> materials,
        Dictionary<EntityId, int> maskIds, StaticBody3D? body)
    {
        var mesh = new ArrayMesh();
        var start = 0;
        while (start < slice.Parts.Count)
        {
            var part = slice.Parts[start];
            var end = start + 3;
            while (end < slice.Parts.Count && slice.Parts[end] == part) end += 3;
            var vertices = new Vector3[end - start];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                // Engine fronts are CCW; Godot fronts are clockwise.
                var source = start + i / 3 * 3 + (i % 3 == 0 ? 0 : 3 - i % 3);
                vertices[i] = new((float)slice.Positions[source * 3], (float)slice.Positions[source * 3 + 1], (float)slice.Positions[source * 3 + 2]);
                normals[i] = new((float)slice.Normals[source * 3], (float)slice.Normals[source * 3 + 1], (float)slice.Normals[source * 3 + 2]);
                uvs[i] = new((float)slice.Uvs[source * 2], (float)slice.Uvs[source * 2 + 1]);
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, materials[(slice.Entity, part)]);
            start = end;
        }
        var entity = new Node3D { Name = slice.Entity.Value };
        entity.AddChild(new MeshInstance3D { Name = "Surface", Mesh = mesh, Layers = 1 });
        entity.AddChild(new MeshInstance3D { Name = "PrismSurface", Mesh = mesh, Layers = 2 });
        // StandardMaterial3D converts sRGB colors for rendering; keep the mask bytes in sRGB.
        var color = slice.ThroughObject ? MaskColor(slice.Entity, maskIds) : Colors.Black;
        entity.AddChild(new MeshInstance3D
        {
            Name = "PrismMask",
            Mesh = mesh,
            Layers = 4,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = color
            }
        });
        if (body is not null)
        {
            body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
            entity.AddChild(body);
        }
        return entity;
    }

    private static Color MaskColor(EntityId entity, Dictionary<EntityId, int> maskIds)
    {
        if (!maskIds.TryGetValue(entity, out var id))
        {
            id = maskIds.Count + 1;
            if (id > 0xffffff) throw new InvalidOperationException("Too many outlined entities for the 24-bit prism mask.");
            maskIds.Add(entity, id);
        }
        return new Color((id & 255) / 255f, ((id >> 8) & 255) / 255f, ((id >> 16) & 255) / 255f);
    }
}
