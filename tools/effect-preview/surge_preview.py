"""Draws the base surge against the mushroom it stands under, so the dome can be judged offline.

A faithful port of Core/GroundDust + Core/CloudDrift + the slice of Core/NuclearCloudDisplay and
Core/ConventionalCloudDisplay they are sized from. If a constant changes in C# it must change
here - the point is that what this draws is what the player gets.

It answers the three things that were actually asked for: does the dome expand outward and
upward at once, is it slower than the cloud, and does it end up big enough to subsume it.

Usage:  python tools/effect-preview/surge_preview.py <out-folder>
"""
import math
import os
import struct
import sys
import zlib

import numpy as np

# ------------------------------------------------------------------ C# ports (keep in sync)

# Core/GroundDust
GROWTH_EXPONENT = 0.35
RADIUS_PER_CAP = 1.3
HEIGHT_PER_CLOUD_TOP = 0.62
BIRTH_RADIUS_PER_CAP = 0.12
GROWTH_PER_RISE = 2.6
HOLD_FRACTION, FADE_FRACTION = 0.35, 0.8
PUFF_SIZE_MIN, PUFF_SIZE_MAX = 0.20, 0.40
PUFF_COUNT = 340
SHELL_DEPTH = 0.42

# Core/CloudDrift
DRIFT_TOP_SPEED = 5.5
DRIFT_BASE_FRACTION = 0.25


def soft(v, floor, knee, ceil):
    if v < floor:
        return floor
    if v <= knee:
        return v
    span = ceil - knee
    return knee + span * (1.0 - math.exp(-(v - knee) / span))


def nuclear_dims(kt):
    """The slice of NuclearCloudDisplay.For the surge needs."""
    cloud_scale = 0.06
    real_cap = 600.0 * 10 ** (0.0137 * 2.1761 ** 3 - 0.0358 * 2.1761 ** 2 + 0.37 * 2.1761)
    l = math.log10(kt); l2 = l * l
    real_top = 3000.0 * 10 ** (0.006941 * l2 * l2 - 0.06216 * l2 * l + 0.1526 * l2 + 0.1878 * l)

    def s(v, k, c):
        if v <= k:
            return v
        span = c - k
        return k + span * (1.0 - math.exp(-(v - k) / span))

    top_real, cap_real = s(real_top, 12000.0, 30000.0), s(real_cap, 8000.0, 26000.0)
    cap = cap_real * cloud_scale * 1.3
    top = s(top_real * cloud_scale, 900.0, 2000.0)
    rise = max(soft(600.0 * (kt / 1000.0) ** 0.25 / 38.0, 5.0, 10.0, 16.0), 5.0)
    return cap, top, rise


def conventional_dims(fireball):
    fb = soft(fireball, 3.0, 18.0, 24.0)
    top = fb * 9.0
    cap = fb * 2.0
    rise = soft(0.7 * math.sqrt(fb), 1.5, 4.0, 6.0)
    return cap, top, rise


def hash01(index, seed, salt):
    h = np.uint32((index * 374761393 + seed * 668265263 + salt * 1274126177) & 0xFFFFFFFF)
    h ^= h >> np.uint32(13)
    h = np.uint32(int(h) * 1911520717 & 0xFFFFFFFF)
    h ^= h >> np.uint32(16)
    return float(int(h) & 0xFFFFFF) / float(0x1000000)


def growth_seconds(rise):
    return max(rise * GROWTH_PER_RISE, 2.0)


def total_seconds(rise):
    return growth_seconds(rise) * (1.0 + HOLD_FRACTION + FADE_FRACTION)


def radius_at(t, cap, rise):
    g = growth_seconds(rise)
    u = min(max(t / g, 0.0), 1.0)
    birth, final = cap * BIRTH_RADIUS_PER_CAP, cap * RADIUS_PER_CAP
    return birth + (final - birth) * u ** GROWTH_EXPONENT


def height_at(t, top, rise):
    g = growth_seconds(rise)
    u = min(max(t / g, 0.0), 1.0)
    return top * HEIGHT_PER_CLOUD_TOP * u ** (GROWTH_EXPONENT * 1.45)


def alpha_at(t, rise):
    g = growth_seconds(rise)
    if t < 0:
        return 0.0
    fade_in = g * 0.08
    if t < fade_in:
        return t / fade_in
    steady = g * (1.0 + HOLD_FRACTION)
    if t <= steady:
        return 1.0
    u = (t - steady) / (g * FADE_FRACTION)
    return 0.0 if u >= 1 else 1.0 - u


def drift(t, rise, height_fraction):
    if t <= 0:
        return 0.0
    hf = min(max(height_fraction, 0.0), 1.0)
    speed = DRIFT_TOP_SPEED * (DRIFT_BASE_FRACTION + (1 - DRIFT_BASE_FRACTION) * hf)
    ramp = min(t / rise, 1.0) ** 2 if rise > 0 else 1.0
    return speed * t * ramp


def surge_puffs(t, cap, top, rise, seed=7):
    r, h = radius_at(t, cap, rise), height_at(t, top, rise)
    out = []
    for i in range(PUFF_COUNT):
        az = hash01(i, seed, 1) * 2 * math.pi
        polar = (math.pi * 0.5) * hash01(i, seed, 2) ** 1.6
        shell = 1.0 - SHELL_DEPTH * hash01(i, seed, 3)
        horiz = r * shell * math.cos(polar)
        x, z = horiz * math.cos(az), horiz * math.sin(az)
        y = h * shell * math.sin(polar)
        churn = t * 0.35 + hash01(i, seed, 4) * 2 * math.pi
        x += r * 0.03 * math.sin(churn)
        y = max(y + h * 0.05 * math.sin(churn * 1.3), 0.0)
        z += r * 0.03 * math.cos(churn * 0.8)
        size = r * (PUFF_SIZE_MIN + (PUFF_SIZE_MAX - PUFF_SIZE_MIN) * hash01(i, seed, 5))
        x += drift(t, rise, y / max(h, 0.001))
        out.append((x, y, z, size))
    return out, r, h


# ---------------------------------------------------------------------------- rendering

def write_png(path, rgb):
    hh, ww, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(hh))

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", ww, hh, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def draw_side(t, cap, top, rise, frame_w, frame_h, px=440):
    """Side elevation: the dome against the cloud's own envelope, at true metres."""
    img = np.full((px, px, 3), 26, np.uint8)
    mppx, mppy = frame_w / px, frame_h / px
    ground = px - 8

    # The cloud's envelope, so "does the dome subsume it" is answerable by eye.
    for sy in range(px):
        for_h = (ground - sy) * mppy
        if for_h < 0:
            continue
        half = cap if for_h > top * 0.58 else cap * 0.32
        if for_h > top:
            continue
        cx = px / 2 + drift(t, rise, for_h / top) / mppx
        for s in (-1, 1):
            sx = int(cx + s * half / mppx)
            if 0 <= sx < px:
                img[sy, sx] = (70, 70, 78)

    puffs, r, h = surge_puffs(t, cap, top, rise)
    a = alpha_at(t, rise)
    for x, y, z, size in puffs:
        sx, sy = int(px / 2 + x / mppx), int(ground - y / mppy)
        rad = max(int(size / 2 / mppx), 1)
        y0, y1 = max(sy - rad, 0), min(sy + rad, px)
        x0, x1 = max(sx - rad, 0), min(sx + rad, px)
        if x1 <= x0 or y1 <= y0:
            continue
        # Composited over, not added. Additive accumulation saturated the whole dome to white,
        # which is exactly the "cannot judge the density from the picture" problem this script
        # exists to avoid.
        lift = y / max(h, 0.001)
        colour = np.array([133 - 46 * lift, 102 - 40 * lift, 84 - 34 * lift])
        cover = 0.22 * a          # one puff's contribution; the crowd builds the opacity
        patch = img[y0:y1, x0:x1].astype(np.float32)
        img[y0:y1, x0:x1] = (patch * (1 - cover) + colour * cover).astype(np.uint8)
    img[ground:ground + 2, :] = (60, 55, 48)
    return img, r, h


def run(label, cap, top, rise, out):
    g = growth_seconds(rise)
    total = total_seconds(rise)
    print(f"\n{label}: cap {cap:.0f} m, cloud top {top:.0f} m, cloud rise {rise:.1f} s")
    print(f"  surge grows for {g:.1f} s ({g/rise:.1f}x the cloud's rise), gone at {total:.1f} s")
    frame_w, frame_h = cap * RADIUS_PER_CAP * 2.6, top * 1.05
    panels = []
    for frac in (0.03, 0.15, 0.5, 1.0, 1.6):
        t = g * frac
        img, r, h = draw_side(t, cap, top, rise, frame_w, frame_h)
        panels.append(img)
        # How solid the dome actually draws, measured in its body rather than assumed - the
        # same discipline cloud_preview.py enforces on the mushroom.
        band = img[int(440 * 0.72):int(440 * 0.88), int(440 * 0.42):int(440 * 0.58)]
        solidity = float(band.mean()) / 133.0
        print(f"    t={t:5.1f}s  dome {r:6.0f} m wide x {h:5.0f} m tall  "
              f"({r/cap:4.2f}x cap, {h/top:4.2f}x cloud top)  alpha {alpha_at(t, rise):.2f}  "
              f"drift {drift(t, rise, 1.0):5.0f} m  body {solidity:.2f}")
    gap = np.full((panels[0].shape[0], 5, 3), 15, np.uint8)
    row = [panels[0]]
    for p in panels[1:]:
        row += [gap, p]
    return np.hstack(row)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    if not os.path.isdir(out):
        os.makedirs(out)
    cap, top, rise = nuclear_dims(150.0)
    a = run("150 kt groundburst", cap, top, rise, out)
    cap, top, rise = conventional_dims(17.2)      # a 1.5 t charge
    b = run("1.5 t groundburst", cap, top, rise, out)
    gap = np.full((5, a.shape[1], 3), 15, np.uint8)
    write_png(os.path.join(out, "base-surge.png"), np.vstack([a, gap, b]))
    print("\nwrote " + os.path.join(out, "base-surge.png"))


if __name__ == "__main__":
    main()
