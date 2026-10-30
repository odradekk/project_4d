"""Writes the building scenes of the compound region next to this file: python game/regions/compound/generate.py

The layout is the greybox prototype's (branch prototype/home-layout, prototypes/home-layout/index.html), in metres with x
east, z south and y up; walls are placed by their centrelines. The origin is where the flat's north wall meets its
kitchen's west wall, and y = 0 is the ground floor. Hand edits to the scenes are lost when this runs again.
Decision note: 2026-10-29-home-scene-layout
"""
import math
from pathlib import Path

H, SLAB = 3.0, 0.2  # room height and floor slab
S = H + SLAB  # storey
FLOORS, HOME = 3, 1  # Xiaoman's flat is on the second floor, index 1
EXT, INT = 0.24, 0.12  # outer and inner wall thickness
DOOR_H = 2.0
E, D = 7.85, 7.35  # the flat's east and south walls
B = E + 1.35  # the balcony's outer wall
CW, V = 1.72, 1.7  # width of the open corridor along the west side; depth of the shared vestibule
zB = 2 * D + V  # the neighbor's flat mirrors ours across the vestibule and ends here, at the stair hall
zS = zB + 4.25  # the stair hall ends here; the other half of the building mirrors this one
LEN = zB + zS
SC = LEN / 2
STEPS = 14  # risers per flight, two flights per storey
RISE = S / (2 * STEPS)
GO = 0.38
RUN = (STEPS - 1) * GO
SX = E - EXT / 2 - 1.4 - RUN  # the flights start here; 1.4 m landing at the turn
GROUND = -SLAB  # the ground outside, under the ground floor's slab


def door(a, b, front=False): return dict(a=a, b=b, lo=0, hi=DOOR_H, front=front)
def win(a, b, lo=0.9, hi=2.3): return dict(a=a, b=b, lo=lo, hi=hi, front=False)
def balcony_win(a, b): return win(a, b, 1.0, 2.6)  # over a parapet
def wall(along, at, span, t, holes=(), h=H): return dict(along=along, at=at, span=span, t=t, holes=holes, h=h)


# along: the axis the wall runs on; at: its place on the other axis
walls = [
    wall('x', 0, (0, E), EXT),
    wall('z', 0, (0, D), EXT, [win(0.9, 2.5)]),
    wall('x', D, (0, E), EXT, [door(0.25, 1.25, front=True)]),
    wall('z', E, (0, D), EXT, [win(0.3, 2.7), door(3.0, 3.85), win(5.1, 6.9)]),
    wall('x', 0, (E, B), EXT, [balcony_win(E + 0.15, B - 0.15)]),
    wall('z', B, (0, 4.0), EXT, [balcony_win(0.15, 3.85)]),
    wall('x', 4.0, (E, B), EXT, [balcony_win(E + 0.15, B - 0.15)]),
    wall('z', 1.8, (0, 4.0), INT),
    wall('x', 4.0, (0, 1.8), INT, [door(0.6, 1.4)]),
    wall('x', 4.0, (1.8, E), INT, [door(2.15, 3.05)]),
    wall('z', 3.25, (4.0, D), INT, [door(4.15, 5.05)]),
    wall('z', 1.8, (5.5, D), INT),
    wall('x', 5.5, (1.8, 3.25), INT, [door(2.2, 2.9)]),
]
vestibule = [wall('z', 0, (D, D + V), EXT, [door(D + 0.4, D + 1.3)]), wall('z', 1.8, (D, D + V), EXT)]
hall_west = wall('z', -CW, (zB, zS), EXT, [win(zB + 1.4, zS - 1.4)], S)
hall_east = wall('z', E, (zB, zS), EXT, [win(zB + 1.0, zS - 1.0, 1.2, 2.6)], S)
rooms = [  # x, z and floor of the kitchen, living room, balcony, her room, hallway, entrance hall and bathroom
    ((0, 1.8), (0, 4.0), 'tile'), ((1.8, E), (0, 4.0), 'floor'), ((E, B), (0, 4.0), 'tile'),
    ((3.25, E), (4.0, D), 'floor'), ((0, 3.25), (4.0, 5.5), 'floor'), ((0, 1.8), (5.5, D), 'floor'),
    ((1.8, 3.25), (5.5, D), 'tile'),
]
furniture = [  # id, x, z, height
    ('stove', (0.12, 0.72), (0.4, 2.8), 0.8), ('piano', (1.86, 2.46), (0.8, 2.3), 1.25),
    ('sofa', (4.0, 6.0), (0.12, 0.97), 0.85), ('tea_table', (4.4, 5.6), (1.7, 2.3), 0.45),
    ('father_bed', (6.53, 7.73), (0.5, 2.5), 0.45), ('tv_stand', (4.4, 5.6), (3.49, 3.94), 0.6),
    ('bed', (5.73, 7.73), (4.06, 5.56), 0.45), ('desk', (7.13, 7.73), (5.8, 7.0), 0.75),
    ('wardrobe', (3.31, 3.91), (5.6, 7.1), 2.0), ('basin', (2.71, 3.19), (6.0, 6.5), 0.8),
    ('squat_toilet', (1.95, 2.45), (6.6, 7.1), 0.05),
]
COLORS = {
    'wall': (0.94, 0.937, 0.918, 1), 'floor': (0.702, 0.675, 0.631, 1), 'tile': (0.784, 0.804, 0.816, 1),
    'slab': (0.851, 0.843, 0.82, 1), 'stair': (0.812, 0.792, 0.753, 1), 'rail': (0.478, 0.502, 0.533, 1),
    'bars': (0.478, 0.502, 0.533, 0.35), 'front': (0.541, 0.231, 0.18, 1), 'ground': (0.655, 0.678, 0.624, 1),
    'furn': (0.561, 0.639, 0.722, 1),
}


# A box is (x0, x1, y0, y1, z0, z1, material).
def box(boxes, x0, x1, y0, y1, z0, z1, m):
    if x1 - x0 > 1e-3 and y1 - y0 > 1e-3 and z1 - z0 > 1e-3: boxes.append((x0, x1, y0, y1, z0, z1, m))


def mirror(boxes, about): return [(x0, x1, y0, y1, about - z1, about - z0, m) for x0, x1, y0, y1, z0, z1, m in boxes]
def lift(boxes, dy): return [(x0, x1, y0 + dy, y1 + dy, z0, z1, m) for x0, x1, y0, y1, z0, z1, m in boxes]


# front_open: the front door is a doorway, as Xiaoman's is, instead of a shut leaf
def wall_boxes(boxes, w, front_open=False):
    e = w['t'] / 2

    def piece(a, b, lo, hi, m='wall', half=e):
        if w['along'] == 'x': box(boxes, a, b, lo, hi, w['at'] - half, w['at'] + half, m)
        else: box(boxes, w['at'] - half, w['at'] + half, lo, hi, a, b, m)

    start = w['span'][0] - e
    for hole in sorted(w['holes'], key=lambda hole: hole['a']):
        piece(start, hole['a'], 0, w['h'])
        piece(hole['a'], hole['b'], 0, hole['lo'])
        piece(hole['a'], hole['b'], hole['hi'], w['h'])
        if hole['front'] and not front_open: piece(hole['a'], hole['b'], 0, DOOR_H, 'front', 0.03)
        start = hole['b']
    piece(start, w['span'][1] + e, 0, w['h'])


# Xiaoman's flat in full; every other flat only by its outer walls, front door shut
def flat(home):
    boxes = []
    for w in walls:
        if home or w['t'] == EXT: wall_boxes(boxes, w, home)
    if home:  # sunk into the slab: a box thinner than 1 cm is no solid to the engine
        for (x0, x1), (z0, z1), m in rooms: box(boxes, x0, x1, -0.02, 0.004, z0, z1, m)
    return boxes


# floor slabs under the north half of a floor: a flat with the corridor beside it, the vestibule, the neighbor's flat
def half_slabs():
    boxes = []
    box(boxes, -CW, E + EXT / 2, -SLAB, 0, -EXT / 2, D + EXT / 2, 'slab')
    box(boxes, E, B + EXT / 2, -SLAB, 0, -EXT / 2, 4.0 + EXT / 2, 'slab')
    box(boxes, -CW, 1.8 + EXT / 2, -SLAB, 0, D, D + V, 'slab')
    return boxes + mirror(boxes, zB)


# A railing: a handrail over a see-through infill of bars, which stops the player too.
def rail(boxes, along, at, a, b, h=1.1):
    for y0, y1, m in ((0, h - 0.06, 'bars'), (h - 0.06, h, 'rail')):
        if along == 'z': box(boxes, at - 0.03, at + 0.03, y0, y1, a, b, m)
        else: box(boxes, a, b, y0, y1, at - 0.03, at + 0.03, m)


# The north half of a floor without its first flat: the neighbor's flat, the vestibule, slabs and the corridor's railings.
def half_rest():
    boxes = mirror(flat(False), zB) + half_slabs()
    for w in vestibule: wall_boxes(boxes, w)
    rail(boxes, 'z', -CW, 0, zB)
    rail(boxes, 'x', 0, -CW, -EXT / 2)
    return boxes


def stair(f):
    boxes = []
    # The ground floor's landing runs under the flights too, so that she can walk out from under them.
    box(boxes, -CW, SX if f else E - EXT / 2, -SLAB, 0, zB, zS, 'slab')
    if f > 0: wall_boxes(boxes, hall_west)  # on the ground floor this side is the open way out
    else:  # under the middle of the fourth tread of the westward flight; beyond it she would be wedged under the turn
        x = SX + RUN - 3.5 * GO
        box(boxes, x - INT / 2, x + INT / 2, 0, S / 2 + 4 * RISE - 0.3, SC + 0.06, zS - EXT / 2, 'wall')
    wall_boxes(boxes, hall_east)
    if f < FLOORS - 1:
        for i in range(1, STEPS):
            box(boxes, SX + (i - 1) * GO, SX + i * GO, i * RISE - 0.3, i * RISE, zB + EXT / 2, SC - 0.06, 'stair')  # up eastward
            box(boxes, SX + RUN - i * GO, SX + RUN - (i - 1) * GO, S / 2 + i * RISE - 0.3, S / 2 + i * RISE,
                SC + 0.06, zS - EXT / 2, 'stair')  # up westward
        box(boxes, SX + RUN, E - EXT / 2, S / 2 - SLAB, S / 2, zB, zS, 'slab')  # landing at the turn
        box(boxes, SX, SX + RUN, 0, S, SC - 0.06, SC + 0.06, 'wall')  # wall between the flights
    else:
        rail(boxes, 'z', SX, zB + EXT / 2, SC)  # at the stairwell
        rail(boxes, 'x', SC, SX, SX + RUN)  # along the top of the wall below, which is level with this floor
    return boxes


def roof():
    boxes = []
    box(boxes, -CW, E + EXT / 2, -SLAB, 0, zB, zS, 'slab')
    return lift(half_slabs() + mirror(half_slabs(), LEN) + boxes, FLOORS * S)


def fmt(v):
    s = f"{v:.6f}".rstrip('0').rstrip('.')
    return '0' if s in ('-0', '') else s


def vec(*v): return f"Vector3({', '.join(fmt(c) for c in v)})"


def element(eid, boxes):
    return eid, (0, 0, 0), boxes, {'EntityId': f'"{eid}"', 'BuildingId': '"home"', 'WMin': '-0.5', 'WMax': '0.5'}


# entities: (name, origin, boxes, properties), with one BoxPart3D per box placed relative to the entity's origin
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


# A collision-only slope from (xa, ya) to (xb, yb) whose top is the walking surface: the player has no step-up.
def slope(name, xa, ya, xb, yb, z0, z1):
    thickness, dx, dy = 0.1, xb - xa, yb - ya
    length = math.hypot(dx, dy)
    angle = math.atan2(dy, abs(dx)) * math.copysign(1, dx)
    nx, ny = -dy / length * math.copysign(1, dx), abs(dx) / length  # the surface's upward normal
    centre = ((xa + xb) / 2 - nx * thickness / 2, (ya + yb) / 2 - ny * thickness / 2, (z0 + z1) / 2)
    return name, (round(length, 6), thickness, round(z1 - z0, 6)), centre, angle


# Slopes over each flight, through the front edges of its treads, and one over the step out of the ground floor.
def props():
    slopes = []
    for f in range(FLOORS - 1):
        y = f * S
        slopes.append(slope(f'Floor{f}Up', SX - GO, y, SX + RUN, y + S / 2, zB + EXT / 2, SC - 0.06))
        slopes.append(slope(f'Floor{f}Turn', SX + RUN + GO, y + S / 2, SX, y + S, SC + 0.06, zS - EXT / 2))
    slopes.append(slope('Exit', -CW - 0.75, GROUND, -CW, 0, zB, zS))
    shapes = {}
    for _, size, _, _ in slopes: shapes.setdefault(size, f"s{len(shapes) + 1}")
    out = [f'[gd_scene load_steps={1 + len(shapes)} format=3]\n']
    out += [f'[sub_resource type="BoxShape3D" id="{sid}"]\nsize = {vec(*size)}\n' for size, sid in shapes.items()]
    out.append('[node name="Props" type="Node3D"]\n')
    for name, size, centre, angle in slopes:
        out.append(f'[node name="{name}Slope" type="StaticBody3D" parent="."]\nposition = {vec(*centre)}\n'
                   f'rotation = {vec(0, 0, angle)}\ncollision_mask = 0\n')
        out.append(f'[node name="Shape" type="CollisionShape3D" parent="{name}Slope"]\nshape = SubResource("{shapes[size]}")\n')
    return '\n'.join(out)


def furniture_scene():
    entities = []
    for eid, (x0, x1), (z0, z1), h in furniture:
        y = HOME * S
        entities.append((eid, ((x0 + x1) / 2, y, (z0 + z1) / 2), [(x0, x1, y, y + h, z0, z1, 'furn')], {'EntityId': f'"{eid}"'}))
    return scene(entities, 'FixedObject3D', 'Furniture')


def main():
    files = {'flat.tscn': scene([element('Flat', lift(flat(True), HOME * S))])}
    for f in range(FLOORS):
        north = half_rest() if f == HOME else flat(False) + half_rest()
        files[f'floor{f}_north.tscn'] = scene([element(f'Floor{f}North', lift(north, f * S))])
        files[f'floor{f}_south.tscn'] = scene([element(f'Floor{f}South', lift(mirror(flat(False) + half_rest(), LEN), f * S))])
    files['stair.tscn'] = scene([element(f'Floor{f}Stair', lift(stair(f), f * S)) for f in range(FLOORS)])
    files['roof.tscn'] = scene([element('Roof', roof())])
    # Temporary: a patch of ground around the building until the compound is built.
    files['ground.tscn'] = scene([element('Ground', [(-8, 16, GROUND - 0.2, GROUND, -6, LEN + 6, 'ground')])])
    files['furniture.tscn'] = furniture_scene()
    files['props.tscn'] = props()
    here = Path(__file__).parent
    for name, text in files.items(): (here / name).write_text(text, encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
