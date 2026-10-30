"""The text of the Godot scenes that generate.py writes: entities made of boxes, collision-only slopes, and scenes of instances."""
import math

COLORS = {
    'wall': (0.94, 0.937, 0.918, 1), 'floor': (0.702, 0.675, 0.631, 1), 'tile': (0.784, 0.804, 0.816, 1),
    'slab': (0.851, 0.843, 0.82, 1), 'stair': (0.812, 0.792, 0.753, 1), 'rail': (0.478, 0.502, 0.533, 1),
    'panel': (0.478, 0.502, 0.533, 0.35), 'front': (0.541, 0.231, 0.18, 1), 'ground': (0.655, 0.678, 0.624, 1),
    'furn': (0.561, 0.639, 0.722, 1), 'road': (0.612, 0.608, 0.588, 1), 'walk': (0.722, 0.706, 0.671, 1),
    'fence': (0.247, 0.373, 0.282, 1), 'fence_panel': (0.247, 0.373, 0.282, 0.35), 'trunk': (0.541, 0.518, 0.471, 1),
    'crown': (0.365, 0.478, 0.29, 1), 'sign': (0.435, 0.659, 0.878, 1),
}


def fmt(v):
    s = f"{v:.6f}".rstrip('0').rstrip('.')
    return '0' if s in ('-0', '') else s


def vec(*v): return f"Vector3({', '.join(fmt(c) for c in v)})"


# entities: (name, origin, boxes, properties), with one BoxPart3D per box (x0, x1, y0, y1, z0, z1, material) placed
# relative to the entity's origin
def scene(entities, script='BuildingElement3D', root='Shell'):
    materials, meshes, nodes = {}, {}, []
    for name, origin, boxes, properties in entities:
        nodes.append(f'[node name="{name}" type="Node3D" parent="."]\n' + (f'position = {vec(*origin)}\n' if any(origin) else '')
                     + 'script = ExtResource("e1")\n' + ''.join(f'{k} = {v}\n' for k, v in properties.items()))
        for i, (x0, x1, y0, y1, z0, z1, m) in enumerate(boxes, 1):
            materials.setdefault(m, f"m{len(materials) + 1}")
            mesh = meshes.setdefault((round(x1 - x0, 6), round(y1 - y0, 6), round(z1 - z0, 6), m), f"b{len(meshes) + 1}")
            centre = ((x0 + x1) / 2 - origin[0], (y0 + y1) / 2 - origin[1], (z0 + z1) / 2 - origin[2])
            nodes.append(f'[node name="p{i}" parent="{name}" instance=ExtResource("e2")]\n'
                         f'position = {vec(*centre)}\nmesh = SubResource("{mesh}")\nPartId = "p{i}"\n')
    head = [f'[gd_scene load_steps={3 + len(materials) + len(meshes)} format=3]\n',
            f'[ext_resource type="Script" path="res://adapter/{script}.cs" id="e1"]\n'
            '[ext_resource type="PackedScene" path="res://regions/box_part.tscn" id="e2"]\n']
    for m, mid in materials.items():
        color = COLORS[m]
        head.append(f'[sub_resource type="StandardMaterial3D" id="{mid}"]\nalbedo_color = Color({", ".join(fmt(c) for c in color)})\n'
                    + ('transparency = 1\n' if color[3] < 1 else ''))
    for (sx, sy, sz, m), mid in meshes.items():
        head.append(f'[sub_resource type="BoxMesh" id="{mid}"]\nmaterial = SubResource("{materials[m]}")\nsize = {vec(sx, sy, sz)}\n')
    return '\n'.join(head + [f'[node name="{root}" type="Node3D"]\n'] + nodes)


# A collision-only slope from (a0, y0) to (a1, y1) along x or z, across c0 to c1, whose top is the walking surface: the
# player has no step-up. Returns its name, size, centre and rotation.
def slope(name, along, a0, y0, a1, y1, c0, c1):
    thickness, length, angle = 0.1, round(math.hypot(a1 - a0, y1 - y0), 6), math.atan((y1 - y0) / (a1 - a0))
    # The surface's upward normal is (-sin, cos) in the plane of a and y.
    a, y, c = (a0 + a1) / 2 + math.sin(angle) * thickness / 2, (y0 + y1) / 2 - math.cos(angle) * thickness / 2, (c0 + c1) / 2
    if along == 'x': return name, (length, thickness, round(c1 - c0, 6)), (a, y, c), (0, 0, angle)
    return name, (round(c1 - c0, 6), thickness, length), (c, y, a), (-angle, 0, 0)


# A collision-only box, such as a wall that cannot be seen: its name, size, centre and rotation.
def solid(name, x0, x1, y0, y1, z0, z1):
    return name, (round(x1 - x0, 6), round(y1 - y0, 6), round(z1 - z0, 6)), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (0, 0, 0)


# The shape resources and nodes of slopes and solids, as StaticBody3D children of the scene's root.
def bodies(items):
    shapes, nodes = {}, []
    for name, size, centre, rotation in items:
        sid = shapes.setdefault(size, f"s{len(shapes) + 1}")
        nodes.append(f'[node name="{name}" type="StaticBody3D" parent="."]\nposition = {vec(*centre)}\n'
                     f'rotation = {vec(*rotation)}\ncollision_mask = 0\n')
        nodes.append(f'[node name="Shape" type="CollisionShape3D" parent="{name}"]\nshape = SubResource("{sid}")\n')
    return [f'[sub_resource type="BoxShape3D" id="{sid}"]\nsize = {vec(*size)}\n' for size, sid in shapes.items()], nodes


# Doors as the player meets them. leaves: (name, hinge, yaw, size, swing, state) for each Door3D, whose leaf of that size
# runs along its local x from the hinge when shut; shut: (name, centre, size) for each shut leaf that a building element
# holds, before which an Interactable3D, a little thicker, says the line.
def doors(leaves, shut, line):
    shapes, meshes, nodes = {}, {}, []
    for name, hinge, yaw, size, swing, state in leaves:
        sid, mid = shapes.setdefault(size, f"s{len(shapes) + 1}"), meshes.setdefault(size, f"b{len(meshes) + 1}")
        centre = vec(size[0] / 2, size[1] / 2, 0)
        nodes.append(f'[node name="{name}" type="Node3D" parent="."]\nposition = {vec(*hinge)}\n'
                     + (f'rotation = {vec(0, yaw, 0)}\n' if yaw else '') + f'script = ExtResource("e1")\nState = {state}\nSwing = {fmt(swing)}\n')
        nodes.append(f'[node name="Leaf" type="StaticBody3D" parent="{name}"]\ncollision_mask = 0\nscript = ExtResource("e2")\n')
        nodes.append(f'[node name="Shape" type="CollisionShape3D" parent="{name}/Leaf"]\nposition = {centre}\nshape = SubResource("{sid}")\n')
        nodes.append(f'[node name="Visual" type="MeshInstance3D" parent="{name}/Leaf"]\nlayers = 3\nposition = {centre}\nmesh = SubResource("{mid}")\n')
    for name, centre, size in shut:
        sid = shapes.setdefault(size, f"s{len(shapes) + 1}")
        nodes.append(f'[node name="{name}" type="StaticBody3D" parent="."]\nposition = {vec(*centre)}\ncollision_layer = 4\ncollision_mask = 0\n'
                     f'script = ExtResource("e2")\nPrompt = "开门"\nLine = "{line}"\n')
        nodes.append(f'[node name="Shape" type="CollisionShape3D" parent="{name}"]\nshape = SubResource("{sid}")\n')
    head = [f'[gd_scene load_steps={3 + bool(meshes) + len(shapes) + len(meshes)} format=3]\n',
            '[ext_resource type="Script" path="res://interaction/Door3D.cs" id="e1"]\n'
            '[ext_resource type="Script" path="res://interaction/Interactable3D.cs" id="e2"]\n']
    if meshes: head.append(f'[sub_resource type="StandardMaterial3D" id="m1"]\nalbedo_color = Color({", ".join(fmt(c) for c in COLORS["front"])})\n')
    head += [f'[sub_resource type="BoxShape3D" id="{sid}"]\nsize = {vec(*size)}\n' for size, sid in shapes.items()]
    head += [f'[sub_resource type="BoxMesh" id="{mid}"]\nmaterial = SubResource("m1")\nsize = {vec(*size)}\n' for size, mid in meshes.items()]
    return '\n'.join(head + ['[node name="Doors" type="Node3D"]\n'] + nodes)


# A scene whose root holds instances: (name, scene, position, overrides), where each override is (node, properties) for
# a node of the instanced scene, and scenes are paths under res://regions/compound.
def packed(root, entries, script=None, properties=(), subs=(), nodes=()):
    scenes = list(dict.fromkeys(s for _, s, _, _ in entries))
    ext = ([f'[ext_resource type="Script" path="res://adapter/{script}.cs" id="e0"]\n'] if script else []) + [
        f'[ext_resource type="PackedScene" path="res://regions/compound/{s}" id="e{i}"]\n' for i, s in enumerate(scenes, 1)]
    out = [f'[gd_scene load_steps={1 + len(ext) + len(subs)} format=3]\n', *([''.join(ext)] if ext else []), *subs,
           f'[node name="{root}" type="Node3D"]\n' + ('script = ExtResource("e0")\n' if script else '')
           + ''.join(f'{k} = {v}\n' for k, v in properties)]
    for name, s, position, overrides in entries:
        out.append(f'[node name="{name}" parent="." instance=ExtResource("e{scenes.index(s) + 1}")]\n'
                   + (f'position = {vec(*position)}\n' if any(position) else ''))
        out += [f'[node name="{node}" parent="{name}"]\n' + ''.join(f'{k} = {v}\n' for k, v in values)
                for node, values in overrides if values]
    return '\n'.join(out + list(nodes))
