"""What generate.py and the modules it calls share: the brick walls' size, the yard's levels, walls with holes in them, boxes
along a line and the prototype's fence posts, paving, scenes of fixed objects, plain meshes for a section's Props, windows
on a block, poplars, and the prototype's random numbers; and what the two teaching buildings share, the fittings of a water
room and a teachers' toilet. Importing it puts the compound region's tscn.py, which writes the scene text, on the path."""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / 'compound'))
from tscn import COLORS, box, fmt, packed, scene, vec  # noqa: E402

WT, WALL_H = 0.24, 2.4  # brick walls: the thickness, and the height of the school's wall and the staff compound's
GROUND = -0.2  # the bottom of the yard's slab
# Paving and floors stand this much over the yard, lines a little more; each reaches into the yard, 1 cm thick at least.
TOP, LINE = 0.004, 0.008


def gap(a, b, h): return (a, b, 0, h, False)
def window(c, w, lo, hi): return (c - w / 2, c + w / 2, lo, hi, True)


# A wall along x (at z) or along z (at x) from a to b, h high from y0, reaching half its thickness past each end so that
# corners close. holes: (a, b, lo, hi, glass) with lo and hi over y0, glass for a pane in it. The wall is cut into bands at
# the holes' sills and heads, and each band into the runs between the holes that span it.
def wall(boxes, along, at, a, b, h, holes=(), t=WT, y0=0, m='wall'):
    def piece(p, q, lo, hi, mm=m, tt=t): beam(boxes, along, at, p, q, y0 + lo, y0 + hi, tt / 2, mm)

    levels = sorted({0, h, *(y for _, _, lo, hi, _ in holes for y in (lo, hi))})
    for lo, hi in zip(levels, levels[1:]):
        start = a - t / 2
        for p, q, hole_lo, hole_hi, _ in sorted(holes):
            if hole_lo <= lo and hi <= hole_hi:
                piece(start, p, lo, hi)
                start = q
        piece(start, b + t / 2, lo, hi)
    for p, q, lo, hi, glass in holes:
        if glass: piece(p, q, lo, hi, 'pane', 0.04)


def beam(boxes, along, at, a, b, lo, hi, t, m):  # a box from a to b along x (at z) or along z (at x), t either side
    if along == 'x': box(boxes, a, b, lo, hi, at - t, at + t, m)
    else: box(boxes, at - t, at + t, lo, hi, a, b, m)


def runs(a, b, gaps):  # the stretches of a to b outside the gaps (p, q), which are in order
    out, s = [], a
    for p, q in [*gaps, (b, b)]:
        if p > s: out.append((s, p))
        s = q
    return out


def posts(a, b, step):  # where the prototype's fence() puts posts from a to b: as near step apart as fits, one at each end
    n = max(1, math.floor((b - a) / step + 0.5))  # JavaScript's Math.round
    return [a + (b - a) * i / n for i in range(n + 1)]


def pave(boxes, x0, x1, z0, z1, top, m): box(boxes, x0, x1, GROUND, top, z0, z1, m)


def fixed(root, **parts):  # a scene of fixed objects, one for each id and its boxes
    return scene([(eid, (0, 0, 0), boxes, {'EntityId': f'"{eid}"'}) for eid, boxes in parts.items()], 'FixedObject3D', root)


# The text of meshes that the engine does not know, for a section's Props: resource adds a sub-resource to subs and
# returns a reference to it, material adds one of that color, and show adds a node to nodes that draws a mesh.
def resource(subs, kind, rid, **values):
    subs.append(f'[sub_resource type="{kind}" id="{rid}"]\n' + ''.join(f'{k} = {v}\n' for k, v in values.items()))
    return f'SubResource("{rid}")'


def material(subs, rgb):
    return resource(subs, 'StandardMaterial3D', f'c{len(subs)}', albedo_color=f'Color({", ".join(fmt(c) for c in rgb)}, 1)')


def show(nodes, name, mesh, position, **values):
    nodes.append(f'[node name="{name}" type="MeshInstance3D" parent="."]\nlayers = 3\nposition = {vec(*position)}\n'
                 + ''.join(f'{k} = {v}\n' for k, v in values.items()) + f'mesh = {mesh}\n')


# For many copies of a mesh, more than a scene file holds as nodes: multimesh adds a MultiMesh that draws it upright at each
# of spots, (x, y, z), and show_many a node that shows one.
def multimesh(subs, rid, mesh, spots):
    buffer = ', '.join(f'1, 0, 0, {fmt(x)}, 0, 1, 0, {fmt(y)}, 0, 0, 1, {fmt(z)}' for x, y, z in spots)
    return resource(subs, 'MultiMesh', rid, transform_format=1, instance_count=len(spots), mesh=mesh,
                    buffer=f'PackedFloat32Array({buffer})')


def show_many(nodes, name, many, position=(0, 0, 0)):
    nodes.append(f'[node name="{name}" type="MultiMeshInstance3D" parent="."]\nlayers = 3\n'
                 + (f'position = {vec(*position)}\n' if any(position) else '') + f'multimesh = {many}\n')


# A scene of the prototype's windows on a block w along x and d along z from the scene's origin: 1.5 × 1.4 m, as many as fit
# 3.3 m apart along each face named in sides (n, s, e, w), 0.9 m over each of its floors, which are fh apart.
# One MultiMesh draws the north and south faces' and one the east and west faces', shown again on every floor.
def windows(w, d, floors, fh, sides='nsew'):
    subs, nodes, many = [], [], {}
    glass = material(subs, COLORS['window'][:3])

    def along(length):
        n = int(length // 3.3)
        return [(length - n * 3.3) / 2 + (i + 0.5) * 3.3 for i in range(n)]
    y = 0.9 + 0.7
    ns = [(c, y, z) for side, z in (('n', -0.03), ('s', d + 0.03)) if side in sides for c in along(w)]
    ew = [(x, y, c) for side, x in (('w', -0.03), ('e', w + 0.03)) if side in sides for c in along(d)]
    for rid, name, size, spots in (('ns', 'NorthSouth', (1.5, 1.4, 0.06), ns), ('ew', 'EastWest', (0.06, 1.4, 1.5), ew)):
        if spots: many[name] = multimesh(subs, rid, resource(subs, 'BoxMesh', f'{rid}b', material=glass, size=vec(*size)), spots)
    for f in range(floors):
        for name, mm in many.items(): show_many(nodes, f'{name}{f + 1}', mm, (0, f * fh, 0))
    return packed('Windows', [], subs=subs, nodes=nodes)


# A scene of poplars that Poplars3D.cs builds when the region loads, one at each (x, z) of at; a scene file cannot hold
# so many boxes. Their trunks are the fixed objects poplars_1, poplars_2 and so on, or entity_1 and on when it is given.
def poplars_scene(at, entity=None):
    spots = ', '.join(f'{fmt(round(x, 3))}, {fmt(round(z, 3))}' for x, z in at)
    return '\n'.join(['[gd_scene load_steps=2 format=3]\n', '[ext_resource type="Script" path="res://regions/campus/Poplars3D.cs" id="e1"]\n',
                      '[node name="Poplars" type="Node3D"]\nscript = ExtResource("e1")\n'
                      + (f'EntityPrefix = "{entity}"\n' if entity else '') + f'At = PackedVector2Array({spots})\n'])


def numbers(seed):  # the prototype's random numbers from that state on
    while True:
        seed = seed * 16807 % 2147483647
        yield seed / 2147483647


def inside(v, gaps): return any(a < v < b for a, b in gaps)


# A water room's and a teachers' toilet's fittings, laid out as the prototype's roomFrame does from the wall with the door:
# put(m, u0, u1, lo, hi, v0, v1) draws a box from u0 to u1 along that wall, v0 to v1 back from it and lo to hi over the
# floor. The room runs from ua to ub along the wall and v back from it; its door is centred at door.
def water_room(put, ua, ub, v, door):  # a boiler for drinking water, a long sink with taps on the far wall, a mop basin
    put('iron', ua + 0.2, ua + 1.0, 0, 1.8, 0.5, 1.7)
    put('pole', ua + 1.0, ua + 1.15, 0.8, 0.9, 1.0, 1.2)
    put('concrete', ua + 0.4, ub - 0.4, 0, 0.8, v - 0.72, v - 0.12)
    u = ua + 0.8
    while u < ub - 0.5:
        put('pole', u - 0.03, u + 0.03, 1.0, 1.08, v - 0.42, v - 0.12)
        u += 0.6
    m = ua + 1.3 if door - ua > ub - door + 0.5 else ub - 1.0  # the mop basin, clear of the door
    put('concrete', m, m + 0.8, 0, 0.45, 0.5, 1.4)


# A screen inside the door, basins at one end, squatting stalls at the other, and for men a urinal trough on the far wall.
# The pans stand 4 cm high, as in the toilet block, so that she can walk over them.
def teacher_toilet(put, ua, ub, v, door, men):
    stalls = 2 if men else 3
    put('wall', door - 0.9, door + 0.9, 0, 2.0, 1.4, 1.5)
    put('concrete', ua + 0.12, ua + 0.62, 0, 0.8, 2.0, 3.6)
    for i in range(stalls + 1): put('furn', ub - 1.5, ub - 0.12, 0, 1.8, v - 0.15 - i * 1.1, v - 0.09 - i * 1.1)
    for i in range(stalls): put('white', ub - 1.1, ub - 0.5, -0.02, 0.04, v - 0.92 - i * 1.1, v - 0.42 - i * 1.1)
    if men: put('concrete', ua + 0.3, ub - 1.8, 0, 0.25, v - 0.5, v - 0.12)
