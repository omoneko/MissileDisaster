"""Draws the rubble a blast sweeps outward, at true scale, so it can be judged before a playtest.

This is a faithful port of Core/BlastDebris + Core/DebrisSweep plus the object budget in
Game/Effects/DebrisFx. If a constant changes in C# it must change here - the point of this
script is that what it draws is what the player gets.

It exists because the debris was wrong three times in a row: invisible twice for reasons the
screen could not explain, then 34 m across, which is a building rather than rubble. It is now
car-sized and swept outward as one ring rather than thrown on individual arcs, and both of
those are things a picture can settle.

Usage:  python tools/effect-preview/debris_preview.py <out-folder>

Outputs the ring at four moments of its run for a conventional and a nuclear strike, drawn at
true metres from directly above, and prints where the band is and how high the pieces get.
"""
import math
import os
import struct
import sys
import zlib

import numpy as np

# ------------------------------------------------------------------ C# ports (keep in sync)

# Core/BlastDebris
RANGE_FRACTION = 0.35
EMIT_FRACTION = 0.07
EMIT_RADIUS_MIN, EMIT_RADIUS_MAX = 12.0, 420.0
RANGE_MIN, RANGE_MAX = 30.0, 400.0
CHUNK_SIZE_FRACTION = 0.01375
CHUNK_SIZE_MIN, CHUNK_SIZE_MAX = 4.0, 5.5
CHUNKS_MIN, CHUNKS_MAX = 40, 520

# Core/ShockWave
FRONT_EXPONENT = 0.4
AVERAGE_FRONT_SPEED = 540.0
FRONT_SECONDS_MIN, FRONT_KNEE, FRONT_CEILING = 0.35, 14.0, 26.0

# Core/DebrisSweep
SWEEP_EXPONENT = FRONT_EXPONENT
TARGET_MIN, TARGET_MAX = 0.75, 1.15
MIN_TRAVEL_FRACTION = 0.15
CARRY_FACTOR = 1.4
CARRY_SECONDS_MIN, CARRY_SECONDS_MAX = 1.5, 9.0
HOP_HEIGHT_MIN, HOP_HEIGHT_MAX = 1.2, 3.0
HOPS_MIN, HOPS_MAX = 2, 4
ROLL_SLIP = 0.35
HOP_HEIGHT_RANGE_CAP = 0.10

# Game/Effects/DebrisFx
MAX_CHUNK_OBJECTS = 320


def clamp(v, lo, hi):
    return lo if v < lo else (hi if v > hi else v)


def soft_ceiling(v, floor, knee, ceiling):
    """Core/EffectCeiling.Soft."""
    if v < floor:
        return floor
    if v <= knee:
        return v
    span = ceiling - knee
    return knee + span * (1.0 - math.exp(-(v - knee) / span))


def emit_radius(blast):
    return 0.0 if blast <= 0 else clamp(blast * EMIT_FRACTION, EMIT_RADIUS_MIN, EMIT_RADIUS_MAX)


def throw_range(blast):
    return 0.0 if blast <= 0 else clamp(blast * RANGE_FRACTION, RANGE_MIN, RANGE_MAX)


def chunk_size(r):
    return clamp(r * CHUNK_SIZE_FRACTION, CHUNK_SIZE_MIN, CHUNK_SIZE_MAX)


def chunk_count(r):
    if r <= 0:
        return 0
    return int(clamp(CHUNKS_MIN + (CHUNKS_MAX - CHUNKS_MIN) * math.sqrt(r / RANGE_MAX),
                     CHUNKS_MIN, CHUNKS_MAX))


def front_seconds(blast):
    return 0.0 if blast <= 0 else soft_ceiling(blast / AVERAGE_FRONT_SPEED,
                                               FRONT_SECONDS_MIN, FRONT_KNEE, FRONT_CEILING)


def carry_seconds(front):
    return clamp(front * CARRY_FACTOR, CARRY_SECONDS_MIN, CARRY_SECONDS_MAX)


def hash01(index, seed, salt):
    h = np.uint32((index * 374761393 + seed * 668265263 + salt * 1274126177) & 0xFFFFFFFF)
    h ^= h >> np.uint32(13)
    h = np.uint32(int(h) * 1911520717 & 0xFFFFFFFF)
    h ^= h >> np.uint32(16)
    return float(int(h) & 0xFFFFFF) / float(0x1000000)


class Ride:
    """DebrisSweep.Deal."""
    def __init__(self, i, seed, emit_r, rng, carry, size, variants=4):
        az = hash01(i, seed, 1) * 2 * math.pi
        start = emit_r * math.sqrt(hash01(i, seed, 2))
        self.dir_x, self.dir_z = math.cos(az), math.sin(az)
        self.start_x, self.start_z = start * self.dir_x, start * self.dir_z

        target = rng * (TARGET_MIN + (TARGET_MAX - TARGET_MIN) * hash01(i, seed, 3))
        self.distance = max(target - start, rng * MIN_TRAVEL_FRACTION)
        self.carry = carry * (0.8 + 0.4 * hash01(i, seed, 4))

        roll = hash01(i, seed, 8)
        self.scale = size * (0.5 + 0.5 * roll * roll)
        self.variant = int(hash01(i, seed, 9) * variants) % variants
        self.hop_height = min(
            self.scale * (HOP_HEIGHT_MIN + (HOP_HEIGHT_MAX - HOP_HEIGHT_MIN) * hash01(i, seed, 5)),
            rng * HOP_HEIGHT_RANGE_CAP)
        self.hops = min(HOPS_MIN + int(hash01(i, seed, 6) * (HOPS_MAX - HOPS_MIN + 1)), HOPS_MAX)
        self.roll_degrees = 360.0 * self.distance / (math.pi * self.scale) * ROLL_SLIP

    def progress(self, t):
        return clamp(t / self.carry, 0.0, 1.0) if self.carry > 0 else 1.0

    def travel_at(self, t):
        return self.distance * self.progress(t) ** SWEEP_EXPONENT

    def height_at(self, t):
        u = self.progress(t)
        if u >= 1.0 or self.hops <= 0:
            return 0.0
        return self.hop_height * abs(math.sin(math.pi * self.hops * u)) * (1.0 - u) ** 2

    def position_at(self, t):
        travel = self.travel_at(t)
        return (self.start_x + self.dir_x * travel,
                self.height_at(t),
                self.start_z + self.dir_z * travel)


# ---------------------------------------------------------------------------- rendering

def write_png(path, rgb):
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def draw_moment(rides, t, emit_r, rng, frame, px):
    """Straight down on the strike, at true metres."""
    mpp = 2 * frame / px
    img = np.full((px, px, 3), 40, np.uint8)

    yy, xx = np.mgrid[0:px, 0:px]
    d = np.hypot(xx - px / 2, yy - px / 2) * mpp
    img[d < emit_r] = (58, 50, 44)                 # the destroyed disc
    img[np.abs(d - rng) < mpp] = (78, 68, 58)      # where the sweep is aimed

    radii = []
    for r in rides:
        p = r.position_at(t)
        radii.append(math.hypot(p[0], p[2]))
        cx, cy = int(px / 2 + p[0] / mpp), int(px / 2 + p[2] / mpp)
        half = max(r.scale / 2 / mpp, 0.5)
        x0, y0 = max(int(cx - half), 0), max(int(cy - half), 0)
        x1, y1 = min(int(math.ceil(cx + half)), px), min(int(math.ceil(cy + half)), px)
        if x1 <= x0 or y1 <= y0:
            continue
        # Brighter the higher it is skipping, so the bounce is visible from above.
        lift = min(p[1] / max(r.hop_height, 0.001), 1.0)
        shade = int(110 + 60 * (r.variant / 3.0) + 55 * lift)
        img[y0:y1, x0:x1] = (min(shade, 255), int(shade * 0.90), int(shade * 0.82))
    return img, radii


def run(blast, label, px=520):
    rng = throw_range(blast)
    emit_r = emit_radius(blast)
    carry = carry_seconds(front_seconds(blast))
    size = chunk_size(rng)
    count = min(chunk_count(rng), MAX_CHUNK_OBJECTS)
    rides = [Ride(i, 7, emit_r, rng, carry, size) for i in range(count)]

    frame = rng * TARGET_MAX * 1.25
    moments = [carry * f for f in (0.08, 0.3, 0.6, 1.0)]
    panels, lines = [], []
    for t in moments:
        img, radii = draw_moment(rides, t, emit_r, rng, frame, px)
        panels.append(img)
        band = (max(radii) - min(radii)) / max(radii)
        lines.append("    t={0:4.1f}s  band {1:4.0f}-{2:4.0f} m (spread {3:3.0%})"
                     .format(t, min(radii), max(radii), band))

    highest = max(max(r.height_at(t) for t in np.linspace(0, r.carry, 60)) for r in rides)
    print("{0}: blast {1:.0f} m -> {2} pieces of {3:.1f}-{4:.1f} m swept off a {5:.0f} m disc "
          "out to {6:.0f} m over {7:.1f} s".format(
              label, blast, count, min(r.scale for r in rides), max(r.scale for r in rides),
              emit_r, rng, carry))
    print("\n".join(lines))
    print("    highest skip {0:.1f} m, which is {1:.1%} of how far it travels"
          .format(highest, highest / rng))

    gap = np.full((px, 6, 3), 24, np.uint8)
    row = [panels[0]]
    for p in panels[1:]:
        row += [gap, p]
    return np.hstack(row)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    if not os.path.isdir(out):
        os.makedirs(out)

    rows = [run(72.0, "1 t conventional"), run(3720.0, "150 kt nuclear")]
    gap = np.full((6, rows[0].shape[1], 3), 24, np.uint8)
    write_png(os.path.join(out, "debris-sweep.png"), np.vstack([rows[0], gap, rows[1]]))
    print("wrote " + os.path.join(out, "debris-sweep.png"))


if __name__ == "__main__":
    main()
