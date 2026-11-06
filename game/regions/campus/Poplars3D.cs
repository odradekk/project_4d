using Game.Adapter;
using Godot;

namespace Game.Regions;

/// <summary>
/// Poplars standing on the ground at y = 0, one at each of <see cref="At"/> (x, z), as the compound's: a trunk 0.3 m square
/// and 5 m tall under a crown 2.4 m square from 4 m up to 17 m. The trunks stop her: they are fixed objects of the section,
/// poplars_1, poplars_2 and so on, 256 trunks each, because a slice draws each part as a surface of its own and a mesh holds
/// at most 256. The crowns, out of her reach, are a plain mesh the engine does not know. It is all
/// built when the node is ready, before the region reads its section, as the spiral stair's blocks are, because the campus's
/// hundreds of poplars written out as boxes would not fit in a scene file of the size the repository allows.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class Poplars3D : Node3D
{
    private const int Most = 256;

    [Export] public Vector2[] At { get; set; } = [];

    public override void _Ready()
    {
        var part = GD.Load<PackedScene>("res://regions/box_part.tscn");
        var trunk = new BoxMesh { Size = new(.3f, 5, .3f), Material = new StandardMaterial3D { AlbedoColor = new(0x8a8478ff) } };
        var crowns = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = new(2.4f, 13, 2.4f), Material = new StandardMaterial3D { AlbedoColor = new(0x5d7a4aff) } },
            InstanceCount = At.Length
        };
        FixedObject3D trunks = null!;
        for (var i = 0; i < At.Length; i++)
        {
            if (i % Most == 0) AddChild(trunks = new FixedObject3D { Name = $"Trunks{i / Most + 1}", EntityId = $"poplars_{i / Most + 1}" });
            var box = part.Instantiate<BoxPart3D>();
            box.PartId = $"p{i % Most + 1}";
            box.Mesh = trunk;
            box.Position = new(At[i].X, 2.5f, At[i].Y);
            trunks.AddChild(box);
            crowns.SetInstanceTransform(i, new(Basis.Identity, new(At[i].X, 10.5f, At[i].Y)));
        }
        AddChild(new MultiMeshInstance3D { Name = "Crowns", Multimesh = crowns, Layers = 3 });
    }
}
