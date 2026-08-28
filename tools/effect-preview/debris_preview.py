"""Draws the rubble a strike throws, at true scale, so its size can be judged before a playtest.

This is a faithful port of Core/BlastDebris + Core/DebrisFlight plus the object budget in
Game/Effects/DebrisFx. If a constant changes in C# it must change here - the point of this
script is that what it draws is what the player gets.

It exists because the debris was wrong twice in ways the screen alone could not explain, and
then wrong a third time in a way it could: the pieces were 34 m across, which is a building,
not rubble. Sizes are now measured against the game's own vehicles rather than chosen.

Usage:  python tools/effect-preview/debris_preview.py <out-folder>

Outputs a top-down field of the rubble at its landing spread for a conventional and a nuclear
strike, drawn at true metres, and prints the size and coverage figures.
"""
import math
import os
import struct
import sys
import zlib

import numpy as np

# ------------------------------------------------------------------ C# ports (keep in sync)

# Core/BlastDebris
GRAVITY = 9.81
RANGE_FRACTION = 0.35
LAUNCH_ANGLE_DEG = 32.0
EMIT_FRACTION = 0.07
EMIT_RADIUS_MIN, EMIT_RADIUS_MAX = 12.0, 420.0
RANGE_MIN, RANGE_MAX = 30.0, 400.0
FLIGHT_SECONDS_MAX = 9.0
CHUNK_SIZE_FRACTION = 0.01375
CHUNK_SIZE_MIN, CHUNK_SIZE_MAX = 4.0, 5.5
CHUNKS_MIN, CHUNKS_MAX = 40, 520

# Game/Effects/DebrisFx
MAX_CHUNK_OBJECTS = 320

# Core/DebrisFlight
DRAG = 0.12


def clamp(v, lo, hi):
    return lo if v < lo else (hi if v > hi else v)


def emit_radius(blast):
    return 0.0 if blast <= 0 else clamp(blast * EMIT_FRACTION, EMIT_RADIUS_MIN, EMIT_RADIUS_MAX)


def throw_range(blast):
    return 0.0 if blast <= 0 else clamp(blast * RANGE_FRACTION, RANGE_MIN, RANGE_MAX)


def launch_speed(r):
    return 0.0 if r <= 0 else math.sqrt(GRAVITY * r / math.sin(2 * math.radians(LAUNCH_ANGLE_DEG)))


def chunk_size(r):
    return clamp(r * CHUNK_SIZE_FRACTION, CHUNK_SIZE_MIN, CHUNK_SIZE_MAX)


def chunk_count(r):
    if r <= 0:
        return 0
    return int(clamp(CHUNKS_MIN + (CHUNKS_MAX - CHUNKS_MIN) * math.sqrt(r / RANGE_MAX),
                     CHUNKS_MIN, CHUNKS_MAX))


def hash01(index, seed, salt):
    h = np.uint32((index * 374761393 + seed * 668265263 + salt * 1274126177) & 0xFFFFFFFF)
    h ^= h >> np.uint32(13)
    h = np.uint32(int(h) * 1911520717 & 0xFFFFFFFF)
    h ^= h >> np.uint32(16)
    return float(int(h) & 0xFFFFFF) / float(0x1000000)


class Launch:
    """DebrisFlight.Launch."""
    def __init__(self, i, seed, emit_r, speed, size, variants=4):
        az = hash01(i, seed, 1) * 2 * math.pi
        radius = emit_r * math.sqrt(hash01(i, seed, 2))
        c, s = math.cos(az), math.sin(az)
        self.x, self.y, self.z = radius * c, 0.0, radius * s
        ang = math.radians(LAUNCH_ANGLE_DEG * (0.7 + 0.6 * hash01(i, seed, 3)))
        v = speed * (0.55 + 0.45 * hash01(i, seed, 4))
        horiz = v * math.cos(ang)
        self.vx, self.vy, self.vz = horiz * c, v * math.sin(ang), horiz * s
        roll = hash01(i, seed, 8)
        self.scale = size * (0.5 + 0.5 * roll * roll)
        self.variant = int(hash01(i, seed, 9) * variants) % variants

    def position_at(self, t):
        travel = (1.0 - math.exp(-DRAG * t)) / DRAG if DRAG > 0 else t
        return (self.x + self.vx * travel,
                self.y + self.vy * t - 0.5 * GRAVITY * t * t,
                self.z + self.vz * travel)

    def flight_seconds(self):
        return 0.0 if self.vy <= 0 else 2 * self.vy / GRAVITY


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


def draw_field(blast, label, px=760):
    """Top-down: where the rubble lands and how big each piece is, at true metres."""
    r = throw_range(blast)
    speed = launch_speed(r)
    size = chunk_size(r)
    count = min(chunk_count(r), MAX_CHUNK_OBJECTS)
    emit_r = emit_radius(blast)

    launches = [Launch(i, 7, emit_r, speed, size) for i in range(count)]
    landings = [l.position_at(l.flight_seconds()) for l in launches]
    dists = [math.hypot(p[0], p[2]) for p in landings]

    frame = max(dists) * 1.12
    mpp = 2 * frame / px                       # metres per pixel
    img = np.full((px, px, 3), 40, np.uint8)   # ground

    # The emit disc and the throw ring, for scale.
    yy, xx = np.mgrid[0:px, 0:px]
    d = np.hypot(xx - px / 2, yy - px / 2) * mpp
    img[d < emit_r] = (58, 50, 44)
    ring = np.abs(d - r) < mpp
    img[ring] = (86, 74, 62)

    covered = 0
    for l, p in zip(launches, landings):
        cx = int(px / 2 + p[0] / mpp)
        cy = int(px / 2 + p[2] / mpp)
        half = max(l.scale / 2 / mpp, 0.5)
        x0, x1 = int(cx - half), int(math.ceil(cx + half))
        y0, y1 = int(cy - half), int(math.ceil(cy + half))
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, px), min(y1, px)
        if x1 <= x0 or y1 <= y0:
            continue
        shade = 120 + int(70 * (l.variant / 3.0))
        img[y0:y1, x0:x1] = (shade, int(shade * 0.90), int(shade * 0.82))
        covered += (x1 - x0) * (y1 - y0)

    stats = dict(
        label=label, blast=blast, throw=r, emit=emit_r, count=count,
        size_min=min(l.scale for l in launches), size_max=max(l.scale for l in launches),
        land_min=min(dists), land_max=max(dists),
        flight_max=max(l.flight_seconds() for l in launches),
        frame=2 * frame, coverage=covered / float(px * px),
    )
    return img, stats


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    if not os.path.isdir(out):
        os.makedirs(out)

    cases = [(72.0, "1 t conventional"), (3720.0, "150 kt nuclear")]
    panels = []
    for blast, label in cases:
        img, st = draw_field(blast, label)
        panels.append(img)
        print("{label}: blast {blast:.0f} m -> {count} chunks of {size_min:.1f}-{size_max:.1f} m,"
              " thrown from a {emit:.0f} m disc {throw:.0f} m further".format(**st))
        print("    landing {land_min:.0f}-{land_max:.0f} m, longest flight {flight_max:.1f} s,"
              " frame {frame:.0f} m, rubble covers {coverage:.3%} of it".format(**st))

    gap = np.full((panels[0].shape[0], 8, 3), 24, np.uint8)
    write_png(os.path.join(out, "debris-field.png"), np.hstack([panels[0], gap, panels[1]]))
    print("wrote " + os.path.join(out, "debris-field.png"))


if __name__ == "__main__":
    main()
