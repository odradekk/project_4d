"""What generate.py and teaching1.py share: the wall thickness, the yard's levels, walls with holes in them, paving,
scenes of fixed objects, and plain meshes for a section's Props. Importing it puts the compound region's tscn.py, which
writes the scene text, on the path."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / 'compound'))
from tscn import box, fmt, scene, vec  # noqa: E402

WT = 0.24  # brick wall thickness
GROUND = -0.2  # the bottom of the yard's slab
# Paving and floors stand this much over the yard, lines a little more; each reaches into the yard, 1 cm thick at least.
TOP, LINE = 0.004, 0.008


def gap(a, b, h): return (a, b, 0, h, False)
def window(c, w, lo, hi): return (c - w / 2, c + w / 2, lo, hi, True)


# A wall along x (at z) or along z (at x) from a to b, h high from y0, reaching half its thickness past each end so that
# corners close. holes: (a, b, lo, hi, glass) with lo and hi over y0, glass for a pane in it. The wall is cut into bands at
# the holes' sills and heads, and each band into the runs between the holes that span it.
def wall(boxes, along, at, a, b, h, holes=(), t=WT, y0=0, m='wall'):
    def piece(p, q, lo, hi, mm=m, tt=t):
        if along == 'x': box(boxes, p, q, y0 + lo, y0 + hi, at - tt / 2, at + tt / 2, mm)
        else: box(boxes, at - tt / 2, at + tt / 2, y0 + lo, y0 + hi, p, q, mm)

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
