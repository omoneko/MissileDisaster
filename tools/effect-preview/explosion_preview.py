"""Renders and MEASURES a non-nuclear detonation, so its size can be read off in metres.

The fireball is the one part of the effect judged against the buildings around it rather than
against anything else in the effect, and it has now been reported as too big once and too small
once. Neither report can be settled by reading the constants: Unity's startSize is a diameter,
the particles are scattered over a sphere, and a size curve grows them, so what a player sees is
several numbers away from the one in the source. This draws the particles the way the game
composites them and reports the width of the bright ball in metres.

Everything here is read straight out of Game/Effects/ExplosionFallback.cs and
Core/ExplosionScale.cs. If a constant changes in C#, change it here.

Usage:  python tools/effect-preview/explosion_preview.py <out-folder>
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fxlib as fx

# ------------------------------------------------------- Core/ExplosionScale.cs + WarheadSpec.cs

CONVENTIONAL_FIREBALL = 15.0      # metres, at the 1 t reference charge
CONVENTIONAL_DESTRUCTION = 72.0
THERMOBARIC_FIREBALL = 40.0
DRAWN_FIREBALL_FACTOR = 1.45      # ExplosionScale.DrawnFireballFactor

# ------------------------------------------------------- Game/Effects/ExplosionFallback.cs

SPRITE_FRACTION = 1.1
FIRE_SECONDS = 1.3
SMOKE_SECONDS = 3.0
FIRE_DRIFT_PER_LIFE = 0.48
SMOKE_DRIFT_PER_LIFE = 1.44
FIRE_BURST, SMOKE_BURST = 60, 40
FIRE_A = (1.0, 0.8, 0.35)
FIRE_B = (1.0, 0.4, 0.08)
SMOKE = (0.12, 0.11, 0.1)
SMOKE_ALPHA = 0.6


def cbrt(x):
    return x ** (1.0 / 3.0)


def burst(radius, t, kind, origin=(0, 0, 0)):
    """One CreateBurst call at time t. Returns a draw batch, or None once it is over."""
    size = min(max(radius, 4.0), 750.0) * SPRITE_FRACTION
    if kind == "fire":
        start, life, n = size, FIRE_SECONDS, FIRE_BURST
        speed = size * FIRE_DRIFT_PER_LIFE / FIRE_SECONDS
        size_from, size_to, additive = 1.0, 1.6, True
    else:
        start, life, n = size * 1.2, SMOKE_SECONDS, SMOKE_BURST
        speed = size * 1.2 * SMOKE_DRIFT_PER_LIFE / SMOKE_SECONDS
        size_from, size_to, additive = 0.7, 2.2, False
    if t < 0.0 or t > life:
        return None

    u = t / life
    p, v = fx.in_sphere(n, start * 0.3)          # ParticleBuilder-free: shape.radius = size*0.3
    p = p + v * (speed * t)
    # colorOverLifetime: white throughout, alpha 1 -> 0.8 at 0.4 -> 0
    alpha = fx.ramp([(0.0, 1.0), (0.4, 0.8), (1.0, 0.0)], u)
    if kind == "fire":
        cols = np.array([fx.mix(FIRE_A, FIRE_B, x) for x in fx.rng.random(n)])
        rgba = np.concatenate([cols, np.full((n, 1), alpha)], axis=1)
    else:
        rgba = np.tile(np.array(SMOKE + (alpha * SMOKE_ALPHA,)), (n, 1))
    diameter = start * fx.ramp([(0.0, size_from), (1.0, size_to)], u)
    return p + np.array(origin), np.full(n, diameter), rgba, additive


def measure(radius, t, kind="fire", samples=9):
    """The bright ball's width in metres: the extent over which it actually covers the sky.

    Rendered head-on against black with an orthographic-equivalent camera, thresholded, and
    converted back to metres. Averaged over several particle draws, because the emitter is
    random and one draw is not the effect.
    """
    widths = []
    for _ in range(samples):
        w = h = 480
        span = radius * 6.0                       # metres across the frame
        dist = 4000.0
        focal = (h * 0.5) / math.tan(math.radians(38.0) * 0.5)
        # Put the camera far enough back that span fills the frame at this focal length.
        dist = span * focal / w
        cam = fx.Camera((0, 0, -dist), (0, 0, 0), w, h, 38.0)
        img = np.zeros((h, w, 3))
        b = burst(radius, t, kind)
        if b is None:
            widths.append(0.0)
            continue
        fx.draw(img, cam, [b])
        lum = img.max(axis=2)
        cols = np.where(lum.max(axis=0) > 0.12)[0]   # visible against a black sky
        if len(cols) == 0:
            widths.append(0.0)
            continue
        widths.append((cols[-1] - cols[0] + 1) / w * span)
    return float(np.mean(widths))


def scene(radius, t, path, w=900, h=620):
    """The explosion over a city, as a scale reference. The buildings are the ruler."""
    ground = int(h * 0.72)
    img = fx.sky(w, h, ground)
    look = radius * 0.9
    dist = max(radius * 9.0, 260.0)
    cam = fx.Camera((0, radius * 1.1, -dist), (0, look, 0), w, h, 38.0)
    fx.city(img, cam, reach=max(radius * 7.0, 320.0), seed=5)
    batches = [b for b in (burst(radius, t, "smoke"), burst(radius, t, "fire")) if b is not None]
    if batches:
        fx.draw(img, cam, batches)
    write_png(path, (np.clip(img, 0, 1) * 255).astype(np.uint8))


def write_png(path, pixels):
    import struct, zlib
    h, w, _ = pixels.shape
    raw = b"".join(b"\x00" + pixels[r].tobytes() for r in range(h))

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "tools/effect-preview/out"
    os.makedirs(out, exist_ok=True)

    print("charge      physical  drawn   real     bright ball     vs real   vs old disc")
    for kg in (250, 1000, 1500, 10000):
        mult = cbrt(kg / 1000.0)
        physical = CONVENTIONAL_FIREBALL * mult
        drawn = physical * DRAWN_FIREBALL_FACTOR
        real_d = 3.5 * cbrt(kg)                       # D = 3.5 W^(1/3), the usual HE approximation
        # The widest the flame gets while it is still bright, sampled across the held part of
        # the alpha curve rather than at one instant.
        ball = max(measure(drawn, t) for t in (0.05, 0.2, 0.4, 0.6))
        old_disc = CONVENTIONAL_DESTRUCTION * mult * 0.5 * 2.0   # the old spawn disc, as a width
        print(f"{kg:6d} kg   {physical:6.1f} m {drawn:6.1f} m {real_d / 2:6.1f} m "
              f"{ball:8.0f} m across  {ball / real_d:6.2f}x  {ball / old_disc:6.2f}x")
        if kg == 1000:
            for label, t in (("flash", 0.05), ("burning", 0.35), ("dying", 0.9)):
                scene(drawn, t, os.path.join(out, f"explosion-1t-{label}.png"))

    thermo = THERMOBARIC_FIREBALL * DRAWN_FIREBALL_FACTOR
    print(f"thermobaric  {THERMOBARIC_FIREBALL:6.1f} m {thermo:6.1f} m         "
          f"{max(measure(thermo, t) for t in (0.05, 0.2, 0.4)):8.0f} m across")
    scene(thermo, 0.3, os.path.join(out, "explosion-thermobaric.png"))


if __name__ == "__main__":
    main()
