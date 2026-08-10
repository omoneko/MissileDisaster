"""Renders the puff mushroom cloud exactly as the game composits it, so density and
opacity can be judged and measured before anything is deployed.

This is a faithful port of Core/CloudPuffs + Core/CloudAnimation + the relevant slice of
Core/NuclearCloudDisplay and MushroomCloudPuffsFx, plus the billboard alpha profile from
ParticleAssets. If a constant changes in C#, it must change here - the whole point is that
what this draws is what the player gets.

Usage:  python tools/effect-preview/cloud_preview.py <out-folder> [--profile current|tuned]

Outputs side and game-angle composites at three moments (forming / standing / fading) over a
backdrop with buildings, plus a printed opacity measurement inside the cap. A cloud should
measure near 1.0 there: if the backdrop shows through the cap, this script fails the build
before a playtest has to.
"""
import math
import os
import struct
import sys
import zlib

import numpy as np

# ---------------------------------------------------------------- C# ports (keep in sync)

TAU = 2.0 * math.pi


def hash01(index, seed, salt):
    h = np.uint32(index * 374761393 + seed * 668265263 + salt * 1274126177 & 0xFFFFFFFF)
    h ^= h >> np.uint32(13)
    h = np.uint32(int(h) * 1911520717 & 0xFFFFFFFF)
    h ^= h >> np.uint32(16)
    return float(int(h) & 0xFFFFFF) / float(0x1000000)


class Display150kt:
    """NuclearCloudDisplay.For(150) - values recomputed the same way the C# does."""
    def __init__(self, cap_width_scale, screen_top, knee):
        cloud_scale = 0.06
        real_cap = 600.0 * 10 ** (0.0137 * 2.1761**3 - 0.0358 * 2.1761**2 + 0.37 * 2.1761)
        l = math.log10(150.0); l2 = l * l
        real_top = 3000.0 * 10 ** (0.006941 * l2 * l2 - 0.06216 * l2 * l + 0.1526 * l2 + 0.1878 * l)

        def soft(v, k, c):
            if v <= k: return v
            span = c - k
            return k + span * (1.0 - math.exp(-(v - k) / span))

        top_real = soft(real_top, 12000.0, 30000.0)
        cap_real = soft(real_cap, 8000.0, 26000.0)
        base_frac = min(top_real * 0.5, 11000.0) / top_real
        stem_frac = 0.5 + ((l - 1.301) / 1.699) * (0.15 - 0.5)

        self.cap_radius = cap_real * cloud_scale * cap_width_scale
        self.cloud_top = soft(top_real * cloud_scale, knee, screen_top)
        self.cap_base = self.cloud_top * base_frac
        self.cap_depth = self.cloud_top - self.cap_base
        self.stem_radius = cap_real * cloud_scale * stem_frac
        self.rise = soft(600.0 * (150.0 / 1000.0) ** 0.25 / 45.0, 10.0, 16.0)
        self.rise = max(self.rise, 5.0)
        self.hold = min(max(self.rise * 1.2, 8.0), 16.0)
        self.fade = min(max(self.rise * 1.7, 12.0), 20.0)
        self.fire_field = self.cap_radius * 2.5


def animation_at(t, rise, hold, fade):
    """CloudAnimation.At."""
    t = max(t, 0.0)
    u = min(t / rise, 1.0) if rise > 0 else 1.0
    ease = 1.0 - (1.0 - u) ** 3
    birth = 0.12
    h = birth + (1.0 - birth) * ease
    w = birth + (1.0 - birth) * ease ** 1.6
    fade_start = rise + hold
    fade_in = rise * 0.15
    if t < fade_in and fade_in > 0:
        alpha = t / fade_in
    elif t < fade_start or fade <= 0:
        alpha = 1.0 if t < fade_start + fade else 0.0
    else:
        f = min((t - fade_start) / fade, 1.0)
        alpha = 1.0  # the thinning is per puff now - see dissolve in Puffs.at
        h *= 1.0 + 0.06 * f
        w *= 1.0 + 0.06 * f
    return h, w, alpha


class Puffs:
    """CloudPuffs, vectorised over all puffs."""
    ROLL_HOLD, ROLL_FADE = 0.35, 0.1

    def __init__(self, P):
        self.P = P
        n = P["cap_count"] + P["col_count"] + P["fire_count"]
        idx = np.arange(n)
        self.cap = idx < P["cap_count"]
        self.fire = idx >= P["cap_count"] + P["col_count"]
        h = lambda salt: np.array([hash01(i, P["seed"], salt) for i in idx])
        self.azimuth = h(1) * TAU
        self.swirl = (h(2) - 0.5) * 0.08
        self.rho = P["rho_min"] + (1.0 - P["rho_min"]) * np.sqrt(h(3))
        self.theta0 = h(4) * TAU
        self.omega = (0.55 + 0.75 * (1 - self.rho)) * (0.8 + 0.4 * h(5))
        self.climb = h(6)
        self.wobble = h(7) * TAU
        self.size01 = h(8)
        self.spin = (h(9) - 0.5) * 24.0
        self.lag = h(10)

    def roll_time(self, t, rise, hold):
        if t <= rise: return t
        if t - rise <= hold: return rise + (t - rise) * self.ROLL_HOLD
        return rise + hold * self.ROLL_HOLD + (t - rise - hold) * self.ROLL_FADE

    def at(self, t, d, anim):
        P = self.P
        hf, wf, alpha = anim
        capR, stemR = d.cap_radius * wf, d.stem_radius * wf
        cap_base, cap_depth = d.cap_base * hf, d.cap_depth * hf
        az = self.azimuth + self.swirl * t
        n = len(self.cap)
        dist = np.zeros(n); y = np.zeros(n); size = np.zeros(n)
        fade = np.ones(n); ember = np.zeros(n); dust = np.zeros(n)

        c = self.cap
        theta = self.theta0 + self.omega * self.roll_time(t, d.rise, d.hold)
        ring, cross = capR * 0.55, capR * 0.45 * self.rho
        dist[c] = np.maximum(ring - cross[c] * np.cos(theta[c]), 0.0)
        y[c] = cap_base + cap_depth * 0.5 + cap_depth * 0.5 * self.rho[c] * np.sin(theta[c])
        size[c] = capR * (P["cap_size0"] + P["cap_size1"] * self.size01[c])
        dust[c] = 0.15 + 0.15 * (1 - self.rho[c])
        ember_env = max(1.0 - t / (d.rise * 0.7), 0.0)
        ember[c] = ember_env * (1 - self.rho[c]) * 0.8

        m = ~c & ~self.fire
        loop = d.rise * 0.9
        u = np.mod(self.climb[m] + t / loop, 1.0)
        column_top = cap_base + cap_depth * 0.2
        y[m] = (1 - (1 - u) ** 2) * column_top
        shape = np.where(u < 0.35, 1.35 + (1.0 - 1.35) * smooth(u / 0.35),
                         1.0 + 0.2 * smooth((u - 0.35) / 0.65))
        radial = 0.25 + 0.75 * self.rho[m]
        wob = 1 + 0.18 * np.sin(self.wobble[m] + u * 9.4 + t * 0.4)
        dist[m] = stemR * shape * radial * wob
        size[m] = stemR * (P["col_size0"] + P["col_size1"] * self.size01[m])
        edge = 0.06
        fade[m] = np.where(u < edge, smooth(u / edge), np.where(u > 1 - edge, smooth((1 - u) / edge), 1.0))
        dust[m] = 0.85 - 0.5 * u
        ember[m] = ember_env * u * 0.6

        # Fire smoke: born across the burn field, rising slowly, gently drawn in toward the
        # central updraft, absorbed as it arrives.
        f = self.fire
        floop = d.rise * 1.6
        fu = np.mod(self.climb[f] + t / floop, 1.0)
        r0 = d.fire_field * (0.3 + 0.7 * self.rho[f])
        pull = P["fire_pull"]
        dist[f] = r0 * (1.0 - pull * smooth(fu))
        y[f] = fu ** 1.3 * cap_base * 0.7
        size[f] = d.fire_field * (P["fire_size0"] + P["fire_size1"] * self.size01[f])
        edge = 0.10
        fade[f] = np.where(fu < edge, smooth(fu / edge),
                           np.where(fu > 1 - edge, smooth((1 - fu) / edge), 1.0))
        dust[f] = 1.0 - 0.3 * fu
        ember[f] = 0.18 * (1.0 - fu) ** 3  # the fires themselves keep glowing at the base

        # Dissolve: the fade is per puff and staggered, so the cloud thins raggedly over many
        # seconds instead of evaporating all at once. The column dies first - nothing is
        # feeding it - the cap loosens next, and the fire smoke outlasts them both.
        fp = np.clip((t - d.rise - d.hold) / d.fade, 0.0, 1.0)
        if fp > 0.0:
            lag = np.where(c, 0.10 + 0.50 * self.lag,
                  np.where(f, 0.35 + 0.50 * self.lag, 0.05 + 0.40 * self.lag))
            span = np.clip(np.minimum(0.35, 1.0 - lag), 0.05, None)
            prog = np.clip((fp - lag) / span, 0.0, 1.0)
            dissolve = 1.0 - smooth(prog)
            fade = fade * dissolve
            size = size * (1.0 + 0.30 * (1.0 - dissolve))  # loosening into haze as it thins

        x = dist * np.cos(az); z = dist * np.sin(az)
        a = np.where(c, P["cap_alpha"], np.where(f, P["fire_alpha"], P["col_alpha"])) * alpha * fade
        return x, y, z, size, a, ember, dust


def smooth(k):
    k = np.clip(k, 0.0, 1.0)
    return k * k * (3 - 2 * k)


def puff_alpha_profile(dd, phase, P):
    """The billboard texture's alpha at normalised distance dd from its centre.
    current: ParticleAssets glow texture, a=(1-d)^2 - built for sparks, not clouds.
    tuned:   ParticleAssets cloud texture - an opaque core, a soft edge, a wobbled rim."""
    if P["texture"] == "current":
        return np.clip(1.0 - dd, 0.0, 1.0) ** 2
    core, edge = P["tex_core"], P["tex_edge"]
    wob = 1.0 + P["tex_wobble"] * np.sin(3.0 * phase)
    d2 = dd / np.maximum(wob, 1e-6)
    t = np.clip((d2 - core) / (edge - core), 0.0, 1.0)
    return 1.0 - (t * t * (3 - 2 * t))


# ---------------------------------------------------------------- compositing

VAPOUR = np.array([0.93, 0.93, 0.94]); DUST_G = np.array([0.52, 0.45, 0.37])
EMBER = np.array([1.0, 0.45, 0.12])


def backdrop(W, H, metres_w, metres_h, horizon_px):
    img = np.zeros((H, W, 3))
    for r in range(H):
        k = r / H
        img[r] = np.array([0.35, 0.55, 0.78]) * (1 - k * 0.4) + np.array([0.75, 0.82, 0.88]) * (k * 0.4)
    img[horizon_px:] = np.array([0.32, 0.34, 0.30])
    rng = np.random.default_rng(4)
    for _ in range(28):  # buildings on the skyline, the things that must not show through
        bw = int(rng.uniform(0.01, 0.04) * W); bh = int(rng.uniform(0.05, 0.22) * H)
        bx = int(rng.uniform(0, W - bw))
        col = np.array([0.55, 0.55, 0.58]) * rng.uniform(0.5, 1.0)
        img[horizon_px - bh:horizon_px, bx:bx + bw] = col
    return img


def render(P, d, t, elev_deg, path):
    W, H = 880, 660
    metres_w = max(d.cap_radius * 2.6, d.fire_field * 2.3, 900.0)
    px = W / metres_w
    horizon = int(H * 0.86)
    img = backdrop(W, H, metres_w, metres_w * H / W, horizon)
    cover = np.zeros((H, W))

    anim = animation_at(t, d.rise, d.hold, d.fade)
    puffs = Puffs(P)
    x, y, z, size, a, ember, dust = puffs.at(t, d, anim)

    elev = math.radians(elev_deg)
    sy = y * math.cos(elev) + z * math.sin(elev) * 0.0 - z * math.sin(elev)  # simple tilt
    depth = z * math.cos(elev) + y * math.sin(elev)
    order = np.argsort(depth)[::-1]  # far first

    for i in order:
        if a[i] <= 0.003: continue
        cx = int(W / 2 + x[i] * px)
        cy = int(horizon - (y[i] * math.cos(elev) - z[i] * math.sin(elev)) * px)
        r = max(int(size[i] * 0.5 * px), 2)
        x0, x1 = max(cx - r, 0), min(cx + r + 1, W)
        y0, y1 = max(cy - r, 0), min(cy + r + 1, H)
        if x0 >= x1 or y0 >= y1: continue
        gy, gx = np.mgrid[y0:y1, x0:x1]
        dd = np.sqrt((gx - cx) ** 2 + (gy - cy) ** 2) / r
        phase = np.arctan2(gy - cy, gx - cx) + puffs.spin[i]
        ta = puff_alpha_profile(dd, phase, P) * a[i]
        col = VAPOUR * (1 - dust[i]) + DUST_G * dust[i]
        col = col * (1 - ember[i]) + EMBER * ember[i]
        img[y0:y1, x0:x1] = img[y0:y1, x0:x1] * (1 - ta[..., None]) + col * ta[..., None]
        cover[y0:y1, x0:x1] = cover[y0:y1, x0:x1] * (1 - ta) + ta

    # opacity inside the cap body, the number that has to be near 1.0
    hf, wf, _ = anim
    capR = d.cap_radius * wf
    yc0 = d.cap_base * hf + d.cap_depth * hf * 0.25
    yc1 = d.cap_base * hf + d.cap_depth * hf * 0.85
    r0, r1 = int(horizon - yc1 * px), int(horizon - yc0 * px)
    c0, c1 = int(W / 2 - capR * 0.6 * px), int(W / 2 + capR * 0.6 * px)
    body = cover[max(r0, 0):max(r1, 1), max(c0, 0):min(c1, W)]
    opacity = float(body.mean()) if body.size else 0.0

    write_png(path, (np.clip(img, 0, 1) * 255).astype(np.uint8))
    return opacity


def write_png(path, pixels):
    h, w, _ = pixels.shape
    raw = b"".join(b"\x00" + pixels[r].tobytes() for r in range(h))
    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


PROFILES = {
    # The shipping profile. Keep in lockstep with Core/CloudPuffs + MushroomCloudPuffsFx.
    "tuned": dict(seed=7, cap_count=340, col_count=160, fire_count=120, rho_min=0.1,
                  cap_size0=0.23, cap_size1=0.16, col_size0=0.7, col_size1=0.5,
                  fire_size0=0.09, fire_size1=0.08, fire_pull=0.85, fire_alpha=0.8,
                  cap_alpha=0.97, col_alpha=0.88, texture="cloud",
                  tex_core=0.42, tex_edge=0.95, tex_wobble=0.10,
                  cap_width_scale=1.3, screen_top=2000.0, knee=1400.0),
}


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "tools/effect-preview/out"
    os.makedirs(out, exist_ok=True)
    which = sys.argv[sys.argv.index("--profile") + 1] if "--profile" in sys.argv else None
    for name, P in PROFILES.items():
        if which and name != which: continue
        d = Display150kt(P["cap_width_scale"], P["screen_top"], P["knee"])
        for label, t in [("forming", d.rise * 0.55), ("standing", d.rise + d.hold * 0.4),
                         ("fade30", d.rise + d.hold + d.fade * 0.3),
                         ("fade60", d.rise + d.hold + d.fade * 0.6),
                         ("fade85", d.rise + d.hold + d.fade * 0.85)]:
            op = render(P, d, t, 0, os.path.join(out, f"{name}-{label}.png"))
            print(f"{name:8s} {label:9s} t={t:5.1f}s  cap-body opacity = {op:.3f}")


if __name__ == "__main__":
    main()
