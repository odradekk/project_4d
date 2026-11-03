"""The unseen walls that keep her where the decision note lets her go, for generate.py, which writes them into bounds.tscn
next to this file: along the main road's kerb beside its west sidewalk; across that sidewalk 20 m north of the asphalt
road's corner and at the mall's south-east corner; across the dirt road and its belts of poplars at the building site's
north end; and across the gap between the building site and the sports ground. Past each of them there is either nothing
to stand on or ground she is not to reach. The prototype stops its walker with walls 0.1 m thick in the same places; these
have the same faces toward her, but reach 1 m back from them, because thin boxes have caught the player's capsule before
(PR #79, thin paving).
Decision note: 2026-11-01-school-scene-layout
"""
from field import FIELD_X, FIELD_Z
from mall import X1 as MALL_EAST, Z1 as MALL_SOUTH
from north_west import MAIN, RICH_X, SITE_X, SITE_Z
from shapes import fixed
from tscn import box  # on the path once shapes is imported

DEPTH, HIGH = 1, 3  # how far each wall reaches back from its face, and its height; she cannot jump
# The west sidewalk's ends (z), as the prototype has them: 20 m north of the asphalt road, and 0.1 m inside the mall's
# south side.
NORTH_END, SOUTH_END = -30, MALL_SOUTH - 0.1


def files():
    sidewalk, dirt_road, gap = [], [], []
    box(sidewalk, MAIN, MAIN + DEPTH, 0, HIGH, NORTH_END - DEPTH, SOUTH_END + DEPTH, 'unseen')  # the kerb
    box(sidewalk, RICH_X[1], MAIN, 0, HIGH, NORTH_END - DEPTH, NORTH_END, 'unseen')  # from the estate's east railing
    box(sidewalk, MALL_EAST, MAIN, 0, HIGH, SOUTH_END, SOUTH_END + DEPTH, 'unseen')  # from the mall's east wall
    # From the building site's east hoarding to the estate's west railing, in line with their north ends.
    box(dirt_road, SITE_X[1], RICH_X[0], 0, HIGH, SITE_Z[0] - DEPTH, SITE_Z[0], 'unseen')
    # In line with the sports ground's fence on the east, from the hoarding's south side to the fence's north side.
    box(gap, FIELD_X[1] - DEPTH, FIELD_X[1], 0, HIGH, SITE_Z[1], FIELD_Z[0], 'unseen')
    return {'bounds.tscn': fixed('Bounds', sidewalk_edge=sidewalk, dirt_road_end=dirt_road, site_gap=gap)}
