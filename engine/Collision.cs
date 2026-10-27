namespace FourD;

// Contact between any two solids.
internal static class Collision
{
    // Locating a depth stops when the interval is this narrow, far below any distance that matters. It follows
    // the secant for this many steps and then bisects.
    private const double Narrow = 1e-13;
    private const int SecantSteps = 12;

    // How deep `a` embeds into `b`: zero when they just touch, negative when they are apart.
    internal static double Depth(Solid4 a, Solid4 b) => (a, b) switch
    {
        (Box4 first, Box4 second) => Box4.Depth(first, second),
        (Round4 round, Box4 box) => round.Tube - round.MinOverCore(box.SignedDistance).Value,
        (Box4 box, Round4 round) => Depth(round, box),
        (Round4 first, Round4 second) => first.Tube + second.Tube - first.MinOverCore(second.CoreDistance).Value,
        _ => throw new ArgumentException("Unknown solid kind.")
    };

    internal static bool Overlaps(Solid4 a, Solid4 b) => Depth(a, b) > World.ContactTolerance;

    // The contact where `moving` embeds into `obstacle` deeper than the contact tolerance, or null.
    internal static Contact? Touch(Solid4 moving, Solid4 obstacle) =>
        Overlaps(moving, obstacle) ? ContactOf(moving, obstacle) : null;

    // Where two solids that touch or overlap meet: a point inside both, and the unit normal from `obstacle`
    // toward `moving`. Section is left for the caller. Null only for boxes that have no point in common.
    internal static Contact? ContactOf(Solid4 moving, Solid4 obstacle)
    {
        switch (moving, obstacle)
        {
            case (Box4 first, Box4 second):
                return Box4.ContactOf(first, second);
            case (Round4 round, Box4 box):
                var (distance, core) = round.MinOverCore(box.SignedDistance);
                var (surface, normal) = box.Nearest(core);
                return new(round.Entity, round.Part, box.Entity, box.Part, Box4.ToVec4(distance >= 0 ? surface : core), Box4.ToVec4(normal), null);
            case (Box4 box, Round4 round):
                var seen = ContactOf(round, box)!;
                return seen with
                {
                    Moving = box.Entity,
                    MovingPart = box.Part,
                    Obstacle = round.Entity,
                    ObstaclePart = round.Part,
                    Normal = new(-seen.Normal.X, -seen.Normal.Y, -seen.Normal.Z, -seen.Normal.W)
                };
            case (Round4 first, Round4 second):
                return RoundContact(first, second);
            default:
                throw new ArgumentException("Unknown solid kind.");
        }
    }

    private static Contact RoundContact(Round4 moving, Round4 obstacle)
    {
        var (distance, core) = moving.MinOverCore(obstacle.CoreDistance);
        var other = obstacle.NearestCore(core);
        var direction = distance > World.RoundingMargin
            ? Box4.Scale(Box4.Sub(core, other), 1 / distance)
            : [0, 1, 0, 0];
        // Along the line between the two circles, the middle of the stretch that lies inside both solids.
        var along = (Math.Max(0, distance - moving.Tube) + Math.Min(obstacle.Tube, distance)) / 2;
        var point = other.Select((value, i) => value + along * direction[i]).ToArray();
        return new(moving.Entity, moving.Part, obstacle.Entity, obstacle.Part, Box4.ToVec4(point), Box4.ToVec4(direction), null);
    }

    // The least distance along `direction` (a unit vector), no greater than `limit`, at which `moving` embeds at
    // least `level` deep into `obstacle`; null when it never does. Depth changes by no more than the distance
    // moved, so the search skips the gap that remains, and locates the level in the first stretch that passes it.
    internal static double? FirstReach(Solid4 moving, Solid4 obstacle, double[] direction, double level, double limit)
    {
        double DepthAt(double distance) => Depth(moving.Moved(Box4.Scale(direction, distance)), obstacle);
        var depth = DepthAt(0);
        if (depth >= level) return 0;
        var below = 0d;
        while (below < limit)
        {
            var next = Math.Min(limit, below + Math.Max(World.SamplingDistance, level - depth));
            var nextDepth = DepthAt(next);
            if (nextDepth >= level) return Locate(DepthAt, level, (below, depth - level), (next, nextDepth - level));
            (below, depth) = (next, nextDepth);
        }
        return null;
    }

    // Moving along `direction` (a unit vector), up to `limit`, `moving` is blocked by `obstacle` where it first
    // embeds `blockLevel` deep. This returns the distance of the last place before that where the depth is no more
    // than `allowed`, which is at least the depth at the start, or null when it is never blocked. The depth may fall
    // and rise again on the way, as it does for a ring, so the search keeps the latest stretch in which the depth
    // rose past `allowed`.
    internal static double? StopBefore(Solid4 moving, Solid4 obstacle, double[] direction, double allowed, double blockLevel, double limit)
    {
        double DepthAt(double distance) => Depth(moving.Moved(Box4.Scale(direction, distance)), obstacle);
        double distance = 0, depth = DepthAt(0);
        ((double Distance, double Gap) Start, (double Distance, double Gap) End)? rise = null;
        while (distance < limit)
        {
            // The depth cannot pass the level it is heading for before the gap to it is closed.
            var level = depth <= allowed ? allowed : blockLevel;
            var next = Math.Min(limit, distance + Math.Max(World.SamplingDistance, level - depth));
            var nextDepth = DepthAt(next);
            if (depth <= allowed && nextDepth > allowed) rise = ((distance, depth - allowed), (next, nextDepth - allowed));
            if (nextDepth >= blockLevel) return Locate(DepthAt, allowed, rise!.Value.Start, rise.Value.End);
            (distance, depth) = (next, nextDepth);
        }
        return null;
    }

    // The last distance below `level` between two distances, where the depth is below it at the first and not at
    // the second; each comes with how far its depth is from the level. The first steps follow the secant through
    // the two ends, and halve the end that stays behind (the Illinois method), which is fast when the depth is
    // nearly linear. If that has not narrowed the interval enough the rest are bisections, so that the number
    // of steps is bounded.
    private static double Locate(Func<double, double> depthAt, double level, (double Distance, double Gap) start, (double Distance, double Gap) end)
    {
        (double low, double below) = start;
        (double high, double above) = end;
        var lastMoved = 0;
        for (var step = 0; high - low > Narrow; step++)
        {
            var guess = step < SecantSteps ? (low * above - high * below) / (above - below) : (low + high) / 2;
            var depth = depthAt(guess) - level;
            if (depth >= 0)
            {
                (high, above) = (guess, depth);
                if (lastMoved == 1) below /= 2;
                lastMoved = 1;
            }
            else
            {
                (low, below) = (guess, depth);
                if (lastMoved == -1) above /= 2;
                lastMoved = -1;
            }
        }
        return low;
    }
}
