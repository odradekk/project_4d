"""The sports ground west of the dirt road, for north_west.py: a 250 m track round a small football pitch with a goal at
each end, inside a wire fence with a gate in its east side, across the dirt road from the school's back gate. The ground,
the fence and the goals are fixed objects. The fence's posts and the markings of the track and the pitch, which are round
where boxes are not, are plain meshes in section I's Props that do not stop her: the markings lie in layers a millimetre
thick over the ground, as the plaza's rings do. The layout is the greybox prototype's FIELD, FIELD_GATE, TC, TR, TL, LANES
and LANE and its "sports ground" section.
Decision note: 2026-11-01-school-scene-layout
"""
from shapes import TOP, beam, fixed, material, multimesh, pave, posts, resource, runs, show, show_many
from tscn import COLORS, box, packed, vec  # on the path once shapes is imported

COLORS.update({'grass': (0.525, 0.627, 0.416, 1), 'track': (0.659, 0.439, 0.361, 1), 'wire': (0.29, 0.333, 0.314, 0.25)})

FIELD_X, FIELD_Z, FIELD_GATE = (-80.3, -17.6), (-27.8, 92), (6, 10)  # the fence's lines; its gate in the east side (z)
FENCE_H = 3
SIDES = (('z', FIELD_X[1], *FIELD_Z, [FIELD_GATE]), ('z', FIELD_X[0], *FIELD_Z, []), ('x', FIELD_Z[0], *FIELD_X, []),
         ('x', FIELD_Z[1], *FIELD_X, []))
TC, TR, TL, LANES, LANE = (-49, 32.8), 22, 55, 4, 1.22  # the track's centre, inside radius, straights and lanes
HW, HL, W = 19, 34, 0.12  # half the pitch's width and length; the width of its lines
LAYER = 0.001


def fixed_scene():
    ground, fence, goals = [], [], []
    pave(ground, *FIELD_X, *FIELD_Z, TOP, 'yard')
    for along, at, a, b, gaps in SIDES:
        # The wire mesh, 6 cm thick, which stops her, and rails at its foot and top. The prototype's mesh starts at the foot
        # rail, 0.1 m up; this one starts on the ground, with the rails inside it, because the player's capsule gets
        # wedged under a rail with a gap beneath it.
        for p, q in runs(a, b, gaps):
            beam(fence, along, at, p, q, 0, FENCE_H - 0.05, 0.03, 'wire')
            for y in (0.1, FENCE_H - 0.05): beam(fence, along, at, p, q, y - 0.03, y + 0.03, 0.025, 'iron')
    x, z = TC
    for zg in (z - HL, z + HL):  # 5 m wide and 2 m high, on the goal lines
        for px in (x - 2.5, x + 2.5): box(goals, px - 0.06, px + 0.06, 0, 2, zg - 0.06, zg + 0.06, 'line')
        box(goals, x - 2.56, x + 2.56, 1.9, 2, zg - 0.06, zg + 0.06, 'line')
    return fixed('Field', field=ground, field_fence=fence, goals=goals)


def props():
    subs, nodes, meshes = [], [], {}
    colors = {m: material(subs, COLORS[m][:3]) for m in ('line', 'track', 'grass')}

    def lay(name, size, m, x, z, k):  # a box (size: its x and z) or a disc (size: its radius) in the kth layer
        if (size, m) not in meshes:
            shape = (dict(size=vec(size[0], LAYER, size[1])) if isinstance(size, tuple) else
                     dict(top_radius=size, bottom_radius=size, height=LAYER, radial_segments=64, rings=1))
            meshes[size, m] = resource(subs, 'BoxMesh' if isinstance(size, tuple) else 'CylinderMesh', f'f{len(meshes) + 1}',
                                       material=colors[m], **shape)
        show(nodes, name, meshes[size, m], (x, TOP + (k + 0.5) * LAYER, z))

    # The track: filled outlines of it, each over the last and 4 cm inside the line it starts, so that of each line only
    # its 8 cm show; inside the lane nearest the pitch, the grass. An outline is a half disc at each end of a rectangle.
    x, z = TC
    outlines = [(TR + i * LANE + d, m) for i in range(LANES, -1, -1) for d, m in ((0.04, 'line'), (-0.04, 'track'))]
    outlines[-1] = (TR - 0.04, 'grass')
    for k, (r, m) in enumerate(outlines):
        lay(f'Track{k + 1}', (2 * r, TL), m, x, z, k)
        for s, end in ((-1, 'North'), (1, 'South')): lay(f'Track{k + 1}{end}', r, m, x, z + s * TL / 2, k)
    # The pitch, 38 × 68 m: touchlines and goal lines, the penalty areas 24 × 10 m, the centre circle 6 m in radius that
    # the halfway line crosses.
    lines = [(x - HW, x + HW, z - HL, z - HL + W), (x - HW, x + HW, z + HL - W, z + HL),
             (x - HW, x - HW + W, z - HL, z + HL), (x + HW - W, x + HW, z - HL, z + HL)]
    for s in (-1, 1):
        a, b = sorted((z + s * HL, z + s * (HL - 10)))
        front = (b - W, b) if s < 0 else (a, a + W)
        lines += [(x - 12, x - 12 + W, a, b), (x + 12 - W, x + 12, a, b), (x - 12, x + 12, *front)]
    top = len(outlines)
    for i, (x0, x1, z0, z1) in enumerate(lines, 1): lay(f'Line{i}', (x1 - x0, z1 - z0), 'line', (x0 + x1) / 2, (z0 + z1) / 2, top)
    lay('CentreCircle', 6.05, 'line', x, z, top)
    lay('CentreCircleInside', 5.95, 'grass', x, z, top + 1)
    lay('HalfwayLine', (2 * HW, W), 'line', x, z, top + 2)
    spots = [(p, FENCE_H / 2, at) if along == 'x' else (at, FENCE_H / 2, p)
             for along, at, a, b, gaps in SIDES for run in runs(a, b, gaps) for p in posts(*run, 2.5)]
    post = resource(subs, 'BoxMesh', 'post', material=material(subs, COLORS['iron'][:3]), size=vec(0.08, FENCE_H, 0.08))
    show_many(nodes, 'FencePosts', multimesh(subs, 'posts', post, spots))
    return packed('Field', [], subs=subs, nodes=nodes)
