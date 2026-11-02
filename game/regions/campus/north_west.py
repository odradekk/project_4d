"""The scenes of the school's north and west sides, which generate.py writes into north_west/ next to this file.

North of the school wall the asphalt road runs east to the main road, with a sidewalk on each side; across it is the upscale
estate: a lawn strip, a railing on a low wall with a shut gate, a guard booth and a barrier arm, and towers inside. West of
the school the dirt road runs from the iron gate at its south end north past the building site into the distance, between
belts of poplars. On its west are the building site, hoarding round three unfinished concrete frames, and the sports ground,
which field.py builds; stalls line the road north of the iron gate. What she walks on and what stops her are fixed objects
of section I; what she cannot reach and there are too many of for a scene file, plain meshes in its Props. The layout is
the greybox prototype's ROAD, DIRT, RICH, SITE, IRON and stall constants and the code that builds them, in its coordinates.
The frames, the poplars and the stalls replay its random numbers, so they stand where its do.
Decision note: 2026-11-01-school-scene-layout
"""
import field
from field import FIELD_GATE, FIELD_X
from shapes import GROUND, LINE, TOP, WT, beam, fixed, gap, material, multimesh, pave, posts, resource, runs, show_many, wall
from tscn import COLORS, box, fmt, packed, vec  # on the path once shapes is imported

COLORS.update({
    'asphalt': (0.333, 0.345, 0.361, 1), 'dirt': (0.604, 0.541, 0.439, 1), 'green': (0.541, 0.604, 0.455, 1),
    'hoarding': (0.561, 0.627, 0.69, 1), 'booth': (0.949, 0.769, 0.541, 1), 'shop_sign': (0.788, 0.655, 0.486, 1),
    'shutter': (0.549, 0.569, 0.588, 1),
})

OUT = -WT / 2  # the school wall's outer face on the north and west, where the paving outside meets the yard
ROAD, NORTH_WALK, SOUTH_WALK = (-10.5, -3.5), (-13.5, -10.5), (-3.5, OUT)  # z
MAIN, WALK_END = 244.2, 239.2  # x: the main road's west edge, and the inner edge of the sidewalk along it
DIRT, FAR, IRON = (-12.3, -5.3), -700, 92  # the dirt road (x); its far north end and the iron gate at its south end (z)
RICH_X, RICH_Z, RICH_GATE = (-0.6, 237), (-106, -19.8), (100, 110)  # the estate's railing; the gate in its south side (x)
LOW, RAIL_H = 0.5, 2  # the railing's low wall, and its top
TOWER_W, TOWER_D, TOWER_FH = 24, 15, 3
TOWERS = [(x, z, 11 if (i + j) % 2 else 18) for i, x in enumerate((12, 58, 132, 178)) for j, z in enumerate((-92, -58))]
SITE_X, SITE_Z = (-79.4, -17), (-106, -32.4)
FRAMES, SPAN = ((-72, -98, 4, 2, 9), (-42, -95, 3, 2, 6), (-70, -62, 3, 2, 12)), 6  # first column (x, z), bays, storeys
SHEETS = ('furn', 'shop_sign', 'shutter', 'door')  # the stalls' sheets, the prototype's stall, sign, shutter and door
# The prototype's random state where it lays out the frames, the poplars and the stalls; between them it draws numbers for
# other things.
FRAMES_SEED, POPLARS_SEED, STALLS_SEED = 1223483939, 1637571316, 979486785


def numbers(seed):  # the prototype's random numbers from that state on
    while True:
        seed = seed * 16807 % 2147483647
        yield seed / 2147483647


def inside(v, gaps): return any(a < v < b for a, b in gaps)


def bars(boxes, along, at, a, b, h):  # a gate as the prototype draws one: a post every 0.15 m, a rail at its foot and top
    for p in posts(a, b, 0.15): beam(boxes, along, at, p - 0.04, p + 0.04, 0, h, 0.04, 'iron')
    for y in (0.1, h - 0.05): beam(boxes, along, at, a, b, y - 0.03, y + 0.03, 0.025, 'iron')


def roads():
    ground, asphalt, walks, dirt, gate = [], [], [], [], []
    pave(ground, FIELD_X[0], WALK_END, RICH_Z[0], OUT, 0, 'ground')
    pave(ground, FIELD_X[0], OUT, OUT, IRON + WT / 2, 0, 'ground')
    pave(asphalt, DIRT[1], MAIN, *ROAD, TOP, 'asphalt')
    mid, x = sum(ROAD) / 2, DIRT[1] + 4
    while x < MAIN - 4:  # the dashed centre line
        pave(asphalt, x, x + 4, mid - 0.07, mid + 0.07, LINE, 'line')
        x += 8
    pave(walks, RICH_X[0], WALK_END, *NORTH_WALK, TOP, 'walk')
    pave(walks, OUT, WALK_END, *SOUTH_WALK, TOP, 'walk')
    # The dirt road and the belts of poplars, cut at the building site's north end, where she is to stop: what she may walk
    # on is no longer than the compound's roads, whose long thin sides can catch her (PR #79). The worn path from the back
    # gate crosses the belts to the sports ground's gate.
    west, east = (FIELD_X[1], DIRT[0]), (DIRT[1], OUT)
    for z0, z1 in ((FAR, RICH_Z[0]), (RICH_Z[0], IRON)): pave(dirt, *DIRT, z0, z1, TOP, 'dirt')
    for x0, x1, z0, z1 in ((*west, FAR, RICH_Z[0]), (*west, RICH_Z[0], FIELD_GATE[0]), (*west, FIELD_GATE[1], IRON),
                           (DIRT[1], RICH_X[0], FAR, RICH_Z[0]), (DIRT[1], RICH_X[0], RICH_Z[0], ROAD[0]),
                           (*east, ROAD[1], FIELD_GATE[0]), (*east, FIELD_GATE[1], IRON)):
        pave(dirt, x0, x1, z0, z1, TOP, 'green')
    for x0, x1 in (west, east): pave(dirt, x0, x1, *FIELD_GATE, TOP, 'dirt')
    # The double iron gate: a pillar at each edge of the road, and the leaves standing open inside them.
    for x in DIRT: box(gate, x - 0.35, x + 0.35, 0, 3.4, IRON - 0.35, IRON + 0.35, 'brick')
    a, b = DIRT[0] + 0.35, DIRT[1] - 0.35
    for x in (a + 0.05, b - 0.05): bars(gate, 'z', x, IRON - (b - a) / 2, IRON - 0.35, 3)
    return fixed('Roads', outside_ground=ground, asphalt_road=asphalt, sidewalks=walks, dirt_road=dirt, iron_gate=gate)


# The estate, and its railing's posts for the Props. The low wall is what stops her; there is no railing on its north side.
def estate():
    lawn, railing, gate, towers, spots = [], [], [], [], []
    (x0, x1), (z0, z1), (g0, g1) = RICH_X, RICH_Z, RICH_GATE
    for a, b in ((x0, g0), (g1, x1)): pave(lawn, a, b, z0, NORTH_WALK[0], TOP, 'green')
    pave(lawn, g0, g1, z0, z1, TOP, 'green')
    pave(lawn, g0, g1, z1, NORTH_WALK[0], TOP, 'walk')  # the path from the sidewalk to the gate
    for along, at, a, b, gaps in (('x', z1, x0, x1, [RICH_GATE]), ('z', x0, z0, z1, []), ('z', x1, z0, z1, [])):
        wall(railing, along, at, a, b, LOW, [gap(p, q, LOW) for p, q in gaps], m='brick')
        for p, q in runs(a, b, gaps):
            beam(railing, along, at, p, q, RAIL_H - 0.08, RAIL_H - 0.02, 0.025, 'fence')
            y = (LOW + RAIL_H) / 2
            spots += [(s, y, at) if along == 'x' else (at, y, s) for s in posts(p, q, 2.5)]
    for x in RICH_GATE: box(gate, x - 0.6, x + 0.6, 0, 3.5, z1 - 0.6, z1 + 0.6, 'wall')
    # The prototype draws the gate open and stops her in it; the note says it is shut. So it is shut, as high as the
    # railing, and the barrier arm stands 0.5 m in behind it.
    bars(gate, 'x', z1, g0 + 0.6, g1 - 0.6, RAIL_H)
    box(gate, g0 + 0.6, g1 - 0.6, 0.9, 1.0, z1 - 0.55, z1 - 0.45, 'door')
    box(gate, g1 + 1, g1 + 4, 0, 2.8, z1 - 4, z1 - 1, 'booth')
    for x, z, floors in TOWERS: box(towers, x, x + TOWER_W, 0, floors * TOWER_FH + 1, z, z + TOWER_D, 'wall')
    return fixed('Estate', estate_lawn=lawn, estate_railing=railing, estate_gate=gate, estate_towers=towers), spots


# A tower's windows, 1.5 × 1.4 m, as many as fit 3.3 m apart along each face, 0.9 m over each floor: one MultiMesh for the
# north and south faces and one for the east and west, shown again on every floor.
def windows(floors):
    subs, nodes = [], []
    glass = material(subs, COLORS['window'][:3])

    def along(length):
        n = int(length // 3.3)
        return [(length - n * 3.3) / 2 + (i + 0.5) * 3.3 for i in range(n)]
    y = 0.9 + 0.7
    ns = multimesh(subs, 'ns', resource(subs, 'BoxMesh', 'nsb', material=glass, size=vec(1.5, 1.4, 0.06)),
                   [(c, y, z) for z in (-0.03, TOWER_D + 0.03) for c in along(TOWER_W)])
    ew = multimesh(subs, 'ew', resource(subs, 'BoxMesh', 'ewb', material=glass, size=vec(0.06, 1.4, 1.5)),
                   [(x, y, c) for x in (-0.03, TOWER_W + 0.03) for c in along(TOWER_D)])
    for f in range(floors):
        show_many(nodes, f'NorthSouth{f + 1}', ns, (0, f * TOWER_FH, 0))
        show_many(nodes, f'EastWest{f + 1}', ew, (0, f * TOWER_FH, 0))
    return packed('Windows', [], subs=subs, nodes=nodes)


def building_site():
    site, frames = [], []
    pave(site, SITE_X[0], FIELD_X[1], *SITE_Z, TOP, 'dirt')  # inside the hoarding, up to the belt of poplars
    for along, at, (a, b) in (('x', SITE_Z[0], SITE_X), ('x', SITE_Z[1], SITE_X), ('z', SITE_X[0], SITE_Z),
                              ('z', SITE_X[1], SITE_Z)):
        wall(site, along, at, a, b, 2.5, t=0.1, m='hoarding')
    rand = numbers(FRAMES_SEED)
    for x0, z0, nx, nz, storeys in FRAMES:
        # Each column is one box up through every storey but the top one, where it stops short and there is no slab.
        for i in range(nx + 1):
            for j in range(nz + 1):
                x, z, h = x0 + i * SPAN, z0 + j * SPAN, (storeys - 1) * 3 + 0.5 + next(rand) * 2.5
                box(frames, x - 0.25, x + 0.25, 0, h, z - 0.25, z + 0.25, 'concrete')
        x1, z1 = x0 + nx * SPAN, z0 + nz * SPAN
        for f in range(storeys - 1): box(frames, x0 - 0.3, x1 + 0.3, f * 3 + 2.8, f * 3 + 3, z0 - 0.3, z1 + 0.3, 'concrete')
    return fixed('BuildingSite', building_site=site, unfinished_frames=frames)


# The stalls along both edges of the dirt road north of the iron gate, and where the sunshades over some of them stand.
def stalls():
    boxes, shades = [], []
    rand = numbers(STALLS_SEED)
    for x0, x1 in ((DIRT[0] + 0.1, DIRT[0] + 1.6), (DIRT[1] - 1.6, DIRT[1] - 0.1)):
        cx, z = (x0 + x1) / 2, IRON - 40
        while z < IRON - 7:  # short of the gate's open leaves
            # Goods on a sheet, 4 cm high like the toilets' pans, which she walks over; the vendor's stool or crate.
            box(boxes, x0, x1, GROUND, TOP + 0.04, z, z + 2.2, SHEETS[int(next(rand) * len(SHEETS))])
            box(boxes, cx - 0.3, cx + 0.3, 0, 0.45, z + 2.4, z + 2.9, 'trunk')
            if next(rand) < 0.5: shades.append((cx, z + 1.1))
            z += 4.5
    return fixed('Stalls', stalls=boxes), shades


# Two rows of poplars in each belt along the dirt road, the eastern ones clear of the corner by the asphalt road, both clear
# of the paths to the sports ground's gate and through the iron gate; and a row on the lawn strip, clear of the estate's gate.
def poplars():
    rand, out = numbers(POPLARS_SEED), []
    corner = (ROAD[0] - 4, 1)
    for x, gaps in ((-16.1, ()), (-13.7, ()), (-3.9, (corner,)), (-1.4, (corner,))):
        z = FAR + next(rand) * 5
        while z < IRON:
            tz = z + (next(rand) - 0.5) * 1.6
            if not inside(tz, (*gaps, (FIELD_GATE[0] - 2, FIELD_GATE[1] + 2), (IRON - 3, IRON + 3))):
                out.append((x + (next(rand) - 0.5) * 0.8, tz))
            z += 5
    x = RICH_X[0] + 4
    while x < RICH_X[1] - 2:
        if not inside(x, ((RICH_GATE[0] - 3, RICH_GATE[1] + 3),)): out.append((x, (RICH_Z[1] + NORTH_WALK[0]) / 2))
        x += 7
    return out


def poplars_scene():  # Poplars3D.cs builds them when the region loads: a scene file cannot hold so many boxes
    at = ', '.join(f'{fmt(round(x, 3))}, {fmt(round(z, 3))}' for x, z in poplars())
    return '\n'.join(['[gd_scene load_steps=2 format=3]\n', '[ext_resource type="Script" path="res://regions/campus/Poplars3D.cs" id="e1"]\n',
                      f'[node name="Poplars" type="Node3D"]\nscript = ExtResource("e1")\nAt = PackedVector2Array({at})\n'])


# Under section I's Props: the estate's posts, the towers' windows, the stalls' sunshades, the poplars and the sports ground's.
def props(posts_at, shades):
    subs, nodes = [], []
    post = resource(subs, 'BoxMesh', 'post', material=material(subs, COLORS['fence'][:3]), size=vec(0.08, RAIL_H - LOW, 0.08))
    show_many(nodes, 'EstatePosts', multimesh(subs, 'posts', post, posts_at))
    pole = resource(subs, 'CylinderMesh', 'pole', material=material(subs, COLORS['pole'][:3]), top_radius=0.025,
                    bottom_radius=0.025, height=2.1, radial_segments=6, rings=1)
    shade = resource(subs, 'CylinderMesh', 'shade', material=material(subs, COLORS['door'][:3]), top_radius=0,
                     bottom_radius=1.1, height=0.45, radial_segments=8, rings=1)
    show_many(nodes, 'SunshadePoles', multimesh(subs, 'poles', pole, [(x, 1.05, z) for x, z in shades]))
    show_many(nodes, 'Sunshades', multimesh(subs, 'shades', shade, [(x, 2.1, z) for x, z in shades]))
    entries = [(f'Tower{k}Windows', f'windows_{floors}.tscn', (x, 0, z), []) for k, (x, z, floors) in enumerate(TOWERS, 1)]
    entries += [('Poplars', 'poplars.tscn', (0, 0, 0), []), ('Field', 'field_props.tscn', (0, 0, 0), [])]
    return packed('NorthWest', entries, subs=subs, nodes=nodes, under='campus/north_west')


def files():
    estate_scene, posts_at = estate()
    stalls_scene, shades = stalls()
    scenes = {'roads': roads(), 'estate': estate_scene, 'building_site': building_site(), 'field': field.fixed_scene(),
              'stalls': stalls_scene, 'field_props': field.props(), 'poplars': poplars_scene(), 'props': props(posts_at, shades)}
    parts = ('Roads', 'roads'), ('Estate', 'estate'), ('BuildingSite', 'building_site'), ('Field', 'field'), ('Stalls', 'stalls')
    scenes['fixed'] = packed('NorthWest', [(name, f'{path}.tscn', (0, 0, 0), []) for name, path in parts], under='campus/north_west')
    scenes.update({f'windows_{floors}': windows(floors) for floors in sorted({f for _, _, f in TOWERS})})
    return {f'north_west/{name}.tscn': text for name, text in scenes.items()}
