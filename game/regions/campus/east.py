"""The scenes of the school's east side, which generate.py writes into east/ next to this file.

East of the school wall a narrow alley runs south from the staff compound's gate on the asphalt road to a wall at its
south end. Across it is the old staff compound: a row of shops under flats on its north side, facing the asphalt road, and
another on its east side, facing the main road, neither of which she can enter; a wall with two openings onto the alley
on its west side and a wall on its south; three blocks of flats in its yard. Further east the main road, four lanes, runs
north and south past the fog with a sidewalk and a row of poplars on each side, and the bus stop on its west sidewalk south
of the corner. What she walks on and what stops her are fixed objects of section I; what she cannot reach and there are too
many of for a scene file, plain meshes in its Props. The layout is the greybox prototype's ALLEY, ALLEY_END, SHOPS_N,
SHOPS_E, FLATS, YARD_DOORS, MAIN, STOP and TUNNEL constants and the code that builds them, in its coordinates. The shop
fronts replay its random numbers, so each shutter and glass front stands where its does.
Decision note: 2026-11-01-school-scene-layout
"""
import north_west
from north_west import OUT, ROAD, WALK_END
from shapes import (LINE, TOP, WALL_H, WT, fixed, gap, inside, material, multimesh, numbers, pave, poplars_scene, resource, runs,
                    show, show_many, wall, windows)
from tscn import COLORS, box, packed, vec  # on the path once shapes is imported

COLORS.update({'flats': (0.949, 0.769, 0.541, 1), 'yellow': (0.847, 0.698, 0.227, 1)})

ALLEY, ALLEY_END = (149.12, 151.9), 90  # x, from the school wall's outer face; the wall across its south end (z)
NORTH_X, NORTH_Z, EAST_X, EAST_Z = (151.9, 239.2), (0, 12), (227.2, 239.2), (12, 92.12)  # the two rows of shops
STOREYS, FH, SHOP_H = 5, 2.9, 3.6  # the shops' blocks: storeys, the flats' storey height over the shops, the shops' height
FLATS_X, FLATS, FLATS_STOREYS = (160, 220), ((22, 33), (45, 56), (68, 79)), 5  # the yard's three blocks of flats (z)
YARD_DOORS = (16, 19), (58, 61)  # the openings from the alley into the yard (z)
MAIN = (north_west.MAIN, north_west.MAIN + 16)  # the main road's carriageway (x), four lanes; sidewalks 5 m wide
STOP, FAR, TUNNEL = (241.6, 15), 700, (103.12, 111.12)  # the bus stop; the main road's ends (z); the mall's passage (z)
SHOPS_SEED = 1943309742  # the prototype's random state where it lays out the shop fronts, after the plaza's texture


def shops(boxes, xs, zs, face, rand):  # the prototype's shopBlock: a front and a sign every 4 m along the north or east face
    (x0, x1), (z0, z1) = xs, zs
    box(boxes, x0, x1, 0, SHOP_H + (STOREYS - 1) * FH + 0.6, z0, z1, 'brick')
    a0, a1 = xs if face == 'n' else zs
    a = a0 + 0.5
    while a + 4 <= a1 - 0.5:
        for m, (p, q), (lo, hi), o in (('shutter' if next(rand) < 0.5 else 'glass', (a + 0.3, a + 3.7), (0, 2.8), 0.04),
                                       ('shop_sign', (a + 0.15, a + 3.85), (3.0, 3.5), 0.25)):
            if face == 'n': box(boxes, p, q, lo, hi, z0 - o, z0, m)
            else: box(boxes, x1, x1 + o, lo, hi, p, q, m)
        a += 4


def compound():
    ground, alley, gate, end, yard, walls, rows, flats = [], [], [], [], [], [], [], []
    # The ground reaches the asphalt road's south sidewalk, under the strip in front of the shops.
    pave(ground, ALLEY[0], WALK_END, OUT, EAST_Z[1], 0, 'ground')
    pave(alley, *ALLEY, OUT, ALLEY_END, TOP, 'road')
    pave(yard, NORTH_X[0], EAST_X[0], NORTH_Z[1], EAST_Z[1], TOP, 'road')
    for x in (ALLEY[0] + 0.1, ALLEY[1] - 0.1): box(gate, x - 0.1, x + 0.1, 0, 3.6, -0.1, 0.1, 'iron')  # the gate: posts, a sign
    box(gate, *ALLEY, 3.6, 4.2, -0.15, 0.15, 'shop_sign')
    wall(end, 'x', ALLEY_END, *ALLEY, 3.0, m='brick')
    # The west wall starts at the north row's south face: a wall reaches half its thickness past its ends, and here that
    # half would stand out of the row's west face into the alley.
    wall(walls, 'z', NORTH_X[0], NORTH_Z[1] + WT / 2, EAST_Z[1], WALL_H, [gap(a, b, WALL_H) for a, b in YARD_DOORS], m='brick')
    wall(walls, 'x', EAST_Z[1], NORTH_X[0], EAST_X[0], WALL_H, m='brick')
    rand = numbers(SHOPS_SEED)
    shops(rows, NORTH_X, NORTH_Z, 'n', rand)
    shops(rows, EAST_X, EAST_Z, 'e', rand)
    for z0, z1 in FLATS: box(flats, *FLATS_X, 0, FLATS_STOREYS * FH + 0.6, z0, z1, 'flats')
    return fixed('Compound', compound_ground=ground, alley=alley, compound_gate=gate, alley_wall=end, compound_yard=yard,
                 compound_wall=walls, shops=rows, flats=flats)


def main_road():
    road, walks, stop = [], [], []
    # Cut every 200 m, like the roads north and west of the school (PR #79); the west sidewalk at the asphalt road too.
    ends = range(-FAR, FAR + 1, 200)
    for z0, z1 in zip(ends, ends[1:]):
        pave(road, *MAIN, z0, z1, TOP, 'asphalt')
        pave(walks, MAIN[1], MAIN[1] + 5, z0, z1, TOP, 'walk')
        for a, b in runs(z0, z1, [ROAD] if z0 < ROAD[0] < z1 else []): pave(walks, WALK_END, MAIN[0], a, b, TOP, 'walk')
    # The bus stop: four posts under a roof, a glass back toward the shops, a bench; the stop's sign at the kerb north of it.
    x, z = STOP
    for px in (x - 1, x + 1):
        for pz in (z - 4.8, z + 4.8): box(stop, px - 0.05, px + 0.05, 0, 2.6, pz - 0.05, pz + 0.05, 'pole')
    box(stop, x - 1.2, x + 1.3, 2.6, 2.72, z - 5, z + 5, 'slab')
    box(stop, x - 1.1, x - 1.02, 0.3, 2.3, z - 4.7, z + 4.7, 'glass')
    box(stop, x - 0.9, x - 0.5, 0, 0.45, z - 3, z + 3, 'wall')
    box(stop, MAIN[0] - 0.65, MAIN[0] - 0.55, 0, 3, z - 6.05, z - 5.95, 'pole')
    box(stop, MAIN[0] - 0.67, MAIN[0] - 0.53, 2.2, 3.0, z - 6.4, z - 5.6, 'shop_sign')
    return fixed('MainRoad', main_road=road, main_road_sidewalks=walks, bus_stop=stop)


# A poplar every 8 m along each kerb of the main road, the west row clear of the corner, the bus stop and the mall's passage.
def poplars():
    out = []
    for z in range(-FAR + 4, FAR, 8):
        if not inside(z, ((ROAD[0] - 5, ROAD[1] + 5), (STOP[1] - 8, STOP[1] + 8), (TUNNEL[0] - 6, TUNNEL[1] + 6))):
            out.append((MAIN[0] - 0.8, z))
        out.append((MAIN[1] + 0.8, z))
    return out


# Under section I's Props: the main road's markings, which lie on the asphalt without stopping her, as the plaza's rings do,
# and are too many for a fixed object; the windows of the flats over the shops and in the yard; the poplars.
def props():
    subs, nodes = [], []
    mid, y, h = sum(MAIN) / 2, (TOP + LINE) / 2, LINE - TOP
    yellow = resource(subs, 'BoxMesh', 'yellow', material=material(subs, COLORS['yellow'][:3]), size=vec(0.12, h, 2 * FAR))
    for i, x in enumerate((mid - 0.2, mid + 0.2), 1): show(nodes, f'YellowLine{i}', yellow, (x, y, 0))
    dash = resource(subs, 'BoxMesh', 'dash', material=material(subs, COLORS['line'][:3]), size=vec(0.14, h, 3))
    dashes = multimesh(subs, 'dashes', dash, [(0, y, z + 1.5) for z in range(-FAR, FAR, 9)])  # 3 m long, every 9 m
    for i, x in enumerate((mid - 3.75, mid + 3.75), 1): show_many(nodes, f'LaneLine{i}', dashes, (x, 0, 0))
    entries = [('NorthShopsWindows', 'north_windows.tscn', (NORTH_X[0], SHOP_H, NORTH_Z[0]), []),
               ('EastShopsWindows', 'east_windows.tscn', (EAST_X[0], SHOP_H, EAST_Z[0]), [])]
    entries += [(f'Flats{k}Windows', 'flats_windows.tscn', (FLATS_X[0], 0, z0), []) for k, (z0, _) in enumerate(FLATS, 1)]
    entries.append(('Poplars', 'poplars.tscn', (0, 0, 0), []))
    return packed('East', entries, subs=subs, nodes=nodes, under='campus/east')


def files():
    scenes = {'compound': compound(), 'main_road': main_road(), 'poplars': poplars_scene(poplars(), 'main_road_poplars'),
              'props': props()}
    scenes['fixed'] = packed('East', [('Compound', 'compound.tscn', (0, 0, 0), []), ('MainRoad', 'main_road.tscn', (0, 0, 0), [])],
                             under='campus/east')
    # The flats over the shops: the east row's have none on its north face, which the north row covers.
    scenes['north_windows'] = windows(NORTH_X[1] - NORTH_X[0], NORTH_Z[1] - NORTH_Z[0], STOREYS - 1, FH)
    scenes['east_windows'] = windows(EAST_X[1] - EAST_X[0], EAST_Z[1] - EAST_Z[0], STOREYS - 1, FH, 'esw')
    scenes['flats_windows'] = windows(FLATS_X[1] - FLATS_X[0], FLATS[0][1] - FLATS[0][0], FLATS_STOREYS, FH, 'ns')
    return {f'east/{name}.tscn': text for name, text in scenes.items()}
