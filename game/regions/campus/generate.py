"""Writes the scenes of the campus region next to this file, all but campus.tscn: python game/regions/campus/generate.py

The layout is the greybox prototype's (branch prototype/school-layout, prototypes/school-layout/index.html), in metres with
x east, z south and y up; walls are placed by their centrelines. The origin is the school wall's north-west corner, and
y = 0 is the top of the yard. The scene text comes from the compound region's tscn.py. Hand edits are lost when this runs.
Decision note: 2026-11-01-school-scene-layout
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / 'compound'))
from tscn import COLORS, bodies, box, fmt, mirror, packed, scene, slope, vec  # noqa: E402

# The campus's colors, beside the compound's; scene() looks them up by name.
COLORS.update({
    'brick': (0.886, 0.863, 0.812, 1), 'yard': (0.769, 0.749, 0.706, 1), 'plaza': (0.837, 0.817, 0.783, 1),
    'plaza_band': (0.741, 0.711, 0.659, 1), 'court': (0.62, 0.639, 0.639, 1), 'line': (0.933, 0.933, 0.918, 1),
    'name_wall': (0.478, 0.231, 0.188, 1), 'iron': (0.29, 0.302, 0.322, 1), 'pole': (0.427, 0.435, 0.447, 1),
    'backdrop': (0.949, 0.769, 0.541, 1), 'concrete': (0.725, 0.714, 0.682, 1), 'cement': (0.741, 0.718, 0.671, 1),
    'desk': (0.69, 0.541, 0.353, 1), 'chair': (0.541, 0.416, 0.267, 1), 'glass': (0.373, 0.408, 0.439, 1),
    'door': (0.816, 0.165, 0.165, 1), 'white': (0.957, 0.957, 0.941, 1), 'pane': (0.612, 0.769, 0.91, 0.35),
})

WT = 0.24  # brick wall thickness
GROUND = -0.2  # the bottom of the yard's slab
# Paving and floors stand this much over the yard, lines a little more; each reaches into the yard, 1 cm thick at least.
TOP, LINE = 0.004, 0.008

SCHOOL_X, SCHOOL_Z, WALL_H = (0, 149), (0, 92), 2.4
GATE, NAME_WALL, BACK = (72, 80), (62, 71.4), (6, 10)  # in the north wall (x); the back gate in the west wall (z)
PLAZA_X, PLAZA_Z = (66, 108), (18, 76)
STAND_X, STAND_Z, STAND_H = (79, 95), (79.5, 84.5), 1.2
STEPS_X, STEPS_Z, STEPS = (83, 91), (77, 79.5), 6  # up southward onto the stand
GO = (STEPS_Z[1] - STEPS_Z[0]) / STEPS  # each step's depth
POLE = (87, 72)
LODGE_X, LODGE_Z, LODGE_H, LODGE_MX = (83, 90), (0.12, 6), 3.4, 87  # MX: the wall between its two rooms
COURTS_X, COURTS_Z, COURTS = (1, 36.5), (19, 63), ((22, 37), (45, 60))  # each court's z, 28 m from x 4.5
PING_X, PING_STEP, PING_ROWS = 123.5, 5, (4.5, 10)  # the rows' middle, spacing and z
TOILET_X, TOILET_Z, TOILET_H = (134, 148.76), (19.5, 35), 3.6  # the east wall stands against the school wall
MEN_Z, MEN_DOOR, WOMEN_DOOR = 28.5, (136.5, 138.3), (144.9, 146.3)  # the boys' ground floor stops at MEN_Z
STAIR_Z, LANDING, FOOT, TREADS = (35.12, 36.8), (144.4, 146.8), 138.5, 21  # the toilet's outside stair, rising east
RUN = LANDING[0] - FOOT
STORE_X, STORE_Z, STORE_H = (3, 19), (67, 75), 3.4
BALL_ROWS, BALL_R = (0.55, 1.15, 1.75), 0.12  # the store's balls: the rows' heights


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
def floor(boxes, x0, x1, z0, z1, m, y=0): box(boxes, x0, x1, y - 0.02, y + TOP, z0, z1, m)


def outline(boxes, x0, x1, z0, z1, w, m):  # a frame w wide just inside the rectangle
    for p, q, r, s in ((x0, x1, z0, z0 + w), (x0, x1, z1 - w, z1), (x0, x0 + w, z0, z1), (x1 - w, x1, z0, z1)):
        pave(boxes, p, q, r, s, LINE, m)


def fixed(root, **parts):  # a scene of fixed objects, one for each id and its boxes
    return scene([(eid, (0, 0, 0), boxes, {'EntityId': f'"{eid}"'}) for eid, boxes in parts.items()], 'FixedObject3D', root)


def site():
    ground, walls, plaza, stand, pole = [], [], [], [], []
    pave(ground, SCHOOL_X[0] - WT / 2, SCHOOL_X[1] + WT / 2, SCHOOL_Z[0] - WT / 2, SCHOOL_Z[1] + WT / 2, 0, 'yard')
    wall(walls, 'x', SCHOOL_Z[0], *SCHOOL_X, WALL_H, [gap(*NAME_WALL, WALL_H), gap(*GATE, WALL_H)], m='brick')
    wall(walls, 'x', SCHOOL_Z[1], *SCHOOL_X, WALL_H, m='brick')
    wall(walls, 'z', SCHOOL_X[0], *SCHOOL_Z, WALL_H, [gap(*BACK, WALL_H)], m='brick')
    wall(walls, 'z', SCHOOL_X[1], *SCHOOL_Z, WALL_H, m='brick')
    box(walls, *NAME_WALL, 0, 3.2, -0.3, 0.3, 'name_wall')
    box(walls, NAME_WALL[1], GATE[0], 0, 3.4, -0.4, 0.4, 'wall')  # the gate's pillars
    box(walls, GATE[1], GATE[1] + 0.8, 0, 3.4, -0.4, 0.4, 'wall')
    box(walls, GATE[1] + 0.1, GATE[1] + 0.7, 0, 1.6, 0.4, 2.2, 'iron')  # the folding gate, pushed open
    for z in BACK: box(walls, -0.4, 0.4, 0, 3.0, z - 0.4, z + 0.4, 'wall')
    # Art stone paving; a darker band 1.2 m in from its edges. The rings in its middle are in props.tscn.
    pave(plaza, *PLAZA_X, *PLAZA_Z, TOP, 'plaza')
    outline(plaza, PLAZA_X[0] + 1.2, PLAZA_X[1] - 1.2, PLAZA_Z[0] + 1.2, PLAZA_Z[1] - 1.2, 1.2, 'plaza_band')
    box(stand, *STAND_X, 0, STAND_H, *STAND_Z, 'slab')
    for i in range(STEPS): box(stand, *STEPS_X, 0, (i + 1) * STAND_H / STEPS, STEPS_Z[0] + i * GO, STEPS_Z[1], 'slab')
    box(stand, *STAND_X, STAND_H, 4.5, STAND_Z[1] - WT, STAND_Z[1], 'backdrop')
    box(pole, POLE[0] - 1.5, POLE[0] + 1.5, 0, 0.45, POLE[1] - 1.5, POLE[1] + 1.5, 'backdrop')
    box(pole, POLE[0] - 0.075, POLE[0] + 0.075, 0.45, 12.45, POLE[1] - 0.075, POLE[1] + 0.075, 'pole')
    return fixed('Site', ground=ground, school_wall=walls, plaza=plaza, stand=stand, flagpole=pole)


# The two basketball courts, with a hoop at each end; the rims and centre circles are in props.tscn. Twelve concrete
# table-tennis tables with a brick net.
def courts():
    courts, tables, circles, rims = [], [], [], []
    pave(courts, *COURTS_X, *COURTS_Z, TOP, 'court')
    for z0, z1 in COURTS:
        x0, x1 = COURTS_X[0] + 3.5, COURTS_X[0] + 31.5
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        outline(courts, x0, x1, z0, z1, 0.1, 'line')
        pave(courts, cx - 0.05, cx + 0.05, z0, z1, LINE, 'line')
        circles.append((cx, cz))
        for xb, way in ((x0, 1), (x1, -1)):
            xk, xp, xbd = xb + way * 5.8, xb - way * 1.0, xb + way * 1.2  # the key's far end, the post, the backboard
            outline(courts, min(xb, xk), max(xb, xk), cz - 2.45, cz + 2.45, 0.1, 'line')
            box(courts, xp - 0.1, xp + 0.1, 0, 3.4, cz - 0.1, cz + 0.1, 'pole')
            box(courts, min(xp, xbd), max(xp, xbd), 3.2, 3.3, cz - 0.06, cz + 0.06, 'pole')
            box(courts, xbd - 0.03, xbd + 0.03, 2.9, 3.95, cz - 0.9, cz + 0.9, 'wall')
            rims.append((xbd + way * 0.375, 3.05, cz))
    for zc in PING_ROWS:
        for i in range(6):
            xc = PING_X + (i - 2.5) * PING_STEP
            box(tables, xc - 0.76, xc + 0.76, 0.66, 0.76, zc - 1.37, zc + 1.37, 'concrete')
            for s in (-1, 1): box(tables, xc - 0.5, xc + 0.5, 0, 0.66, zc + s * 0.6 - 0.15, zc + s * 0.6 + 0.15, 'concrete')
            box(tables, xc - 0.76, xc + 0.76, 0.76, 0.91, zc - 0.06, zc + 0.06, 'concrete')
    return fixed('Courts', courts=courts, ping_pong=tables), circles, rims


# The lodge against the north wall: a duty room by the gate, which takes the mail too, and the gatekeeper's room.
def lodge():
    shell, things = [], []
    (x0, x1), (z0, z1), h, mx = LODGE_X, LODGE_Z, LODGE_H, LODGE_MX
    def win(c, w): return window(c, w, 0.9, 2.2)
    wall(shell, 'z', x0, z0, z1, h, [win(2.2, 2.0), gap(4.4, 5.3, 2.1)])
    wall(shell, 'z', x1, z0, z1, h, [win(2.5, 1.0)])
    wall(shell, 'x', z1, x0, x1, h, [win(84.9, 1.4), win(88.6, 1.2)])
    wall(shell, 'z', mx, z0, z1, h, [gap(4.6, 5.5, 2.1)], 0.12)
    box(shell, x0 - 0.12, x1 + 0.12, WALL_H, h, -WT / 2, WT / 2, 'wall')  # the school wall, built up to the roof
    box(shell, x0, x1, 0, WALL_H, z0, z0 + 0.02, 'wall')  # and plastered inside
    floor(shell, x0, x1, z0, z1, 'cement')
    box(shell, x0 - 0.12, x1 + 0.12, h - 0.2, h, z0 - 0.12, z1 + 0.12, 'slab')
    box(shell, x0 + 0.12, x0 + 0.95, 0, 2.1, 4.4, 4.45, 'door')  # the leaf, open
    # The duty room: desk and phone at the window, chair, coal stove piped out through the roof, pigeonholes for mail.
    box(things, x0 + 0.12, x0 + 0.82, 0, 0.75, 1.3, 3.1, 'desk')
    box(things, x0 + 0.3, x0 + 0.55, 0.75, 0.85, 1.5, 1.8, 'glass')
    box(things, x0 + 1.0, x0 + 1.4, 0, 0.45, 2.0, 2.4, 'chair')
    box(things, 85.4, 85.9, 0, 0.7, z0 + 0.3, z0 + 0.8, 'iron')
    box(things, 85.59, 85.71, 0.7, h + 0.8, z0 + 0.49, z0 + 0.61, 'pole')
    box(things, mx - 0.5, mx - 0.06, 0, 2.0, z0 + 0.2, 4.0, 'desk')
    for k in range(5): box(things, mx - 0.54, mx - 0.5, 0.4 + 0.32 * k, 0.43 + 0.32 * k, z0 + 0.2, 4.0, 'slab')
    # The gatekeeper's room: a bed, a wardrobe, a washstand.
    box(things, x1 - 2.1, x1 - 0.12, 0, 0.45, z0 + 0.12, z0 + 1.12, 'desk')
    box(things, x1 - 2.0, x1 - 0.2, 0.45, 0.55, z0 + 0.17, z0 + 1.07, 'furn')
    box(things, x1 - 0.7, x1 - 0.12, 0, 2.0, 3.2, 4.4, 'desk')
    box(things, x1 - 0.62, x1 - 0.12, 0, 0.8, z1 - 0.62, z1 - 0.12, 'concrete')
    return fixed('Lodge', lodge=shell, lodge_furniture=things)


# Windows high up, 1.6 m wide every 3 m along a toilet wall from a to b, clear of the span skip.
def strip(a, b, skip=(0, 0)):
    out, c = [], a + 1.5
    while c < b - 1:
        if not (c + 0.8 > skip[0] and c - 0.8 < skip[1]): out.append(window(c, 1.6, 2.2, 2.8))
        c += 3
    return out


# The toilet block, doors facing south: the boys' toilet in the north part downstairs, its door at the back of a bay open
# to the west and south; the girls' toilet over the whole block, up its own stair outside the south wall.
def toilet():
    down, up, stair, edge = [], [], [], []
    (x0, x1), (z0, z1), th = TOILET_X, TOILET_Z, TOILET_H
    wall(down, 'z', x0, z0, MEN_Z, th, strip(z0, MEN_Z))
    wall(down, 'z', x1, z0, MEN_Z, th, strip(z0, MEN_Z))
    wall(down, 'x', z0, x0, x1, th, strip(x0, x1))
    wall(down, 'x', MEN_Z, x0, x1, th, [gap(*MEN_DOOR, 2.4)] + strip(x0, x1, MEN_DOOR))
    floor(down, x0, x1, z0, MEN_Z, 'tile')
    for x in (x0 + 0.2, (x0 + x1) / 2, x1 - 0.2): box(down, x - 0.2, x + 0.2, 0, th - 0.2, z1 - 0.4, z1, 'wall')
    box(up, x0 - 0.12, x1 + 0.12, th - 0.2, th, z0 - 0.12, z1 + 0.12, 'slab')
    wall(up, 'z', x0, z0, z1, th, strip(z0, z1), y0=th)
    wall(up, 'z', x1, z0, z1, th, strip(z0, z1), y0=th)
    wall(up, 'x', z0, x0, x1, th, strip(x0, x1), y0=th)
    wall(up, 'x', z1, x0, x1, th, [gap(*WOMEN_DOOR, 2.2)] + strip(x0, x1, WOMEN_DOOR), y0=th)
    floor(up, x0, x1, z0, z1, 'tile', th)
    box(up, x0 - 0.12, x1 + 0.12, 2 * th - 0.2, 2 * th, z0 - 0.12, z1 + 0.12, 'slab')
    for p, q, r, s in ((x0, x1, z0, z0), (x0, x1, z1, z1), (x0, x0, z0, z1), (x1, x1, z0, z1)):
        box(up, p - 0.12, q + 0.12, 2 * th, 2 * th + 0.6, r - 0.12, s + 0.12, 'wall')  # parapet
    # Each tread stands on the ground; the landing is the last rise. The sloping handrails are in props.tscn.
    rise = th / (TREADS + 1)
    for i in range(TREADS): box(stair, FOOT + i * RUN / TREADS, FOOT + (i + 1) * RUN / TREADS, 0, (i + 1) * rise, *STAIR_Z, 'concrete')
    box(stair, *LANDING, 0, th, *STAIR_Z, 'concrete')
    rail = STAIR_Z[1] - 0.08, STAIR_Z[1] - 0.03
    for i in range(4): box(stair, FOOT + i * RUN / 3 - 0.03, FOOT + i * RUN / 3 + 0.03, i * th / 3, i * th / 3 + 1.0, *rail, 'iron')
    stair += mirror(stair[-4:], sum(STAIR_Z))
    box(stair, *LANDING, th + 1.0, th + 1.05, *rail, 'iron')
    box(stair, LANDING[1] - 0.08, LANDING[1] - 0.03, th + 1.0, th + 1.05, *STAIR_Z, 'iron')
    # The landing's handrail stops her; between each side's posts, under a handrail without collision, an unseen wall does.
    box(edge, FOOT, LANDING[0], 0, th + 1.1, STAIR_Z[1] - 0.1, STAIR_Z[1], 'unseen')
    edge += mirror(edge, sum(STAIR_Z))
    return (fixed('Toilet', toilet=down, toilet_stair=stair, toilet_stair_edge=edge),
            fixed('ToiletUpstairs', toilet_upstairs=up))


# A screen inside each door, washing troughs, stalls behind 1.2 m partitions with pans she walks over, a urinal trough.
def toilet_fittings():
    down, up = [], []
    (x0, x1), (z0, z1), th = TOILET_X, TOILET_Z, TOILET_H

    def stalls(boxes, y, along, at, first, n):  # n stalls 0.95 wide from first, 1.38 m deep from the wall at `at`
        for k in range(n + 1):
            p = first + k * 0.95
            if along == 'x': box(boxes, p - 0.03, p + 0.03, y, y + 1.2, at, at + 1.38, 'furn')  # off the north wall
            else: box(boxes, at - 1.38, at, y, y + 1.2, p - 0.03, p + 0.03, 'furn')  # off the east wall
        for k in range(n):
            p = first + 0.2 + k * 0.95
            if along == 'x': box(boxes, p, p + 0.55, y - 0.02, y + 0.04, at + 0.38, at + 0.98, 'white')
            else: box(boxes, at - 0.98, at - 0.38, y - 0.02, y + 0.04, p, p + 0.55, 'white')

    box(down, MEN_DOOR[0] - 0.9, MEN_DOOR[1] + 0.9, 0, 2.0, MEN_Z - 1.6, MEN_Z - 1.5, 'wall')
    stalls(down, 0, 'z', x1 - 0.12, z0 + 0.9, 8)
    box(down, x0 + 1.6, x1 - 2.2, 0, 0.25, z0 + 0.12, z0 + 0.5, 'concrete')
    box(down, x0 + 0.12, x0 + 0.72, 0, 0.8, z0 + 3, MEN_Z - 1.8, 'concrete')
    box(up, WOMEN_DOOR[0] - 0.9, WOMEN_DOOR[1] + 0.9, th, th + 2.0, z1 - 1.6, z1 - 1.5, 'wall')
    box(up, x0 + 3.0, WOMEN_DOOR[0] - 1.2, th, th + 0.8, z1 - 0.72, z1 - 0.12, 'concrete')
    stalls(up, th, 'z', x1 - 0.12, z0 + 0.9, 13)
    stalls(up, th, 'x', z0 + 0.12, x0 + 1.5, 12)
    return fixed('ToiletFittings', toilet_fittings=down, toilet_upstairs_fittings=up)


# The sports store: a 3 m double door to the courts, high windows; shelves of balls, mats, a vaulting box, hurdles, carts.
def store():
    shell, things = [], []
    (x0, x1), (z0, z1), h = STORE_X, STORE_Z, STORE_H
    def high(c): return window(c, 1.5, 1.8, 2.6)
    wall(shell, 'x', z0, x0, x1, h, [gap(9.5, 12.5, 2.6), high(5.5), high(16)])
    wall(shell, 'x', z1, x0, x1, h, [high(7), high(15)])
    wall(shell, 'z', x0, z0, z1, h, [high((z0 + z1) / 2)])
    wall(shell, 'z', x1, z0, z1, h)
    floor(shell, x0, x1, z0, z1, 'cement')
    box(shell, x0 - 0.12, x1 + 0.12, h - 0.2, h, z0 - 0.12, z1 + 0.12, 'slab')
    for x in (9.55, 12.45): box(shell, x - 0.025, x + 0.025, 0, 2.6, z0 + 0.12, z0 + 1.6, 'door')  # the leaves, open inward
    # The prototype's shelves are a solid block hiding the balls; these are open: a back, two ends, a board under each row.
    sx0, sx1, sz0, sz1 = x0 + 0.5, x1 - 0.5, z1 - 0.62, z1 - 0.12
    box(things, sx0, sx1, 0, 2.0, sz1 - 0.04, sz1, 'desk')
    for x in (sx0, sx1 - 0.04): box(things, x, x + 0.04, 0, 2.0, sz0, sz1, 'desk')
    for top in [y - BALL_R for y in BALL_ROWS] + [2.0]: box(things, sx0, sx1, top - 0.03, top, sz0, sz1, 'desk')
    for z in (z0 + 0.6, z0 + 2.2): box(things, x0 + 0.3, x0 + 2.3, 0, 0.6, z, z + 1.2, 'furn')
    box(things, x0 + 4.0, x0 + 5.4, 0, 1.1, z0 + 3.0, z0 + 3.5, 'desk')
    for i in range(4):
        z = z0 + 1.0 + i * 0.5
        box(things, x1 - 1.3, x1 - 0.3, 0.76, 0.84, z - 0.02, z + 0.02, 'line')
        for x in (x1 - 1.28, x1 - 0.32): box(things, x - 0.02, x + 0.02, 0, 0.8, z - 0.02, z + 0.02, 'iron')
    for x in (x0 + 8.0, x0 + 9.4): box(things, x, x + 1.0, 0, 0.9, z0 + 4.2, z0 + 5.0, 'iron')
    return fixed('Store', store=shell, store_furniture=things)


# Section I's Props: collision-only slopes through the front edges of the stand's and the outside stair's treads, and what
# upright boxes cannot draw, unknown to the engine: the plaza's rings, centre circles, rims, handrails, balls.
def props(circles, rims):
    subs, nodes = bodies([slope('StandSlope', 'z', STEPS_Z[0] - GO, 0, STEPS_Z[0] + (STEPS - 1) * GO, STAND_H, *STEPS_X),
                          slope('ToiletStairSlope', 'x', FOOT - RUN / TREADS, 0, LANDING[0], TOILET_H, *STAIR_Z)])

    def resource(kind, rid, **values):
        subs.append(f'[sub_resource type="{kind}" id="{rid}"]\n' + ''.join(f'{k} = {v}\n' for k, v in values.items()))
        return f'SubResource("{rid}")'

    def material(rgb): return resource('StandardMaterial3D', f'c{len(subs)}', albedo_color=f'Color({", ".join(fmt(c) for c in rgb)}, 1)')

    def show(name, mesh, position, **values):
        nodes.append(f'[node name="{name}" type="MeshInstance3D" parent="."]\nlayers = 3\nposition = {vec(*position)}\n'
                     + ''.join(f'{k} = {v}\n' for k, v in values.items()) + f'mesh = {mesh}\n')

    def disc(rid, r, rgb, h):
        return resource('CylinderMesh', rid, material=material(rgb), top_radius=r, bottom_radius=r, height=h, radial_segments=48, rings=1)

    # The prototype lays each ring over the last at 55% opacity; these are the colors that gives.
    x, z = (PLAZA_X[0] + PLAZA_X[1]) / 2, (PLAZA_Z[0] + PLAZA_Z[1]) / 2
    for i, (r, rgb) in enumerate(((13, (0.774, 0.737, 0.676)), (10, (0.836, 0.804, 0.751)), (7, (0.741, 0.705, 0.644)),
                                  (2.5, (0.767, 0.729, 0.665))), 1):
        show(f'PlazaRing{i}', disc(f'disc{i}', r, rgb, 0.004), (x, 0.002 + 0.004 * i, z))
    # A centre circle is a disc of the line's color under one of the court's, 0.1 m smaller, which the centre line crosses.
    for name, r, m, y in (('CentreCircle', 1.85, 'line', 0.0055), ('CentreCircleInside', 1.75, 'court', 0.0065)):
        mesh = disc(name, r, COLORS[m][:3], 0.001)
        for i, (x, z) in enumerate(circles, 1): show(f'{name}{i}', mesh, (x, y, z))
    red = material(COLORS['door'][:3])
    rim = resource('TorusMesh', 'rim', material=red, inner_radius=0.21, outer_radius=0.24, rings=16, ring_segments=6)
    for i, position in enumerate(rims, 1): show(f'Rim{i}', rim, position)
    th = TOILET_H
    bar = resource('BoxMesh', 'handrail', material=material(COLORS['iron'][:3]), size=vec(math.hypot(RUN, th), 0.05, 0.05))
    for i, z in enumerate((STAIR_Z[1] - 0.05, STAIR_Z[0] + 0.05), 1):
        show(f'StairHandrail{i}', bar, (FOOT + RUN / 2, th / 2 + 1.0, z), rotation=vec(0, 0, math.atan2(th, RUN)))
    ball = resource('SphereMesh', 'ball', material=red, radius=BALL_R, height=2 * BALL_R, radial_segments=8, rings=6)
    spots = [(STORE_X[0] + 0.8 + 0.45 * k, y, STORE_Z[1] - 0.37) for y in BALL_ROWS for k in range(33)]
    buffer = ', '.join(f'1, 0, 0, {fmt(x)}, 0, 1, 0, {fmt(y)}, 0, 0, 1, {fmt(z)}' for x, y, z in spots)
    many = resource('MultiMesh', 'balls', transform_format=1, instance_count=len(spots), mesh=ball,
                    buffer=f'PackedFloat32Array({buffer})')
    nodes.append(f'[node name="Balls" type="MultiMeshInstance3D" parent="."]\nlayers = 3\nmultimesh = {many}\n')
    return packed('Props', [], subs=subs, nodes=nodes)


def main():
    files = {'site.tscn': site(), 'lodge.tscn': lodge(), 'store.tscn': store(), 'toilet_fittings.tscn': toilet_fittings()}
    files['courts.tscn'], circles, rims = courts()
    files['toilet.tscn'], files['toilet_upstairs.tscn'] = toilet()
    files['props.tscn'] = props(circles, rims)
    here = Path(__file__).parent
    for path, text in files.items(): (here / path).write_text(text, encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
