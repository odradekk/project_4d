using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Lines and marks in the section worlds that only the prism's lens shows: the preview of a turn, and the
/// points where a moved slice was stopped. Plain colored lines, as the white-box scope of the note says.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal sealed class LensOverlay
{
    internal static readonly Color Clear = new(.77f, .86f, 1f);
    internal static readonly Color Blocked = new(1f, .23f, .16f);
    internal static readonly Color Fold = new(1f, .92f, .6f);

    private const double MarkSeconds = .5;
    private const uint LensOnly = 2;

    private readonly IReadOnlyList<SectionScene> scenes;
    private readonly SphereMesh markMesh = new() { Radius = .04f, Height = .08f, Material = Unlit(Blocked) };
    private double markRemaining;

    internal LensOverlay(IReadOnlyList<SectionScene> scenes)
    {
        this.scenes = scenes;
        foreach (var scene in scenes) scene.Overlay.AddChild(new Node3D { Name = "Contacts" });
    }

    /// <summary>
    /// Draws in one section the outlines a turn sweeps, pale until the first obstruction and red from it, and the
    /// fold planes of a flip, replacing what was drawn before.
    /// </summary>
    internal void ShowPreview(SectionId section, RotationPreview? sweep, IEnumerable<Polyline> foldPlanes)
    {
        ClearPreview();
        List<Vector3> clear = [], blocked = [], fold = [];
        foreach (var sample in sweep?.Samples ?? [])
            foreach (var contour in sample.Contours) AddSegments(sample.Blocked ? blocked : clear, contour);
        foreach (var plane in foldPlanes) AddSegments(fold, plane);
        var mesh = new ArrayMesh();
        AddSurface(mesh, clear, Clear);
        AddSurface(mesh, blocked, Blocked);
        AddSurface(mesh, fold, Fold);
        if (mesh.GetSurfaceCount() == 0) return;
        scenes.Single(scene => scene.Id == section).Overlay.AddChild(new MeshInstance3D
        {
            Name = "Preview",
            Mesh = mesh,
            Layers = LensOnly,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    internal void ClearPreview()
    {
        foreach (var scene in scenes) scene.Overlay.GetNodeOrNull("Preview")?.Free();
    }

    /// <summary>Marks where a slice was stopped, in the lens of every section, for a short time.</summary>
    internal void MarkContacts(IEnumerable<Contact> contacts)
    {
        ClearMarks();
        foreach (var scene in scenes)
            foreach (var contact in contacts)
                scene.Overlay.GetNode("Contacts").AddChild(new MeshInstance3D
                {
                    Mesh = markMesh,
                    Layers = LensOnly,
                    Position = new((float)contact.Point.X, (float)contact.Point.Y, (float)contact.Point.Z)
                });
        markRemaining = MarkSeconds;
    }

    internal void Tick(double delta)
    {
        if (markRemaining <= 0) return;
        markRemaining -= delta;
        if (markRemaining <= 0) ClearMarks();
    }

    private void ClearMarks()
    {
        foreach (var mark in scenes.SelectMany(scene => scene.Overlay.GetNode("Contacts").GetChildren())) mark.Free();
    }

    private static void AddSegments(List<Vector3> vertices, Polyline line)
    {
        var points = line.Positions.Count / 3;
        var segments = line.Closed ? points : points - 1;
        for (var i = 0; i < segments; i++)
        {
            vertices.Add(PointOf(line, i));
            vertices.Add(PointOf(line, (i + 1) % points));
        }
    }

    private static Vector3 PointOf(Polyline line, int index) =>
        new((float)line.Positions[index * 3], (float)line.Positions[index * 3 + 1], (float)line.Positions[index * 3 + 2]);

    private static void AddSurface(ArrayMesh mesh, List<Vector3> vertices, Color color)
    {
        if (vertices.Count == 0) return;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, Unlit(color));
    }

    // Drawn over everything, so the swept range shows through the walls it passes.
    private static StandardMaterial3D Unlit(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = color,
        NoDepthTest = true
    };
}
