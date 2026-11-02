"""Teaching building 1's scenes, which generate.py writes into teaching1/ next to this file.

Four storeys 3.6 m high on the plaza's east side. From the north: a water room, a narrow stair, three classrooms, the
wide stair in a tower that stands out toward the plaza, three classrooms, a narrow stair and the teachers' toilet, with a
corridor along the east wall past them all. Every storey is built from the same scenes and every classroom from one
classroom's, each instance with ids of its own. Layout and furniture are the greybox prototype's TB1_* constants and its
"teaching building 1, inside" section, in its coordinates.
Decision note: 2026-11-01-school-scene-layout
"""
import math
from collections import namedtuple

from shapes import TOP, fixed, gap, material, pave, resource, show, teacher_toilet, wall, water_room, window
from tscn import COLORS, bodies, box, packed, slope, vec  # on the path once shapes is imported

FH, FLOORS = 3.6, 4  # storey height
STEPS = 12  # risers a flight, two flights a storey
RISE = FH / 2 / STEPS
X0, XC, X1 = 114.5, 123.5, 126.5  # the west wall, the corridor's wall (rooms 9 m deep), the east wall (corridor 3 m wide)
TX = X0 - 1.5  # the tower's west wall

def bays():  # (kind, z0, z1) from the north
    out, z = [], 13.6
    for kind, depth in [('water', 3.6), ('stair', 3.24)] + [('room', 8.4)] * 3 + [('wide', 7)] + [('room', 8.4)] * 3 + [
            ('stair', 3.24), ('toilet', 3.6)]:
        out.append((kind, z, z + depth))
        z += depth
    return out


BAYS = bays()
Z0, Z1 = BAYS[0][1], BAYS[-1][2]
WATER, TOILET = BAYS[0][1:], BAYS[-1][1:]
ROOMS = [(z0, z1) for kind, z0, z1 in BAYS if kind == 'room']

# A stair bay from z0 to z1. Its flights are over f0 to f1: up westward in the north half from the floor's landing at lx
# to the half landing from hx0 to hx1, then on up eastward in the south half to the next floor's landing at lx. The wide
# stair reaches into the tower and leaves a passage from door 1 south of it.
Stair = namedtuple('Stair', 'name z0 z1 f0 f1 hx0 hx1 lx')
NORTH = Stair('North', *BAYS[1][1:], *BAYS[1][1:], X0 + 0.12, X0 + 1.5, X0 + 4.58)
WIDE = Stair('Wide', *BAYS[5][1:], BAYS[5][1], BAYS[5][1] + 4.4, X0 - 1.38, X0 + 1.3, X0 + 4.38)
SOUTH = Stair('South', *BAYS[9][1:], *BAYS[9][1:], X0 + 0.12, X0 + 1.5, X0 + 4.58)
STAIRS = (NORTH, WIDE, SOUTH)
DOOR1, DOOR2 = (WIDE.f1 + 0.36, WIDE.z1 - 0.44), (XC + 0.7, X1 - 0.7)  # in the tower's west wall; in the south wall


def middle(a, b): return (a + b) / 2
def tread(s): return (s.lx - s.hx1) / (STEPS - 1)


# Four columns of double desks with even aisles between them and the walls, six rows deep: the middle of each desk's
# front edge, from the classroom's north-west corner.
AISLE = (XC - X0 - 0.24 - 4 * 1.2) / 5
SEATS = [(0.12 + AISLE + 0.6 + k * (1.2 + AISLE), 2.4 + 0.95 * j) for k in range(4) for j in range(6)]


def west():  # three windows a classroom; over the wide stair's bay the tower takes over
    holes = [gap(WIDE.z0, WIDE.z1, FH), window(middle(*WATER), 1.5, 0.9, 2.4), window(middle(*TOILET), 1.2, 1.6, 2.4)]
    holes += [window(z0 + 8.4 * k / 6, 1.8, 0.9, 2.7) for z0, _ in ROOMS for k in (1, 3, 5)]
    holes += [window(middle(s.z0, s.z1), 1.2, 0.9, 2.4) for s in (NORTH, SOUTH)]
    boxes = []
    wall(boxes, 'z', X0, Z0, Z1, FH, holes)
    return fixed('West', teaching1_west=boxes)


def east():  # a row of windows along the corridor; the north wall
    boxes, holes, z = [], [], Z0 + 1.6
    while z < Z1 - 1:
        holes.append(window(z, 1.5, 0.9, 2.4))
        z += 3.2
    wall(boxes, 'z', X1, Z0, Z1, FH, holes)
    wall(boxes, 'x', Z0, X0, X1, FH, [window(middle(X0, XC), 1.5, 0.9, 2.4), window(middle(XC, X1), 1.5, 0.9, 2.4)])
    return fixed('East', teaching1_east=boxes)


# The walls across the building, and the corridor's wall, open to the stair bays, with two doors to each classroom and one
# to the water room and the toilet, each leaf open into the room; a wall between each stair's flights, and one between
# the wide stair and the passage from door 1. A leaf is 0.9 m long, 5 cm shorter than the prototype's, so that a back
# door's leaf clears the chair beside it.
def inside():
    boxes, holes = [], [gap(s.z0, s.z1, FH) for s in STAIRS]
    for z in [z for z0, z1 in ROOMS for z in (z0 + 0.6, z1 - 1.6)] + [middle(*WATER) - 0.5, middle(*TOILET) - 0.5]:
        holes.append(gap(z, z + 1, 2.1))
        box(boxes, XC - 1.0, XC - 0.1, 0, 2.1, z, z + 0.05, 'door')
    wall(boxes, 'z', XC, Z0, Z1, FH, holes, 0.2)
    for _, z, _ in BAYS[1:]: wall(boxes, 'x', z, TX if z in (WIDE.z0, WIDE.z1) else X0, XC, FH, t=0.2)
    for s in STAIRS: box(boxes, s.hx1, s.lx, 0, FH, middle(s.f0, s.f1) - 0.1, middle(s.f0, s.f1) + 0.1, 'wall')
    box(boxes, WIDE.hx0, WIDE.lx, 0, FH, WIDE.f1 - 0.1, WIDE.f1 + 0.1, 'wall')
    return fixed('Inside', teaching1_inside=boxes)


def flights():  # one storey of each stair: the half landing and two flights of treads 0.35 m thick
    boxes = []
    for s in STAIRS:
        sm, go = middle(s.f0, s.f1), tread(s)
        box(boxes, s.hx0, s.hx1, FH / 2 - 0.2, FH / 2, s.f0, s.f1, 'slab')
        for i in range(STEPS - 1):
            y = (i + 1) * RISE
            box(boxes, s.lx - (i + 1) * go, s.lx - i * go, max(0, y - 0.35), y, s.f0, sm - 0.1, 'slab')
            box(boxes, s.hx1 + i * go, s.hx1 + (i + 1) * go, FH / 2 + y - 0.35, FH / 2 + y, sm + 0.1, s.f1, 'slab')
    return fixed('Flights', teaching1_flights=boxes)


# A storey's floor as rectangles (x0, x1, z0, z1, m), the water room and the toilet tiled. Above the ground floor it is
# open over the stairwells, from each half landing to the floor's landing.
def floor_plan(ground):
    a, b, m = X0 - 0.12, X1 + 0.12, 'cement' if ground else 'slab'
    out = [(a, XC, Z0 - 0.12, WATER[1], 'tile'), (XC, b, Z0 - 0.12, WATER[1], m),
           (a, XC, TOILET[0], Z1 + 0.12, 'tile'), (XC, b, TOILET[0], Z1 + 0.12, m)]
    if ground: return out + [(a, b, WATER[1], TOILET[0], m), (TX - 0.12, a, WIDE.z0, WIDE.z1, m)]
    z = WATER[1]
    for s in STAIRS:
        out += [(a, b, z, s.f0, m), (a, s.hx0, s.f0, s.f1, m), (s.lx, b, s.f0, s.f1, m)]
        z = s.f1
    return out + [(TX - 0.12, a, WIDE.f1, WIDE.z1, m)]


def ends(boxes, ground):  # the south wall and the tower's west wall, with doors 2 and 1 on the ground floor
    wall(boxes, 'x', Z1, X0, X1, FH, [window(middle(X0, XC), 1.2, 1.6, 2.4),
                                      gap(*DOOR2, 2.4) if ground else window(middle(XC, X1), 1.5, 0.9, 2.4)])
    wall(boxes, 'z', TX, WIDE.z0, WIDE.z1, FH, [window(middle(WIDE.f0, WIDE.f1), 2.4, 0.9, 2.4),
                                                gap(*DOOR1, 2.6) if ground else window(middle(*DOOR1), 1.8, 0.9, 2.4)])


# The ground floor's floor, as deep as the yard so that no thin slab can catch her (PR #79); its ends with both doors,
# whose two leaves each stand open inward; and solid stairs under each half landing and second flight.
def ground():
    boxes = []
    for x0, x1, z0, z1, m in floor_plan(True): pave(boxes, x0, x1, z0, z1, TOP, m)
    ends(boxes, True)
    for z in (DOOR1[0] + 0.03, DOOR1[1] - 0.03): box(boxes, TX + 0.12, TX + 1.02, 0, 2.6, z - 0.025, z + 0.025, 'door')
    for x in (DOOR2[0] + 0.03, DOOR2[1] - 0.03): box(boxes, x - 0.025, x + 0.025, 0, 2.4, Z1 - 0.92, Z1 - 0.12, 'door')
    for s in STAIRS:
        sm = middle(s.f0, s.f1)
        box(boxes, s.hx0, s.hx1, 0, FH / 2 - 0.2, s.f0, sm - 0.1, 'slab')
        box(boxes, s.hx0, s.lx, 0, FH / 2 - 0.2, sm + 0.1, s.f1, 'slab')
    return fixed('Ground', teaching1_ground=boxes)


# A floor above the ground floor: its slab 0.2 m thick, the tiles the top half of it so that the ceiling below stays the
# slab's color; and its ends.
def upper():
    boxes = []
    for x0, x1, z0, z1, m in floor_plan(False):
        box(boxes, x0, x1, -0.2, -0.1 if m == 'tile' else 0, z0, z1, 'slab')
        if m == 'tile': box(boxes, x0, x1, -0.1, 0, z0, z1, 'tile')
    ends(boxes, False)
    return fixed('Upper', teaching1_upper=boxes)


# The top floor's rails 1.1 m high where no flight goes up; the roof and its parapet; the tower a storey above the roof,
# whose red pyramid roof is in props.tscn.
def top():
    boxes, y = [], FLOORS * FH
    for s in STAIRS: box(boxes, s.lx - 0.04, s.lx + 0.04, y - FH, y - FH + 1.1, s.f0, middle(s.f0, s.f1), 'iron')
    tx, tz = (TX - 0.12, X0 + 5.9), (WIDE.z0 - 0.12, WIDE.z1 + 0.12)
    box(boxes, X0 - 0.12, X1 + 0.12, y - 0.2, y, Z0 - 0.12, Z1 + 0.12, 'slab')
    box(boxes, tx[0], X0 - 0.12, y - 0.2, y, *tz, 'slab')
    for p, q, r, s in ((X0, X1, Z0, Z0), (X0, X1, Z1, Z1), (X0, X0, Z0, Z1), (X1, X1, Z0, Z1)):
        box(boxes, p - 0.12, q + 0.12, y, y + 1.0, r - 0.12, s + 0.12, 'wall')
    box(boxes, *tx, y, y + 4.6, *tz, 'wall')
    c = middle(WIDE.f0, WIDE.f1)
    box(boxes, tx[0] - 0.06, tx[0], y + 1.2, y + 3.0, c - 0.7, c + 0.7, 'window')
    return fixed('Top', teaching1_top=boxes)


# Both rooms open off the corridor, and their fittings are laid out from its wall: u along the wall, v back from it, y
# over the floor at y0.
def fixture(boxes, y0):
    return lambda m, u0, u1, lo, hi, v0, v1: box(boxes, XC - v1, XC - v0, y0 + lo, y0 + hi, u0, u1, m)


def fittings():  # every floor's water room and teachers' toilet, a men's toilet on the first and third floors
    parts = {}
    for f in range(FLOORS):
        water, wc = [], []
        water_room(fixture(water, f * FH), *WATER, XC - X0, middle(*WATER))
        teacher_toilet(fixture(wc, f * FH), *TOILET, XC - X0, middle(*TOILET), f % 2 == 0)
        parts[f'teaching1_water_{f + 1}'], parts[f'teaching1_toilet_{f + 1}'] = water, wc
    return fixed('Fittings', **parts)


# One classroom's furniture from its north-west corner: the blackboard on the north wall, so that the windows light the
# desks from the left; the platform, which she cannot step onto, as in the old school region; the teacher's desk on it;
# and the double desks. Their chairs are in props.tscn.
def classroom():
    boxes, xm = [], (XC - X0) / 2
    box(boxes, xm - 2, xm + 2, 0.9, 2.1, 0.1, 0.16, 'board')
    box(boxes, xm - 2.6, xm + 2.6, 0, 0.2, 0.1, 1.1, 'slab')
    box(boxes, xm - 0.6, xm + 0.6, 0.2, 1.0, 0.3, 0.85, 'desk')
    for x, z in SEATS: box(boxes, x - 0.6, x + 0.6, 0, 0.72, z, z + 0.45, 'desk')
    return fixed('Classroom', teaching1_class=boxes)


def chairs():  # one classroom's, from its north-west corner, behind the desks
    subs, nodes = [], []
    chair = resource(subs, 'BoxMesh', 'chair', material=material(subs, COLORS['chair'][:3]), size=vec(1, 0.45, 0.35))
    for i, (x, z) in enumerate(SEATS, 1): show(nodes, f'Chair{i}', chair, (x, 0.225, z + 0.675))
    return packed('Chairs', [], subs=subs, nodes=nodes)


# Under section I's Props: the collision-only slopes over the flights, through the front edges of their treads; the
# tower's red pyramid roof; and every classroom's chairs. The chairs and the roof are plain meshes, which the engine does
# not know, so the chairs do not stop her, as in the prototype, and she can walk between the rows of desks.
def props():
    slopes = []
    for f in range(FLOORS - 1):
        y = f * FH
        for s in STAIRS:
            sm, go = middle(s.f0, s.f1), tread(s)
            slopes.append(slope(f'{s.name}Stair{f + 1}West', 'x', s.lx + go, y, s.hx1, y + FH / 2, s.f0 + 0.1, sm - 0.1))
            slopes.append(slope(f'{s.name}Stair{f + 1}East', 'x', s.hx1 - go, y + FH / 2, s.lx, y + FH, sm + 0.1, s.f1 - 0.1))
    subs, nodes = bodies(slopes)
    roof = resource(subs, 'CylinderMesh', 'roof', material=material(subs, COLORS['roof'][:3]), top_radius=0, bottom_radius=5.4,
                    height=3.2, radial_segments=4, rings=0)
    show(nodes, 'Roof', roof, (middle(TX - 0.12, X0 + 5.9), FLOORS * FH + 6.2, middle(WIDE.z0, WIDE.z1)),
         rotation=vec(0, math.pi / 4, 0))
    entries = [(f'Chairs{n}_{k}', 'chairs.tscn', (X0, (n - 1) * FH, z0), []) for n in range(1, FLOORS + 1)
               for k, (z0, _) in enumerate(ROOMS, 1)]
    return packed('Teaching1', entries, subs=subs, nodes=nodes, under='campus/teaching1')


# The building under section I's Fixed: each storey's scenes on every floor and the classroom's in every classroom, with
# ids of their own, numbered from 1 by floor and then from the north.
def building():
    def at(name, path, position, node, eid): return name, path, position, [(node, [('EntityId', f'"{eid}"')])]
    entries = []
    for n in range(1, FLOORS + 1):
        y = (n - 1) * FH
        parts = ['west', 'east', 'inside'] + (['flights'] if n < FLOORS else []) + (['upper'] if n > 1 else [])
        entries += [at(f'{p.title()}{n}', f'{p}.tscn', (0, y, 0), f'teaching1_{p}', f'teaching1_{p}_{n}') for p in parts]
        entries += [at(f'Class{n}_{k}', 'classroom.tscn', (X0, y, z0), 'teaching1_class', f'teaching1_class_{n}_{k}')
                    for k, (z0, _) in enumerate(ROOMS, 1)]
    entries += [(name, f'{name.lower()}.tscn', (0, 0, 0), []) for name in ('Ground', 'Top', 'Fittings')]
    return packed('Teaching1', entries, under='campus/teaching1')


def files():
    scenes = {'west': west(), 'east': east(), 'inside': inside(), 'flights': flights(), 'ground': ground(), 'upper': upper(),
              'top': top(), 'fittings': fittings(), 'classroom': classroom(), 'chairs': chairs(), 'props': props(),
              'building': building()}
    return {f'teaching1/{name}.tscn': text for name, text in scenes.items()}
