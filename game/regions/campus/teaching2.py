"""Teaching building 2's scenes, which generate.py writes into teaching2/ next to this file.

Six storeys 3.6 m high on the plaza's west side, round an atrium open from the ground floor to a pointed glass roof: a ring
of 16 rooms a floor round a corridor 2.4 m wide round the atrium, and a spiral stair at each end of the atrium. Doors 1 and
2, in the east wall to the plaza and the west wall to the courts, open into halls on the ground floor; above the halls are
rooms. Every storey is built from the same scenes, each instance with ids of its own. Layout, rooms and furniture are the
greybox prototype's TB2_* constants and its "teaching building 2, inside" section, in its coordinates.
Decision note: 2026-11-01-school-scene-layout
"""
import math
from collections import namedtuple

from shapes import TOP, WT, fixed, gap, material, pave, resource, show, teacher_toilet, wall, water_room, window
from tscn import COLORS, box, fmt, packed, vec  # on the path once shapes is imported

FH, FLOORS = 3.6, 6  # storey height
X0, X1, Z0, Z1 = 37.5, 64.5, 18, 76  # the outer walls
CX0, CX1, CZ0, CZ1 = 44.1, 57.9, 25.2, 68.8  # the corridor's outer walls: rooms 6.6 m deep east and west, 7.2 north and south
AX0, AX1, AZ0, AZ1 = 46.5, 55.5, 27.6, 66.4  # the atrium, inside the corridor
# Both spiral stairs' columns stand on x SX, and the atrium's parapets are open OPEN either side of it, where SpiralStair3D
# leaves its rail open. Each stair: its column's z, and the angle from north, clockwise, at which the corridors meet it.
SX, OPEN = 51, 1.1
SPIRALS = (('North', 29.3, 0), ('South', 64.7, math.pi))
DOOR1, DOOR2 = (28, 30.4), (36, 38.4)  # in the east wall to the plaza and the west wall to the courts

# The rooms clockwise from the north-west corner, each on the side of the corridor its wall with the doors is on. hall: 1
# and 2 for the bays of doors 1 and 2, halls on the ground floor and rooms above.
Room = namedtuple('Room', 'x0 x1 z0 z1 side hall', defaults=(0,))
ROOMS = ([Room(a, b, Z0, CZ0, 'n') for a, b in ((X0, AX0), (AX0, AX1), (AX1, X1))]
         + [Room(CX1, X1, a, b, 'e', *h) for a, b, *h in ((25.2, 33.2, 1), (33.2, 42.1), (42.1, 51), (51, 59.9), (59.9, 68.8))]
         + [Room(a, b, CZ1, Z1, 's') for a, b in ((AX1, X1), (AX0, AX1), (X0, AX0))]
         + [Room(X0, CX0, a, b, 'w', *h) for a, b, *h in ((59.6, 68.8), (50.4, 59.6), (41.2, 50.4), (33.2, 41.2, 2), (25.2, 33.2))])
WATER, TOILET = ROOMS[0], ROOMS[10]  # in the north-west and south-west corners
# The furnished rooms by floor, numbered from 1 in ROOMS' order, as the prototype's NAMES assigns them: the fifth and sixth
# years' classrooms on floors 2 and 3, laboratories on floor 4, computer rooms on floor 5. The other rooms stay empty.
CLASSES = (2, 3, 5, 6, 7, 8)
FURNISHED = {2: ('class', CLASSES), 3: ('class', CLASSES), 4: ('lab', (2, 3, 5, 6)), 5: ('computer', (2, 3, 4, 5, 6))}
SIDES = {'n': 'north', 'e': 'east', 's': 'south', 'w': 'west'}


def corridor(r):  # the line of the room's wall on the corridor, and the stretch of it that faces the corridor
    if r.side in 'ns': return (CZ0 if r.side == 'n' else CZ1), max(r.x0, CX0), min(r.x1, CX1)
    return (CX1 if r.side == 'e' else CX0), r.z0, r.z1


def doors(r):  # where each door, 1 m wide and 2.1 m high, starts: one into a short room, two into a long one
    _, p, q = corridor(r)
    return [p + 0.6, q - 1.6] if q - p >= 6 else [(p + q) / 2 - 0.5]


# Each door's leaf, standing open into the room at right angles to the wall, 0.9 m long as in teaching building 1. It hangs
# where the door starts, except on two kinds of door, where it hangs at the door's far end: a room's second door, whose
# leaf would stand in a classroom's last desk; and the toilet's door, whose leaf would leave her no way past the screen.
def leaves(boxes, r):
    at, _, _ = corridor(r)
    way = 1 if r.side in 'se' else -1
    v = sorted((at + way * 0.1, at + way * 1.0))
    for i, a in enumerate(doors(r)):
        u = a + 0.95 if i or r is TOILET else a
        if r.side in 'ns': box(boxes, u, u + 0.05, 0, 2.1, *v, 'door')
        else: box(boxes, *v, 0, 2.1, u, u + 0.05, 'door')


def outside(r):  # the sides on which the room has an outer wall
    return [s for s, on in (('n', r.z0 == Z0), ('s', r.z1 == Z1), ('w', r.x0 == X0), ('e', r.x1 == X1)) if on]


def windows(r, side):  # evenly along the room's outer wall, as many as whole 3 m it has; the toilet's high up
    a, b = (r.x0, r.x1) if side in 'ns' else (r.z0, r.z1)
    n = int((b - a) // 3)
    return [window(a + (b - a) * (k + 0.5) / n, *((1.2, 1.6, 2.4) if r is TOILET else (1.8, 0.9, 2.4))) for k in range(n)]


# One side of the building, the same on every floor: the outer wall, the wall of that side's rooms on the corridor with
# their doors, and the walls between them. halls() builds the halls' bays, which differ on the ground floor.
def wing(side):
    along, rooms, boxes = ('x' if side in 'ns' else 'z'), [r for r in ROOMS if r.side == side], []
    outer, inner = {'n': (Z0, CZ0), 's': (Z1, CZ1), 'w': (X0, CX0), 'e': (X1, CX1)}[side]
    bays = [gap(r.z0, r.z1, FH) for r in rooms if r.hall]
    holes = [h for r in ROOMS if not r.hall and side in outside(r) for h in windows(r, side)]
    wall(boxes, along, outer, *((X0, X1) if along == 'x' else (Z0, Z1)), FH, bays + holes)
    ends = (X0, X1) if along == 'x' else (CZ0, CZ1)
    wall(boxes, along, inner, *ends, FH, bays + [gap(a, a + 1, 2.1) for r in rooms if not r.hall for a in doors(r)], 0.2)
    for r in rooms:
        if not r.hall: leaves(boxes, r)
    for c in sorted({c for r in rooms for c in ((r.x0, r.x1) if along == 'x' else (r.z0, r.z1))} - set(ends)):
        wall(boxes, 'z' if along == 'x' else 'x', c, *sorted((outer, inner)), FH, t=0.2)
    return fixed(SIDES[side].title(), **{f'teaching2_{SIDES[side]}': boxes})


# The bays of doors 1 and 2. On the ground floor: the door in the outer wall, and the hall open to the corridor. Above: the
# room's windows, and its wall on the corridor with two doors and their leaves.
def halls(boxes, ground):
    for r, door in ((ROOMS[3], DOOR1), (ROOMS[14], DOOR2)):
        outer, inner = (X1, CX1) if r.side == 'e' else (X0, CX0)
        wall(boxes, 'z', outer, r.z0 + WT / 2, r.z1 - WT / 2, FH, [gap(*door, 2.6)] if ground else windows(r, r.side))
        if not ground:
            wall(boxes, 'z', inner, r.z0 + 0.1, r.z1 - 0.1, FH, [gap(a, a + 1, 2.1) for a in doors(r)], 0.2)
            leaves(boxes, r)


# A storey's floor as rectangles (x0, x1, z0, z1, m) out to the outer walls' outer faces, split at the walls' centrelines:
# the water room and the toilet tiled, and on the ground floor the atrium too.
def floor_plan(ground):
    a, b, c, d, m = X0 - WT / 2, X1 + WT / 2, Z0 - WT / 2, Z1 + WT / 2, 'cement' if ground else 'slab'
    out = [(a, AX0, c, CZ0, 'tile'), (AX0, b, c, CZ0, m), (a, AX0, CZ1, d, 'tile'), (AX0, b, CZ1, d, m),
           (a, AX0, CZ0, CZ1, m), (AX1, b, CZ0, CZ1, m), (AX0, AX1, CZ0, AZ0, m), (AX0, AX1, AZ1, CZ1, m)]
    return out + [(AX0, AX1, AZ0, AZ1, 'tile')] if ground else out


# The ground floor's floor, as deep as the yard so that no thin slab can catch her (PR #79); the halls' bays with doors 1
# and 2, whose two leaves each stand open inward.
def ground():
    boxes = []
    for x0, x1, z0, z1, m in floor_plan(True): pave(boxes, x0, x1, z0, z1, TOP, m)
    halls(boxes, True)
    for z in (DOOR1[0] + 0.03, DOOR1[1] - 0.03): box(boxes, X1 - 1.02, X1 - 0.12, 0, 2.6, z - 0.025, z + 0.025, 'door')
    for z in (DOOR2[0] + 0.03, DOOR2[1] - 0.03): box(boxes, X0 + 0.12, X0 + 1.02, 0, 2.6, z - 0.025, z + 0.025, 'door')
    return fixed('Ground', teaching2_ground=boxes)


# A floor above the ground floor: its slab 0.2 m thick round the atrium, the tiles the top half of it so that the ceiling
# below stays the slab's color; the parapets 1.1 m high round the atrium, open where the spiral stairs meet the corridor;
# and the halls' bays.
def upper():
    boxes = []
    for x0, x1, z0, z1, m in floor_plan(False):
        box(boxes, x0, x1, -0.2, -0.1 if m == 'tile' else 0, z0, z1, 'slab')
        if m == 'tile': box(boxes, x0, x1, -0.1, 0, z0, z1, 'tile')
    for z in (AZ0, AZ1):
        for p, q in ((AX0, SX - OPEN), (SX + OPEN, AX1)): box(boxes, p, q, 0, 1.1, z - 0.08, z + 0.08, 'brick')
    for x in (AX0, AX1): box(boxes, x - 0.08, x + 0.08, 0, 1.1, AZ0, AZ1, 'brick')
    halls(boxes, False)
    return fixed('Upper', teaching2_upper=boxes)


def frame(boxes, x0, x1, z0, z1, lo, hi):  # walls 0.24 m thick round a rectangle, by their centrelines
    for p, q, r, s in ((x0, x1, z0, z0), (x0, x1, z1, z1), (x0, x0, z0, z1), (x1, x1, z0, z1)):
        box(boxes, p - WT / 2, q + WT / 2, lo, hi, r - WT / 2, s + WT / 2, 'wall')


def top():  # the roof round the atrium, its parapet, and the curb round the atrium; the glass roof is in props.tscn
    boxes, y = [], FLOORS * FH
    for x0, x1, z0, z1, _ in floor_plan(False): box(boxes, x0, x1, y - 0.2, y, z0, z1, 'slab')
    frame(boxes, X0, X1, Z0, Z1, y, y + 1.0)
    frame(boxes, AX0, AX1, AZ0, AZ1, y, y + 0.4)
    return fixed('Top', teaching2_top=boxes)


# A storey's water room and teachers' toilet, laid out from their walls on the corridor as the prototype's roomFrame does:
# the water room's door in its south wall, the toilet's in its north wall.
def fittings(men):
    water, wc = [], []
    water_room(lambda m, u0, u1, lo, hi, v0, v1: box(water, u0, u1, lo, hi, CZ0 - v1, CZ0 - v0, m),
               WATER.x0, WATER.x1, CZ0 - Z0, doors(WATER)[0] + 0.5)
    teacher_toilet(lambda m, u0, u1, lo, hi, v0, v1: box(wc, u0, u1, lo, hi, CZ1 + v0, CZ1 + v1, m),
                   TOILET.x0, TOILET.x1, Z1 - CZ1, doors(TOILET)[0] + 0.5, men)
    return fixed('Fittings', teaching2_water=water, teaching2_toilet=wc)


def rows(v, step, depth, end):  # the prototype's rows: from v, step apart, as long as a row depth deep ends short of end
    out = []
    while v + depth < end:
        out.append(v)
        v += step
    return out


# One room's furniture from its north-west corner, as the prototype's furnish lays it out from the front wall, the west
# wall north of the atrium and the north wall east of it: u runs across the room and v back from that wall. Returns the
# boxes and where the chairs stand; the chairs are in props.tscn. Room 4 runs 8 m north to south, not 8.9 m as the others
# east of the atrium, but its computer desks come out the same.
def furnish(kind, r):
    north, boxes, chairs = r.side == 'n', [], []
    U, V = (r.z1 - r.z0, r.x1 - r.x0) if north else (r.x1 - r.x0, r.z1 - r.z0)

    def put(m, u0, u1, lo, hi, v0, v1):
        if north: box(boxes, v0, v1, lo, hi, u0, u1, m)
        else: box(boxes, u0, u1, lo, hi, v0, v1, m)

    put('board', U / 2 - 2, U / 2 + 2, 0.9, 2.1, 0.1, 0.16)
    if kind == 'class':  # a platform she cannot step onto, the teacher's desk on it, three columns of double desks
        put('slab', U / 2 - 2.6, U / 2 + 2.6, 0, 0.2, 0.1, 1.1)
        put('desk', U / 2 - 0.6, U / 2 + 0.6, 0.2, 1.0, 0.3, 0.85)
        aisle = (U - 0.24 - 3 * 1.2) / 4
        for v in rows(2.4, 0.95, 0.85, V - 0.3):
            for k in range(3):
                c = 0.12 + aisle + 0.6 + k * (1.2 + aisle)
                put('desk', c - 0.6, c + 0.6, 0, 0.72, v, v + 0.45)
                chairs.append((v + 0.675, c) if north else (c, v + 0.675))
    elif kind == 'lab':  # long benches across the room
        for v in rows(2.2, 1.8, 0.75, V - 0.6): put('concrete', 0.8, U - 0.8, 0, 0.9, v, v + 0.75)
    else:  # rows of desks, a monitor at each seat
        for v in rows(2.0, 1.5, 0.6, V - 0.6):
            put('desk', 0.6, U - 0.6, 0, 0.75, v, v + 0.6)
            for c in rows(1.0, 1.0, 0, U - 0.8): put('glass', c - 0.25, c + 0.25, 0.75, 1.15, v + 0.1, v + 0.4)
    return boxes, chairs


SAMPLE = {'north': ROOMS[1], 'east': ROOMS[4]}  # a room of each shape, whose furniture every room of that shape gets


def furnished():  # (floor, number, room, kind, the room's shape) of each furnished room
    return [(n, k, ROOMS[k - 1], kind, 'north' if ROOMS[k - 1].side == 'n' else 'east')
            for n, (kind, numbers) in FURNISHED.items() for k in numbers]


def chairs(shape):  # one classroom's
    subs, nodes = [], []
    size = vec(0.35, 0.45, 1) if shape == 'north' else vec(1, 0.45, 0.35)
    chair = resource(subs, 'BoxMesh', 'chair', material=material(subs, COLORS['chair'][:3]), size=size)
    for i, (x, z) in enumerate(furnish('class', SAMPLE[shape])[1], 1): show(nodes, f'Chair{i}', chair, (x, 0.225, z))
    return packed('Chairs', [], subs=subs, nodes=nodes)


def spirals():  # SpiralStair3D.cs builds each one when the region loads; the south one is turned half round
    nodes = [f'[node name="{name}" type="Node3D" parent="."]\nposition = {vec(SX, 0, z)}\n' + (f'rotation = {vec(0, -e, 0)}\n' if e else '')
             + f'script = ExtResource("e1")\nFloors = {FLOORS}\nEntityId = "teaching2_spiral_{name.lower()}"\n' for name, z, e in SPIRALS]
    return '\n'.join(['[gd_scene load_steps=2 format=3]\n', '[ext_resource type="Script" path="res://regions/campus/SpiralStair3D.cs" id="e1"]\n',
                      '[node name="Spirals" type="Node3D"]\n', *nodes])


# Under section I's Props, plain meshes the engine does not know: the spiral stairs; every classroom's chairs, which do not
# stop her, as in teaching building 1; and over the atrium the pointed glass roof, 3.5 m high from the curb's top, with its
# ridge and 17 pairs of rafters.
def props():
    entries = [('Spirals', 'spirals.tscn', (0, 0, 0), [])]
    entries += [(f'Chairs{n}_{k}', f'chairs_{shape}.tscn', (r.x0, (n - 1) * FH, r.z0), [])
                for n, k, r, kind, shape in furnished() if kind == 'class']
    y, h, half, zm, depth = FLOORS * FH + 0.4, 3.5, (AX1 - AX0) / 2, (AZ0 + AZ1) / 2, AZ1 - AZ0
    pitch, length, subs, nodes = math.atan2(h, half), math.hypot(h, half), [], []
    glass = resource(subs, 'StandardMaterial3D', 'glass', transparency=1,
                     albedo_color=f'Color({", ".join(fmt(c) for c in COLORS["glass_roof"])})')
    iron = material(subs, COLORS['iron'][:3])
    pane = resource(subs, 'BoxMesh', 'pane', material=glass, size=vec(length, 0.02, depth))
    gable = resource(subs, 'PrismMesh', 'gable', material=glass, left_to_right=0.5, size=vec(2 * half, h, 0.02))
    rafter = resource(subs, 'BoxMesh', 'rafter', material=iron, size=vec(length, 0.08, 0.08))
    show(nodes, 'GlassWest', pane, (AX0 + half / 2, y + h / 2, zm), rotation=vec(0, 0, pitch))
    show(nodes, 'GlassEast', pane, (AX1 - half / 2, y + h / 2, zm), rotation=vec(0, 0, -pitch))
    for name, z in (('GableNorth', AZ0), ('GableSouth', AZ1)): show(nodes, name, gable, (AX0 + half, y + h / 2, z))
    show(nodes, 'Ridge', resource(subs, 'BoxMesh', 'ridge', material=iron, size=vec(0.12, 0.12, depth)), (AX0 + half, y + h, zm))
    for k in range(17):
        z = AZ0 + k * depth / 16
        show(nodes, f'Rafter{k + 1}West', rafter, (AX0 + half / 2, y + h / 2, z), rotation=vec(0, 0, pitch))
        show(nodes, f'Rafter{k + 1}East', rafter, (AX1 - half / 2, y + h / 2, z), rotation=vec(0, 0, -pitch))
    return packed('Teaching2', entries, subs=subs, nodes=nodes, under='campus/teaching2')


# The building under section I's Fixed: each storey's scenes on every floor and the furniture in each furnished room, with
# ids of their own, numbered from 1 by floor and room.
def building():
    def at(name, path, position, *ids): return name, path, position, [(node, [('EntityId', f'"{eid}"')]) for node, eid in ids]
    entries = []
    for n in range(1, FLOORS + 1):
        y = (n - 1) * FH
        for part in list(SIDES.values()) + (['upper'] if n > 1 else []):
            entries.append(at(f'{part.title()}{n}', f'{part}.tscn', (0, y, 0), (f'teaching2_{part}', f'teaching2_{part}_{n}')))
        entries.append(at(f'Fittings{n}', f'fittings_{"men" if n % 2 else "women"}.tscn', (0, y, 0),
                          ('teaching2_water', f'teaching2_water_{n}'), ('teaching2_toilet', f'teaching2_toilet_{n}')))
    for n, k, r, kind, shape in furnished():
        entries.append(at(f'Room{n}_{k}', f'{kind}_{shape}.tscn', (r.x0, (n - 1) * FH, r.z0),
                          (f'teaching2_{kind}_{shape}', f'teaching2_{kind}_{n}_{k}')))
    entries += [(name, f'{name.lower()}.tscn', (0, 0, 0), []) for name in ('Ground', 'Top')]
    return packed('Teaching2', entries, under='campus/teaching2')


def files():
    scenes = {SIDES[s]: wing(s) for s in SIDES}
    scenes.update({'ground': ground(), 'upper': upper(), 'top': top(), 'fittings_men': fittings(True),
                   'fittings_women': fittings(False), 'spirals': spirals(), 'props': props(), 'building': building()})
    for kind in ('class', 'lab', 'computer'):
        for shape, r in SAMPLE.items():
            scenes[f'{kind}_{shape}'] = fixed(f'{kind.title()}{shape.title()}', **{f'teaching2_{kind}_{shape}': furnish(kind, r)[0]})
    scenes.update({f'chairs_{shape}': chairs(shape) for shape in SAMPLE})
    return {f'teaching2/{name}.tscn': text for name, text in scenes.items()}
