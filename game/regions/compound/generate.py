"""Writes the scenes of the compound region next to this file, all but compound.tscn: python game/regions/compound/generate.py

The layout is the greybox prototype's (branch prototype/home-layout, prototypes/home-layout/index.html), in metres with x
east, z south and y up; walls are placed by their centrelines. The origin is where the flat's north wall meets its
kitchen's west wall, and y = 0 is the ground floor. Hand edits to the scenes are lost when this runs again.
Decision note: 2026-10-29-home-scene-layout
"""
import math
from pathlib import Path

from tscn import bodies, box, doors, lift, mirror, packed, scene, slope

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

# The compound: 16 identical buildings, ours at (0, 0) in the north-west corner. A building's x and z are its origin's.
COLS, ROWS = (0, 21, 42, 63), (0, 45, 105, 150)
WEST_ROAD, CROSS_ROAD = (-18, -11), (90, 97)  # x of the road along the west side; z of the road to the gate
FENCE_X0, FENCE_X1, FENCE_Z0, FENCE_Z1 = -24, 78, -12, 197  # the gate is in the east fence, across the road to the gate
MAIN_ROAD = 91  # x where the main road's motor lanes start, outside the gate
SIDEWALK = (78, 84)  # x of the sidewalk outside the gate
BUS_X, BUS_Z = 80.5, CROSS_ROAD[0] - 23  # the bus shelter's back corner; it is open toward the street
WALK = (BUS_Z - 10, CROSS_ROAD[1] + 8)  # z of the stretch of sidewalk she may walk on, from north of the bus stop
def lane(x): return (x - CW - 5, x - CW - 2)  # x of the path along the west side of the column of buildings at x


SHUT, OPEN, AJAR = 0, 1, 2  # a door's state at the start, as Door3D.DoorState
NOT_OURS = '爸爸说过不能随便敲别人家的门。'  # what she says at another flat's front door
GAP = 0.5  # the room beside an ajar leaf: her 0.4 m and 5 cm on each side


# leaf: for Xiaoman's doors that open, (name, the end of the hole its hinge is at, 0 or 1, the side of the wall it opens to,
# -1 or 1 on the other axis, and its state at the start)
def door(a, b, front=False, leaf=None): return dict(a=a, b=b, lo=0, hi=DOOR_H, front=front, leaf=leaf)
def win(a, b, lo=0.9, hi=2.3): return dict(a=a, b=b, lo=lo, hi=hi, front=False, leaf=None)
def balcony_win(a, b): return win(a, b, 1.0, 2.6)  # over a parapet
def wall(along, at, span, t, holes=(), h=H): return dict(along=along, at=at, span=span, t=t, holes=holes, h=h)


# along: the axis the wall runs on; at: its place on the other axis
walls = [
    wall('x', 0, (0, E), EXT),
    wall('z', 0, (0, D), EXT, [win(0.9, 2.5)]),
    wall('x', D, (0, E), EXT, [door(0.25, 1.25, True, ('FrontDoor', 0, -1, SHUT))]),
    wall('z', E, (0, D), EXT, [win(0.3, 2.7), door(3.0, 3.85, leaf=('BalconyDoor', 1, 1, OPEN)), win(5.1, 6.9)]),
    wall('x', 0, (E, B), EXT, [balcony_win(E + 0.15, B - 0.15)]),
    wall('z', B, (0, 4.0), EXT, [balcony_win(0.15, 3.85)]),
    wall('x', 4.0, (E, B), EXT, [balcony_win(E + 0.15, B - 0.15)]),
    wall('z', 1.8, (0, 4.0), INT),
    wall('x', 4.0, (0, 1.8), INT, [door(0.6, 1.4, leaf=('KitchenDoor', 0, -1, OPEN))]),
    wall('x', 4.0, (1.8, E), INT, [door(2.15, 3.05, leaf=('LivingRoomDoor', 0, -1, AJAR))]),
    wall('z', 3.25, (4.0, D), INT, [door(4.15, 5.05, leaf=('RoomDoor', 0, 1, OPEN))]),
    wall('z', 1.8, (5.5, D), INT),
    wall('x', 5.5, (1.8, 3.25), INT, [door(2.2, 2.9, leaf=('BathroomDoor', 0, 1, OPEN))]),
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
    ('squat_toilet', (1.95, 2.45), (6.6, 7.1), 0.05), ('bag_stool', (6.58, 6.93), (6.2, 6.6), 0.45),  # not in the prototype; scene 1's bag is on it
]
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


# A railing: a handrail over a see-through panel, which stops the player too.
def rail(boxes, along, at, a, b, h=1.1, panel='panel', top='rail', y=0):
    for y0, y1, m in ((y, y + h - 0.06, panel), (y + h - 0.06, y + h, top)):
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


def element(eid, boxes):
    return eid, (0, 0, 0), boxes, {'EntityId': f'"{eid}"', 'BuildingId': '"home"', 'WMin': '-0.5', 'WMax': '0.5'}


# Slopes over each flight, through the front edges of its treads, and one over the step out of the ground floor.
def stair_slopes():
    slopes = []
    for f in range(FLOORS - 1):
        y = f * S
        slopes.append(slope(f'Floor{f}UpSlope', 'x', SX - GO, y, SX + RUN, y + S / 2, zB + EXT / 2, SC - 0.06))
        slopes.append(slope(f'Floor{f}TurnSlope', 'x', SX + RUN + GO, y + S / 2, SX, y + S, SC + 0.06, zS - EXT / 2))
    slopes.append(slope('ExitSlope', 'x', -CW - 0.75, GROUND, -CW, 0, zB, zS))
    subs, nodes = bodies(slopes)
    return packed('Slopes', [], subs=subs, nodes=nodes)


# Xiaoman's doors that open, each turning into the room it leads to, or into the flat. A leaf is 1 cm short of the hole
# at each side and of the floor and the lintel.
def home_leaves():
    leaves = []
    for w in walls:
        for hole in w['holes']:
            if hole['leaf'] is None: continue
            name, end, side, state = hole['leaf']
            run = 1 if end == 0 else -1  # the way the shut leaf runs from its hinge
            a, y = (hole['a'], hole['b'])[end] + 0.01 * run, HOME * S + 0.01
            size = (round(hole['b'] - hole['a'] - 0.02, 6), DOOR_H - 0.02, 0.04)
            # Ajar, the leaf's face is GAP from the far side of the hole: 1 cm of the hole's 2 cm spare is behind the hinge.
            ajar = math.degrees(math.acos(1 - (GAP + 0.01) / size[0]))
            # Door3D turns the leaf toward its local -z, which the yaw points to -z (along x) or to +x (along z) when run is 1.
            along_x = w['along'] == 'x'
            sign = side * run * (-1 if along_x else 1)
            hinge, yaw = ((a, y, w['at']), 0 if run > 0 else math.pi) if along_x else ((w['at'], y, a), -math.pi / 2 * run)
            leaves.append((name, hinge, yaw, size, 90 * sign, ajar * sign, state))
    return leaves


# The shut front doors of a building's flats, all but Xiaoman's in ours, where the walls of north.tscn and south.tscn have them.
def shut_doors(home):
    front = next(hole for w in walls for hole in w['holes'] if hole['front'])
    x, width, shut = (front['a'] + front['b']) / 2, front['b'] - front['a'], []
    for f in range(FLOORS):
        for half, zs in (('North', (D, zB - D)), ('South', (LEN - D, LEN - zB + D))):
            for i, z in enumerate(zs, 1):
                if not (home and f == HOME and half == 'North' and i == 1):
                    shut.append((f'Floor{f}{half}{i}', (x, f * S + DOOR_H / 2, z), (width, DOOR_H, 0.1)))
    return shut


def furniture_scene():
    entities = []
    for eid, (x0, x1), (z0, z1), h in furniture:
        y = HOME * S
        entities.append((eid, ((x0 + x1) / 2, y, (z0 + z1) / 2), [(x0, x1, y, y + h, z0, z1, 'furn')], {'EntityId': f'"{eid}"'}))
    return scene(entities, 'FixedObject3D', 'Furniture')


CURB = 0.1  # the sidewalk's height over the ground


# Section I's ground, roads, paths, fence and gate, poplars, and the sidewalk outside the gate with the bus stop and kiosk.
def site():
    g, ground, roads, paths, fence, sidewalk, edge, bus, kiosk = GROUND, [], [], [], [], [], [], [], []
    box(ground, -40, 120, g - 0.2, g, -40, 240, 'ground')

    def pave(boxes, x0, x1, z0, z1): box(boxes, x0, x1, g - 0.02, g + 0.004, z0, z1, 'road')  # as the rooms' floors
    pave(roads, *WEST_ROAD, FENCE_Z0 + 2, FENCE_Z1 - 2)
    pave(roads, WEST_ROAD[1], MAIN_ROAD, *CROSS_ROAD)
    # Every stair hall's exit reaches a road: in the first column straight west, in the others onto the lane to their
    # west, which runs to the road to the gate.
    for x in COLS:
        for z in ROWS: pave(paths, lane(x)[1] if x else WEST_ROAD[1], x - CW, z + SC - 1.5, z + SC + 1.5)
    for x in COLS[1:]:
        pave(paths, *lane(x), ROWS[0], CROSS_ROAD[0])
        pave(paths, *lane(x), CROSS_ROAD[1], ROWS[-1] + LEN)
    for along, at, a, b in (('x', FENCE_Z0, FENCE_X0, FENCE_X1), ('x', FENCE_Z1, FENCE_X0, FENCE_X1),
                            ('z', FENCE_X0, FENCE_Z0, FENCE_Z1), ('z', FENCE_X1, FENCE_Z0, CROSS_ROAD[0] - 0.6),
                            ('z', FENCE_X1, CROSS_ROAD[1] + 0.6, FENCE_Z1)):
        rail(fence, along, at, a, b, 1.8, 'fence_panel', 'fence', g)
    for z in (CROSS_ROAD[0] - 0.6, CROSS_ROAD[1]): box(fence, FENCE_X1 - 0.3, FENCE_X1 + 0.3, g, g + 2.6, z, z + 0.6, 'wall')
    # Only the stretch she may walk on; the rest of the street belongs to the city.
    for z0, z1 in ((WALK[0], CROSS_ROAD[0]), (CROSS_ROAD[1], WALK[1])):
        box(sidewalk, *SIDEWALK, g, g + CURB, z0, z1, 'walk')
    # Unseen walls that keep her on it: along its edge by the bicycle lane, and across both of its ends.
    box(edge, SIDEWALK[1], SIDEWALK[1] + 0.1, g, g + 3, *WALK, 'unseen')
    for z in (WALK[0] - 0.1, WALK[1]): box(edge, SIDEWALK[0], SIDEWALK[1] + 0.1, g, g + 3, z, z + 0.1, 'unseen')
    x, z = BUS_X, BUS_Z
    box(bus, x, x + 0.1, g, g + 2.5, z, z + 8, 'wall')  # back wall
    box(bus, x, x + 2.6, g + 2.5, g + 2.65, z, z + 8, 'slab')  # roof
    for pz in (z, z + 7.9): box(bus, x + 2.5, x + 2.6, g, g + 2.5, pz, pz + 0.1, 'rail')
    box(bus, x + 0.1, x + 0.5, g, g + 0.45, z + 1, z + 7, 'furn')  # bench
    box(bus, x + 3.2, x + 3.28, g, g + 3.0, z - 1, z - 0.92, 'rail')  # sign pole
    box(bus, x + 3.0, x + 3.5, g + 2.3, g + 3.0, z - 1.02, z - 0.9, 'sign')
    box(kiosk, x - 0.1, x + 1.9, g, g + 2.5, z - 7, z - 5, 'front')
    box(kiosk, x - 0.3, x + 2.1, g + 2.5, g + 2.7, z - 7.2, z - 4.8, 'slab')

    def row(along, at, a, b, gaps=()):  # a tree every 5 m, except where a path meets the road
        return [(at, p) if along == 'z' else (p, at) for p in range(a, b + 1, 5) if not any(s < p < e for s, e in gaps)]
    path_gaps = [(z + SC - 3, z + SC + 3) for z in ROWS]
    lane_gaps = [(lane(x)[0] - 1.8, lane(x)[1] + 1.8) for x in COLS[1:]]
    trees = (row('z', WEST_ROAD[0] - 2.5, FENCE_Z0 + 4, FENCE_Z1 - 4)
             + row('z', WEST_ROAD[1] + 2.5, FENCE_Z0 + 4, FENCE_Z1 - 4, path_gaps + [(CROSS_ROAD[0] - 4, CROSS_ROAD[1] + 4)])
             + row('x', CROSS_ROAD[0] - 1.5, WEST_ROAD[1] + 5, FENCE_X1 - 4, lane_gaps)
             + row('x', CROSS_ROAD[1] + 1.5, WEST_ROAD[1] + 5, FENCE_X1 - 4, lane_gaps))
    parts = dict(ground=ground, roads=roads, paths=paths, fence=fence, sidewalk=sidewalk, sidewalk_edge=edge, bus_stop=bus, kiosk=kiosk)
    return scene([(eid, (0, 0, 0), boxes, {'EntityId': f'"{eid}"'}) for eid, boxes in parts.items()], 'FixedObject3D', 'Site'), trees


# Boxes for the prototype's poplar: a trunk 5 m tall, and a cone 13 m tall and 1.7 m in radius.
def poplar():
    boxes = []
    box(boxes, -0.15, 0.15, GROUND, GROUND + 5, -0.15, 0.15, 'trunk')
    box(boxes, -1.2, 1.2, GROUND + 4, GROUND + 17, -1.2, 1.2, 'crown')
    return scene([('Poplar', (0, 0, 0), boxes, {'EntityId': '"poplar"'})], 'FixedObject3D')


# A building of the compound: the shared scenes of its floors, its stair and its roof, with ids of its own. Ours has
# Xiaoman's flat on the second floor, with its front door open.
def building(col, row):
    home = (col, row) == (0, 0)
    bid, tag = ('home', '') if home else (f'building_{col}_{row}', f'_{col}_{row}')

    def ids(node, eid):
        return node, ([('EntityId', f'"{eid}"')] if eid != node else []) + ([] if home else [('BuildingId', f'"{bid}"')])
    entries = [('Flat', 'flat.tscn', (0, 0, 0), []), ('Floor1North', 'floor1_north.tscn', (0, 0, 0), [])] if home else []
    for f in range(FLOORS):
        for half in ('North', 'South'):
            if not (home and f == HOME and half == 'North'):
                entries.append((f'Floor{f}{half}', f'{half.lower()}.tscn', (0, f * S, 0), [ids(half, f'Floor{f}{half}{tag}')]))
    entries.append(('Stair', 'stair.tscn', (0, 0, 0), [ids(f'Floor{f}Stair', f'Floor{f}Stair{tag}') for f in range(FLOORS)]))
    entries.append(('Roof', 'roof.tscn', (0, 0, 0), [ids('Roof', f'Roof{tag}')]))
    return packed('Building', entries, 'Building3D', [('BuildingId', f'"{bid}"'), ('WMin', '-0.5'), ('WMax', '0.5')])


def main():
    files = {
        'flat.tscn': scene([element('Flat', lift(flat(True), HOME * S))]),
        'floor1_north.tscn': scene([element('Floor1North', lift(half_rest(), HOME * S))]),
        'north.tscn': scene([element('North', flat(False) + half_rest())]),
        'south.tscn': scene([element('South', mirror(flat(False) + half_rest(), LEN))]),
        'stair.tscn': scene([element(f'Floor{f}Stair', lift(stair(f), f * S)) for f in range(FLOORS)]),
        'roof.tscn': scene([element('Roof', roof())]),
        'furniture.tscn': furniture_scene(),
        'poplar.tscn': poplar(),
        'slopes.tscn': stair_slopes(),
        'doors.tscn': doors([], shut_doors(False), NOT_OURS),
        'home_doors.tscn': doors(home_leaves(), shut_doors(True), NOT_OURS),
    }
    files['site.tscn'], trees = site()
    files['poplars.tscn'] = packed('Poplars', [(f'Poplar{i}', 'poplar.tscn', (x, 0, z), [('Poplar', [('EntityId', f'"poplar_{i}"')])])
                                               for i, (x, z) in enumerate(trees, 1)])
    grid = [(col, row, COLS[col], ROWS[row]) for col in range(len(COLS)) for row in range(len(ROWS))]
    for col, row, _, _ in grid: files[f'buildings/building_{col}_{row}.tscn'] = building(col, row)
    names = {(col, row): 'Home' if (col, row) == (0, 0) else f'Building_{col}_{row}' for col, row, _, _ in grid}
    files['buildings.tscn'] = packed('Buildings', [(names[c, r], f'buildings/building_{c}_{r}.tscn', (x, 0, z), [])
                                                   for c, r, x, z in grid])
    subs, nodes = bodies([slope('SidewalkNorthSlope', 'z', CROSS_ROAD[0], GROUND + CURB, CROSS_ROAD[0] + 0.6, GROUND, *SIDEWALK),
                          slope('SidewalkSouthSlope', 'z', CROSS_ROAD[1] - 0.6, GROUND, CROSS_ROAD[1], GROUND + CURB, *SIDEWALK)])
    entries = [(names[c, r], 'slopes.tscn', (x, 0, z), []) for c, r, x, z in grid]
    entries += [(f'{names[c, r]}Doors', 'home_doors.tscn' if (c, r) == (0, 0) else 'doors.tscn', (x, 0, z), []) for c, r, x, z in grid]
    files['props.tscn'] = packed('Props', entries, subs=subs, nodes=nodes)
    here = Path(__file__).parent
    (here / 'buildings').mkdir(exist_ok=True)
    for path, text in files.items(): (here / path).write_text(text, encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
