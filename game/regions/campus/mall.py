"""The mall tunnel's scenes, which generate.py writes into mall/ next to this file.

South of the school, the sports ground and the staff compound, against the school's south wall, the mall runs two storeys
from a railed forecourt at its west end to the main road's sidewalk at its east end. Downstairs a passage 8 m wide under a
shallow arch runs its whole length between two rows of shops she cannot enter, a front and a sign every 4 m; on its north
side the side door opens onto the iron gate at the dirt road's south end, and three straight stairs go up in place of a
shop. Upstairs is a market: rows of 3 x 3 m booths along aisles, windows on every outer wall, the mall's name over each
end. What she walks on and what stops her are fixed objects of section I; what she cannot reach and there are too many of
for a scene file, plain meshes in its Props. The layout is the greybox prototype's MALL, TUNNEL, MF, MH, STAIR_X and
STAIR_TOP constants and the code that builds the mall, in its coordinates. The shop fronts replay its random numbers, so
each shutter and glass front stands where its does.
Decision note: 2026-11-01-school-scene-layout
"""
import math

from east import TUNNEL
from north_west import DIRT
from shapes import TOP, WT, beam, fixed, material, multimesh, numbers, pave, posts, resource, runs, show, show_many, spaced
from tscn import COLORS, bodies, box, packed, slope, vec  # on the path once shapes is imported

COLORS.update({'vault': (0.839, 0.82, 0.776, 1)})

X0, X1, Z0, Z1 = -121.6, 239.2, 92.12, 122.12  # 40 m longer to the west than the sketch, 30 m deep instead of 52
T0, T1 = TUNNEL  # the passage (z), open at both ends; east.py keeps the poplars clear of its mouth
SPRING, CROWN, MF, MH = 3.7, 5.0, 5.3, 4.2  # the arch's foot and crown; the upper floor's level and storey height
ROOF = MF + MH
STAIRS, STAIR_TOP, TREADS = (-80, 60, 200), 94.75, 31  # each stair's middle (x), 3 m wide, up north from the passage
WELLS, RUN = [(s - 1.5, s + 1.5) for s in STAIRS], T0 - STAIR_TOP  # the stairs (x), and how far each runs (z)
SIDE_DOOR = (DIRT[0] + 0.35, DIRT[1] - 0.35)  # between the iron gate's pillars
HALVES = (X0, STAIRS[1]), (STAIRS[1], X1)  # the floors longer than 200 m, cut in two at the middle stair
RAIL = 1.2  # the forecourt's railing
# Upstairs, rows of booths from the north: (z0, z1, the sides their counters face). A row against an outer wall faces in
# and runs from end wall to end wall; a row of two back to back, the prototype's two rows sharing a back, stops 4 m inside
# the mall's ends and leaves a cross aisle every 30 m. A row that starts north of the passage leaves 9 m clear at each
# stair.
ROWS = ((Z0 + WT, Z0 + WT + 3, 's'), (99, 105, 'ns'), (109.5, 115.5, 'ns'), (Z1 - WT - 3, Z1 - WT, 'n'))
MALL_SEED = 2041053723  # the prototype's random state where it lays out the mall's shop fronts, after the east side's


def shops():  # the two rows of shops on the passage, and where their signs stand
    rand, scenes, signs = numbers(MALL_SEED), [], []
    for name, (z0, z1), face, d, gaps in (('North', (Z0, T0), T0, 1, [SIDE_DOOR] + WELLS),
                                          ('South', (T1, Z1), T1, -1, [])):
        boxes, spots = [], []
        for a, b in runs(X0, X1, gaps):
            box(boxes, a, b, 0, CROWN, z0, z1, 'brick')
            u = a + 0.2
            while u + 4 <= b - 0.2:
                box(boxes, u + 0.3, u + 3.7, 0, 3.0, *sorted((face, face + d * 0.04)), 'shutter' if next(rand) < 0.5 else 'glass')
                spots.append((u + 2, 0, 0))
                u += 4
        scenes.append(fixed(f'{name}Shops', **{f'mall_{name.lower()}_shops': boxes}))
        signs.append((face + d * 0.15, spots))
    return scenes, signs


def ground():  # the passage's and the side door's floors, and the forecourt
    ground, forecourt = [], []
    for a, b in HALVES: pave(ground, a, b, T0, T1, TOP, 'road')
    pave(ground, *SIDE_DOOR, Z0, T0, TOP, 'road')
    fx, fz = (X0 - 7, X0), (T0 - 4, T1 + 4)
    pave(forecourt, *fx, *fz, TOP, 'walk')
    for along, at, a, b in (('z', fx[0], *fz), ('x', fz[0], *fx), ('x', fz[1], *fx)):
        for p in posts(a, b, 2.5): beam(forecourt, along, at, p - 0.04, p + 0.04, 0, RAIL, 0.04, 'iron')
        for y in (0.1, RAIL - 0.05): beam(forecourt, along, at, a, b, y - 0.03, y + 0.03, 0.025, 'iron')
        # From the ground: the player's capsule gets wedged under a rail with a gap beneath it, as at the sports ground.
        beam(forecourt, along, at, a, b, 0, RAIL, 0.05, 'unseen')
    return fixed('Ground', mall_ground=ground, mall_forecourt=forecourt)


# The upper floor's slab, open over each stair, the long part cut in two; the outer walls inside the mall's outline, as the
# prototype places them, the roof and its parapet; the mall's name over each end; the booths.
def upstairs():
    upstairs, booths, rows = [], [], []
    for a, b in runs(X0, X1, WELLS): box(upstairs, a, b, CROWN, MF, Z0, T0, 'slab')
    for a, b in HALVES: box(upstairs, a, b, CROWN, MF, T0, Z1, 'slab')
    for p, q, r, s in ((X0, X1, Z0, Z0 + WT), (X0, X1, Z1 - WT, Z1), (X0, X0 + WT, Z0, Z1), (X1 - WT, X1, Z0, Z1)):
        box(upstairs, p, q, MF, ROOF, r, s, 'brick')
        box(upstairs, p, q, ROOF + 0.3, ROOF + 1.0, r, s, 'brick')
    box(upstairs, X0, X1, ROOF, ROOF + 0.3, Z0, Z1, 'slab')
    for x, o in ((X1, 0.3), (X0, -0.3)): box(upstairs, min(x, x + o), max(x, x + o), MF + 0.6, MF + 2.8, T0 - 3, T1 + 3, 'shop_sign')
    # Each run of booths stops her along its whole front, as the prototype's does: the booths' dividers and counters are
    # plain meshes in the Props, and an unseen box fills the run. Its back is the one part she can see here.
    cross, x = [], X0 + 30
    while x < X1 - 10:
        cross.append((x - 1.5, x + 1.5))
        x += 30
    for z0, z1, faces in ROWS:
        wall_row = len(faces) == 1
        gaps = ([] if wall_row else cross) + ([(s - 4.5, s + 4.5) for s in STAIRS] if z0 < T0 else [])
        back = {'s': (z0, z0 + 0.08), 'n': (z1 - 0.08, z1), 'ns': ((z0 + z1) / 2 - 0.08, (z0 + z1) / 2 + 0.08)}[faces]
        dividers, counters = [], []
        for p, q in runs(*((X0 + WT, X1 - WT) if wall_row else (X0 + 4, X1 - 4)), gaps):
            box(booths, p, q, MF, MF + 2.4, *back, 'furn')
            box(booths, p, q, MF, MF + 2.4, z0, z1, 'unseen')
            u = p
            while u < q - 0.5:
                dividers.append(u)
                u += 3
            dividers.append(q - 0.04)
            u = p
            while u + 3 <= q + 0.01:
                counters.append(u + 1.2)  # each counter leaves a way in beside it
                u += 3
        rows.append(booth_row(dividers, counters, z0, z1, faces))
    return fixed('Upstairs', mall_upstairs=upstairs, mall_booths=booths), rows


# One stair from its middle (x): treads that stand on the ground, which she walks over on a slope in the Props; the
# landing, and under it a block that closes the hole the prototype leaves in the mall's north face; the railings round
# the stairwell upstairs.
def stair():
    boxes = []
    for i in range(TREADS): box(boxes, -1.5, 1.5, 0, (i + 1) * MF / TREADS, T0 - (i + 1) * RUN / TREADS, T0 - i * RUN / TREADS, 'slab')
    box(boxes, -1.5, 1.5, 0, CROWN, Z0, STAIR_TOP, 'brick')
    box(boxes, -1.5, 1.5, CROWN, MF, Z0, STAIR_TOP, 'slab')
    for r0, r1, s0, s1 in ((-1.55, -1.45, STAIR_TOP, T0), (1.45, 1.55, STAIR_TOP, T0), (-1.5, 1.5, T0 - 0.05, T0 + 0.05)):
        box(boxes, r0, r1, MF, MF + 1.1, s0, s1, 'iron')
    return fixed('Stair', mall_stair=boxes)


def booth_row(dividers, counters, z0, z1, faces):  # one row's dividers and counters, at each x given
    subs, nodes = [], []
    divider = resource(subs, 'BoxMesh', 'divider', material=material(subs, COLORS['furn'][:3]), size=vec(0.08, 2.4, z1 - z0))
    show_many(nodes, 'Dividers', multimesh(subs, 'dividers', divider, [(x, 0, 0) for x in dividers]), (0, MF + 1.2, (z0 + z1) / 2))
    counter = resource(subs, 'BoxMesh', 'counter', material=material(subs, COLORS['shop_sign'][:3]), size=vec(1.8, 1.0, 0.5))
    many = multimesh(subs, 'counters', counter, [(x, 0, 0) for x in counters])
    for face in faces: show_many(nodes, f'Counters{face.upper()}', many, (0, MF + 0.5, z0 + 0.25 if face == 'n' else z1 - 0.25))
    return packed('Booths', [], subs=subs, nodes=nodes)


# Under section I's Props: the collision-only slopes over the stairs, through the front edges of their treads; the arch
# over the passage, in eight flat strips; the windows; the shops' signs; and the rows of booths.
def props(signs):
    subs, nodes = bodies([slope(f'Stair{k}Slope', 'z', T0 + RUN / TREADS, 0, STAIR_TOP + RUN / TREADS, MF, *well)
                          for k, well in enumerate(WELLS, 1)])
    half, rise, n = (T1 - T0) / 2, CROWN - SPRING, 8
    r = (half * half + rise * rise) / (2 * rise)
    step = 2 * math.asin(half / r) / n
    strip = resource(subs, 'BoxMesh', 'vault', material=material(subs, COLORS['vault'][:3]),
                     size=vec(X1 - X0, 0.05, 2 * r * math.sin(step / 2)))
    out = r * math.cos(step / 2) + 0.025  # from the arch's centre to each strip's middle
    for k in range(n):
        a = (k + 0.5 - n / 2) * step
        show(nodes, f'Vault{k + 1}', strip, ((X0 + X1) / 2, CROWN - r + out * math.cos(a), (T0 + T1) / 2 + out * math.sin(a)),
             rotation=vec(a, 0, 0))
    # The windows on each outer wall, 2.4 x 1.8 m, outside and in; those inside fall where those outside do.
    glass, y = material(subs, COLORS['window'][:3]), MF + 1.0 + 0.9
    ns = resource(subs, 'BoxMesh', 'nsb', material=glass, size=vec(2.4, 1.8, 0.06))
    ns = multimesh(subs, 'ns', ns, [(c, 0, 0) for c in spaced(X0, X1, 4)])
    for i, z in enumerate((Z0 - 0.03, Z0 + WT + 0.03, Z1 - WT - 0.03, Z1 + 0.03), 1): show_many(nodes, f'WindowsNS{i}', ns, (0, y, z))
    ew = resource(subs, 'BoxMesh', 'ewb', material=glass, size=vec(0.06, 1.8, 2.4))
    ew = multimesh(subs, 'ew', ew, [(0, 0, c) for c in spaced(Z0, Z1, 4)])
    for i, x in enumerate((X0 - 0.03, X0 + WT + 0.03, X1 - WT - 0.03, X1 + 0.03), 1): show_many(nodes, f'WindowsEW{i}', ew, (x, y, 0))
    sign = resource(subs, 'BoxMesh', 'sign', material=material(subs, COLORS['shop_sign'][:3]), size=vec(3.7, 0.4, 0.3))
    for name, (z, spots) in zip(('North', 'South'), signs): show_many(nodes, f'{name}Signs', multimesh(subs, name, sign, spots), (0, 3.3, z))
    entries = [(f'Booths{k}', f'booths_{k}.tscn', (0, 0, 0), []) for k in range(1, len(ROWS) + 1)]
    return packed('Mall', entries, subs=subs, nodes=nodes, under='campus/mall')


def files():
    (north, south), signs = shops()
    upstairs_scene, rows = upstairs()
    scenes = {'north_shops': north, 'south_shops': south, 'ground': ground(), 'upstairs': upstairs_scene, 'stair': stair(),
              'props': props(signs)}
    scenes.update({f'booths_{k}': row for k, row in enumerate(rows, 1)})
    entries = [(name, f'{path}.tscn', (0, 0, 0), []) for name, path in
               (('NorthShops', 'north_shops'), ('SouthShops', 'south_shops'), ('Ground', 'ground'), ('Upstairs', 'upstairs'))]
    entries += [(f'Stair{k}', 'stair.tscn', (s, 0, 0), [('mall_stair', [('EntityId', f'"mall_stair_{k}"')])])
                for k, s in enumerate(STAIRS, 1)]
    scenes['fixed'] = packed('Mall', entries, under='campus/mall')
    return {f'mall/{name}.tscn': text for name, text in scenes.items()}
