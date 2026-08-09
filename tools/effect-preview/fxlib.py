"""
A software billboard renderer that reproduces MissileDisaster's nuclear particle systems.

Every emitter shape, start size, speed, lifetime, colour gradient and size curve is read
straight out of Game/Effects/NuclearMushroomFx.cs, and the sprite falloff (clamp01(1-d)^2) and
the two blend modes (additive for fire, alpha for smoke) out of Game/Effects/ParticleAssets.cs,
so what comes out is the shape the game draws rather than an illustration of it.
"""
import math
import numpy as np

rng = np.random.default_rng(20260809)

# ---------------------------------------------------------------- Core/NuclearCloud.cs

def fireball_radius(kt): return 55.0 * kt ** 0.4
def fireball_seconds(kt): return 10.0 * (kt / 1000.0) ** 0.4

def cloud_radius(kt):
    L = math.log10(kt)
    return 600.0 * 10 ** (0.0137 * L**3 - 0.0358 * L**2 + 0.37 * L)

def cloud_top(kt):
    L = math.log10(kt)
    return 3000.0 * 10 ** (0.006941 * L**4 - 0.06216 * L**3 + 0.1526 * L**2 + 0.1878 * L)

def stem_fraction(kt):
    L = math.log10(kt)
    t = (L - 1.301) / (3.0 - 1.301)
    return max(0.1, min(0.5, 0.5 + t * (0.15 - 0.5)))

def stabilise_seconds(kt): return 600.0 * (kt / 1000.0) ** 0.25

# ------------------------------------------------------- Core/EffectCeiling.cs (new)

def soft(v, knee, ceil):
    if v <= knee: return v
    if ceil <= knee: return knee
    span = ceil - knee
    return knee + span * (1.0 - math.exp(-(v - knee) / span))

def soft_floor(v, floor, knee, ceil):
    return floor if v < floor else soft(v, knee, ceil)

def hard(v, lo, hi): return max(lo, min(hi, v))

# ------------------------------------------------- Core/NuclearCloudDisplay.cs (new)

NEW = dict(fb=(25.0, 3000.0, 7000.0), fbs=(0.8, 12.0, 20.0), cap=(200.0, 8000.0, 26000.0),
           top=(800.0, 12000.0, 30000.0), rise=(8.0, 26.0, 40.0))
# the hard clamps that were in NuclearMushroomFx.cs before this change
OLD = dict(fb=(25.0, 3000.0), fbs=(0.8, 12.0), cap=(200.0, 8000.0),
           top=(800.0, 12000.0), rise=(8.0, 26.0))


def dimensions(kt, ceilings="new"):
    """The dimensions the effect is drawn at, before or after the ceiling change."""
    if ceilings == "new":
        d = dict(
            fireball=soft_floor(fireball_radius(kt), *NEW["fb"]),
            fireball_t=soft_floor(fireball_seconds(kt), *NEW["fbs"]),
            cap=soft_floor(cloud_radius(kt), *NEW["cap"]),
            top=soft_floor(cloud_top(kt), *NEW["top"]),
            rise=soft_floor(stabilise_seconds(kt) / 25.0, *NEW["rise"]),
        )
    else:
        d = dict(
            fireball=hard(fireball_radius(kt), *OLD["fb"]),
            fireball_t=hard(fireball_seconds(kt), *OLD["fbs"]),
            cap=hard(cloud_radius(kt), *OLD["cap"]),
            top=hard(cloud_top(kt), *OLD["top"]),
            rise=hard(stabilise_seconds(kt) / 25.0, *OLD["rise"]),
        )
    d["stem"] = d["cap"] * stem_fraction(kt)
    return d

# ------------------------------------------------------------------- colours

FIREBALL_CORE = (1.00, 0.99, 0.94)
FIREBALL_MID = (1.00, 0.82, 0.35)
FIREBALL_EDGE = (1.00, 0.42, 0.10)
FIREBALL_COOL = (0.42, 0.13, 0.05)
CONDENSATION = (0.96, 0.97, 1.00, 0.42)
DUST_LIGHT = (0.55, 0.49, 0.40, 0.75)
DUST_DARK = (0.32, 0.28, 0.23, 0.75)
CAP_WARM = (0.40, 0.31, 0.24, 0.72)
CAP_COOL = (0.24, 0.23, 0.22, 0.72)


def ramp(keys, u):
    """Piecewise-linear key interpolation, the way a Unity Gradient reads."""
    xs = [k[0] for k in keys]
    if u <= xs[0]: return keys[0][1]
    if u >= xs[-1]: return keys[-1][1]
    for i in range(1, len(keys)):
        if u <= xs[i]:
            t = (u - xs[i-1]) / (xs[i] - xs[i-1])
            a, b = keys[i-1][1], keys[i][1]
            if isinstance(a, tuple):
                return tuple(a[j] + t * (b[j] - a[j]) for j in range(len(a)))
            return a + t * (b - a)
    return keys[-1][1]


def mix(a, b, t):
    return tuple(a[i] + t * (b[i] - a[i]) for i in range(len(a)))

# ------------------------------------------------------------------- emitters


def in_sphere(n, radius):
    v = rng.normal(size=(n, 3))
    v /= np.linalg.norm(v, axis=1, keepdims=True)
    r = radius * rng.random(n) ** (1 / 3)
    p = v * r[:, None]
    return p, v  # Unity's sphere shape sends each particle out along its own radius


def in_hemisphere(n, radius):
    p, v = in_sphere(n, radius)
    p[:, 1] = np.abs(p[:, 1]); v[:, 1] = np.abs(v[:, 1])
    return p, v


def in_cone(n, radius, angle_deg):
    """Unity's cone, axis turned up: a point on the base disc, flying out along the cone."""
    th = rng.random(n) * 2 * math.pi
    rr = radius * np.sqrt(rng.random(n))
    p = np.stack([rr * np.cos(th), np.zeros(n), rr * np.sin(th)], axis=1)
    radial = np.stack([np.cos(th), np.zeros(n), np.sin(th)], axis=1)
    spread = math.tan(math.radians(angle_deg)) * (rr / max(radius, 1e-6))[:, None]
    v = np.array([0.0, 1.0, 0.0]) + radial * spread
    v /= np.linalg.norm(v, axis=1, keepdims=True)
    return p, v


def travel(v0, t, limit=None, tau=0.4):
    """Distance covered at start speed v0, with limitVelocityOverLifetime braking to `limit`."""
    if limit is None or v0 <= limit:
        return v0 * t
    return limit * t + (v0 - limit) * tau * (1.0 - math.exp(-t / tau))

# --------------------------------------------------------------------- stages
# Each returns (positions Nx3, diameters N, rgba Nx4, additive?) at wall-clock time `t`.


def stage_fireball(d, t, origin=(0, 0, 0), n=90):
    R, T = d["fireball"], d["fireball_t"]
    life = T * 1.7
    if t < 0 or t > life: return None
    u = t / life
    p, v = in_sphere(n, R * 0.28)
    dist = travel(R * 0.05, t, limit=R * 0.05, tau=0.3)
    p = p + v * dist
    p[:, 1] += R * 0.12 * t                              # Rise
    col = ramp([(0.0, FIREBALL_CORE), (0.25, FIREBALL_MID),
                (0.6, FIREBALL_EDGE), (1.0, FIREBALL_COOL)], u)
    alpha = ramp([(0.0, 1.0), (0.55, 1.0), (0.8, 0.55), (1.0, 0.0)], u)
    start = np.array([mix(FIREBALL_CORE, FIREBALL_MID, x) for x in rng.random(n)])
    rgba = np.concatenate([start * np.array(col), np.full((n, 1), alpha)], axis=1)
    size = R * 0.55 * ramp([(0.0, 0.35), (1.0, 2.0)], u)
    return p + np.array(origin), np.full(n, size), rgba, True


def stage_condensation(d, t, origin=(0, 0, 0), n=70):
    R = d["fireball"] * 2.6
    delay = d["fireball_t"] * 0.3
    life = 1.3
    if t < delay or t > delay + life: return None
    u = (t - delay) / life
    p, v = in_hemisphere(n, R * 0.7)
    p = p + v * travel(R * 0.35, t - delay)
    alpha = CONDENSATION[3] * ramp([(0.0, 0.0), (0.2, 1.0), (0.6, 0.6), (1.0, 0.0)], u)
    rgba = np.tile(np.array(CONDENSATION[:3] + (alpha,)), (n, 1))
    size = R * 0.5 * ramp([(0.0, 0.8), (1.0, 1.5)], u)
    return p + np.array(origin), np.full(n, size), rgba, False


def _stream(rate, duration, t, life):
    """Birth times of a steady stream still alive at t, and their ages."""
    n = max(1, int(rate * min(t, duration)))
    born = rng.random(n) * min(t, duration)
    age = t - born
    keep = (age >= 0) & (age <= life)
    return born[keep], age[keep]


def stage_ground_dust(d, t, origin=(0, 0, 0)):
    stemR, rise = d["stem"], d["rise"]
    life = rise * 0.55
    born, age = _stream(45.0, rise * 0.4, t, life)
    n = len(age)
    if n == 0: return None
    p, v = in_cone(n, stemR * 1.7, 22.0)
    step = np.array([travel(stemR * 0.06, a) for a in age])
    p = p + v * step[:, None]
    p[:, 1] += stemR * 0.25 * age                        # Rise
    u = age / life
    alpha = np.array([ramp([(0.0, 0.7), (0.4, 0.8), (1.0, 0.0)], x) for x in u]) * 0.75
    base = np.array([mix(DUST_LIGHT[:3], DUST_DARK[:3], x) for x in rng.random(n)])
    rgba = np.concatenate([base, alpha[:, None]], axis=1)
    size = stemR * 0.5 * np.array([ramp([(0.0, 0.6), (1.0, 1.9)], x) for x in u])
    return p + np.array(origin), size, rgba, False


def stage_stem(d, t, origin=(0, 0, 0)):
    stemR, top, rise = d["stem"], d["top"], d["rise"]
    life = rise + 8.0
    born, age = _stream(40.0, rise, t, life)
    n = len(age)
    if n == 0: return None
    p, v = in_sphere(n, stemR)
    step = np.array([travel(stemR * 0.02, a) for a in age])
    p = p + v * step[:, None]
    p[:, 1] += (top / rise) * age                        # Rise: it climbs the whole column
    u = age / life
    alpha = np.array([ramp([(0.0, 0.6), (0.25, 0.85), (0.7, 0.7), (1.0, 0.0)], x) for x in u])
    base = np.array([mix(DUST_DARK[:3], CAP_COOL[:3], x) for x in rng.random(n)])
    rgba = np.concatenate([base, (alpha * 0.75)[:, None]], axis=1)
    size = stemR * 1.1 * np.array([ramp([(0.0, 0.8), (1.0, 1.6)], x) for x in u])
    return p + np.array(origin), size, rgba, False


EMERGE = 0.55   # CapEmergeFraction


def _climb(age, climb_seconds, lifetime, rate):
    """The cap's ClimbThenSettle curve, integrated: full rate, then eased to nothing."""
    hold = min(max(climb_seconds / lifetime, 0.02), 0.9) * lifetime
    settled = min(hold * 1.15, 0.99 * lifetime)
    if age <= hold: return rate * age
    if age >= settled: return rate * (hold + 0.5 * (settled - hold))
    x = (age - hold) / (settled - hold)
    return rate * (hold + (settled - hold) * (x - 0.5 * x * x))


def stage_cap_legacy(d, t, origin=(0, 0, 0), n=100):
    """The cap as it was: emitted at the cloud top, finished, before the stem gets there."""
    capR, rise, top = d["cap"], d["rise"], d["top"]
    lifetime = max(18.0, rise * 0.8)
    delay = rise * EMERGE
    emit_f, size_f, growth = 0.35, 0.45, 1.6
    drift = max(0.0, capR * (1 - emit_f - size_f * growth * 0.5)) / lifetime
    if t < delay or t > delay + lifetime: return None
    age = t - delay
    u = age / lifetime
    p, v = in_cone(n, capR * emit_f, 62.0)
    p = p + v * travel(drift * 2.5, age, limit=drift, tau=0.6)
    p[:, 1] += top - 0.5 * 9.81 * 0.015 * age ** 2
    alpha = ramp([(0.0, 0.6), (0.25, 0.85), (0.7, 0.7), (1.0, 0.0)], u) * 0.72
    base = np.array([mix(CAP_WARM[:3], CAP_COOL[:3], x) for x in rng.random(n)])
    rgba = np.concatenate([base, np.full((n, 1), alpha)], axis=1)
    size = capR * size_f * ramp([(0.0, 0.7), (1.0, growth)], u)
    return p + np.array(origin), np.full(n, size), rgba, False


def stage_cap(d, t, origin=(0, 0, 0), n=100):
    capR, rise, top = d["cap"], d["rise"], d["top"]
    lifetime = max(18.0, rise * 0.8)
    delay = rise * EMERGE
    emit_f, size_f, growth = 0.35, 0.45, 1.6
    drift = max(0.0, capR * (1 - emit_f - size_f * growth * 0.5)) / lifetime
    if t < delay or t > delay + lifetime: return None
    age = t - delay
    u = age / lifetime
    p, v = in_cone(n, capR * emit_f, 62.0)
    p = p + v * (drift * age)          # a steady drift, covering driftDistance over the lifetime
    # born at the head of the column, riding the rest of the way up with it
    p[:, 1] += top * EMERGE + _climb(age, rise - delay, lifetime, top / rise)
    p[:, 1] -= 0.5 * 9.81 * 0.015 * age ** 2             # the rim droops
    alpha = ramp([(0.0, 0.6), (0.25, 0.85), (0.7, 0.7), (1.0, 0.0)], u) * 0.72
    base = np.array([mix(CAP_WARM[:3], CAP_COOL[:3], x) for x in rng.random(n)])
    rgba = np.concatenate([base, np.full((n, 1), alpha)], axis=1)
    size = capR * size_f * ramp([(0.0, 0.7), (1.0, growth)], u)
    return p + np.array(origin), np.full(n, size), rgba, False


STAGES = [
    ("1. Fireball", stage_fireball),
    ("2. Condensation cloud", stage_condensation),
    ("3. Ground dust", stage_ground_dust),
    ("4. Stem", stage_stem),
    ("5. Cap", stage_cap),
]

# -------------------------------------------------------------------- camera


class Camera:
    def __init__(self, eye, target, w, h, fov=38.0):
        self.eye = np.array(eye, dtype=float)
        f = np.array(target, dtype=float) - self.eye
        f /= np.linalg.norm(f)
        r = np.cross(f, [0, 1, 0]); r /= np.linalg.norm(r)
        u = np.cross(r, f)
        self.basis = np.stack([r, u, f])
        self.w, self.h = w, h
        self.focal = (h * 0.5) / math.tan(math.radians(fov) * 0.5)

    def project(self, pts):
        rel = (pts - self.eye) @ self.basis.T          # x right, y up, z forward
        z = np.maximum(rel[:, 2], 1e-3)
        x = self.w * 0.5 + rel[:, 0] * self.focal / z
        y = self.h * 0.5 - rel[:, 1] * self.focal / z
        return x, y, z, rel[:, 2]


def sky(w, h, ground_y):
    img = np.zeros((h, w, 3))
    t = np.linspace(0, 1, h)[:, None]
    top, bottom = np.array([0.16, 0.24, 0.38]), np.array([0.62, 0.70, 0.80])
    img[:] = (top * (1 - t) + bottom * t)[:, None, :]
    if ground_y < h:
        g = np.linspace(0, 1, max(1, h - ground_y))[:, None]
        near, far = np.array([0.20, 0.21, 0.19]), np.array([0.44, 0.45, 0.41])
        img[ground_y:] = (far * (1 - g) + near * g)[:, None, :]
    return img


def draw(img, cam, batches, horizon_px=None):
    """Composite every particle back to front, additive for fire and alpha for smoke."""
    items = []
    for pos, size, rgba, additive in batches:
        x, y, z, fwd = cam.project(pos)
        r = size * 0.5 * cam.focal / z
        for i in range(len(x)):
            if fwd[i] <= 1.0 or r[i] < 0.4 or rgba[i, 3] <= 0.004: continue
            if x[i] < -r[i] or x[i] > cam.w + r[i] or y[i] < -r[i] or y[i] > cam.h + r[i]: continue
            if r[i] > 60000: continue   # a sprite this large is behind the camera in all but name
            items.append((z[i], x[i], y[i], r[i], rgba[i], additive))
    items.sort(key=lambda it: -it[0])
    h, w = img.shape[:2]
    for _, cx, cy, r, rgba, additive in items:
        x0, x1 = max(0, int(cx - r)), min(w, int(cx + r) + 1)
        y0, y1 = max(0, int(cy - r)), min(h, int(cy + r) + 1)
        if x0 >= x1 or y0 >= y1: continue
        xs = np.arange(x0, x1)[None, :] - cx
        ys = np.arange(y0, y1)[:, None] - cy
        d = np.sqrt(xs ** 2 + ys ** 2) / r
        a = np.clip(1.0 - d, 0.0, 1.0) ** 2 * rgba[3]     # the mod's glow texture
        if a.max() <= 0.002: continue
        tile = img[y0:y1, x0:x1]
        col = rgba[:3][None, None, :]
        if additive:
            tile += col * a[:, :, None]
        else:
            tile *= (1 - a[:, :, None])
            tile += col * a[:, :, None]
    np.clip(img, 0, 1, out=img)
    return img
