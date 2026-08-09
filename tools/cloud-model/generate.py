"""Generates the mod's mushroom cloud mesh and texture from scratch.

Everything here is original and procedural - no external model or image is used, so the output
is covered by the repository's MIT licence. Reference material informed the *shape constants*
only: the 1945 photographs and public test footage, which show

  - a cap that is a crowd of cauliflower lobes of visibly different sizes, wider than it is
    deep, with the rim rolling under at its edge and overhanging the column
  - a column noticeably narrower than the cap - about a third of its width at strategic
    yields - with vertical billows rather than a smooth cylinder
  - a skirt of dust where the column meets the ground
  - colour: near-white vapour in the cap, grey-brown dust in the column and skirt, and fire
    glowing in the deep crevices of the lower half early in the cloud's life

The mesh is a surface of revolution around Y - profile below - displaced along its normals by
a fixed-seed noise field: large gaussian lobes for the cauliflower, small ones on the column
for the billows, and fine fBm everywhere. The texture is painted from the same seed: a colour
ramp along the height, shaded by seamless value noise, crevices darkened, embers added where
the crevices are deepest.

Output follows the mod's aligned-OBJ convention (one v/vt/vn each, in the same order, faces
f a/a/a), normalised to height 1.0 with the base at y=0, so ObjParser textures it and the game
scales it straight to metres.

Usage:  python tools/cloud-model/generate.py [out-models-folder]
"""
import os
import sys

import numpy as np

SEED = 20260809          # fixed, so the shipped asset is reproducible bit for bit
AZIMUTH_SEGMENTS = 48    # columns around the axis (plus a duplicated seam column for UVs)
TEXTURE_SIZE = 1024

# The profile, (y, r) pairs from the bottom pole up over the cap to the top pole, drawn from
# the reference proportions: cap max half-width 0.59 of the height, its underside near 0.60,
# the column about 0.11-0.13, a dust skirt at the base. Resampled densely by arclength before
# revolving.
PROFILE = [
    (0.000, 0.000),  # bottom pole, on the ground
    (0.010, 0.170),  # dust skirt spreading at the base
    (0.040, 0.150),
    (0.090, 0.125),  # skirt narrowing into the column
    (0.180, 0.110),  # the column
    (0.320, 0.105),  # its waist
    (0.450, 0.115),
    (0.540, 0.135),  # swelling toward the cap
    (0.585, 0.180),  # the throat, where the column disappears into the cap
    (0.600, 0.330),  # the cap's underside, running out over the overhang
    (0.595, 0.470),  # sagging slightly - the underside droops between throat and rim
    (0.620, 0.560),  # the rim rolling under
    (0.680, 0.590),  # the rim's widest point
    (0.780, 0.560),  # the upper cap curving in
    (0.880, 0.470),
    (0.950, 0.320),
    (0.990, 0.150),
    (1.000, 0.000),  # top pole
]

# Where along the profile (by arclength fraction) each region sits, for lobe placement and
# painting. Kept in one place so the mesh and the texture agree about what is cap and what is
# column.
SKIRT_END = 0.10
COLUMN_END = 0.52
CAP_START = 0.56


def smoothstep(t):
    return t * t * (3.0 - 2.0 * t)


def value_noise(x, y, z, seed):
    """Trilinear value noise on an integer lattice, vectorised. Inputs are arrays in lattice units."""
    def hash01(ix, iy, iz):
        h = (ix.astype(np.uint32) * np.uint32(374761393)
             + iy.astype(np.uint32) * np.uint32(668265263)
             + iz.astype(np.uint32) * np.uint32(1274126177)
             + np.uint32((seed * 974711) & 0xFFFFFFFF))
        h ^= h >> np.uint32(13)
        h *= np.uint32(1911520717)
        h ^= h >> np.uint32(16)
        return h.astype(np.float64) / np.float64(0xFFFFFFFF)

    x0, y0, z0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64), np.floor(z).astype(np.int64)
    fx, fy, fz = smoothstep(x - x0), smoothstep(y - y0), smoothstep(z - z0)
    out = np.zeros_like(x, dtype=np.float64)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = ((fx if dx else 1 - fx) * (fy if dy else 1 - fy) * (fz if dz else 1 - fz))
                out += w * hash01(x0 + dx, y0 + dy, z0 + dz)
    return out


def fbm(x, y, z, seed, octaves=4):
    total, amp, freq, norm = 0.0, 1.0, 1.0, 0.0
    for o in range(octaves):
        total = total + amp * value_noise(x * freq, y * freq, z * freq, seed + o * 101)
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return total / norm


def resample_profile(samples_per_unit=64.0, minimum=72):
    """The profile as dense (y, r, t) samples, t being the 0..1 arclength fraction."""
    pts = np.array(PROFILE, dtype=np.float64)
    seg = np.hypot(np.diff(pts[:, 0]), np.diff(pts[:, 1]))
    arc = np.concatenate([[0.0], np.cumsum(seg)])
    total = arc[-1]
    count = max(minimum, int(total * samples_per_unit))
    t = np.linspace(0.0, 1.0, count)
    y = np.interp(t * total, arc, pts[:, 0])
    r = np.interp(t * total, arc, pts[:, 1])
    return y, r, t


def build_revolve(y_prof, r_prof, t_prof):
    """The undisplaced surface of revolution, with a duplicated seam column for clean UVs."""
    cols = AZIMUTH_SEGMENTS + 1
    theta = np.linspace(0.0, 2.0 * np.pi, cols)
    rings = len(y_prof)

    yy = np.repeat(y_prof, cols)
    rr = np.repeat(r_prof, cols)
    tt = np.repeat(t_prof, cols)
    th = np.tile(theta, rings)
    positions = np.stack([rr * np.cos(th), yy, rr * np.sin(th)], axis=1)
    uvs = np.stack([np.tile(theta / (2.0 * np.pi), rings), tt], axis=1)

    tris = []
    for j in range(rings - 1):
        for i in range(AZIMUTH_SEGMENTS):
            a = j * cols + i
            b = j * cols + i + 1
            c = (j + 1) * cols + i
            d = (j + 1) * cols + i + 1
            tris.append((a, c, b))
            tris.append((b, c, d))
    return positions, uvs, tt, np.array(tris, dtype=np.int64)


def vertex_normals(positions, tris):
    normals = np.zeros_like(positions)
    p0, p1, p2 = positions[tris[:, 0]], positions[tris[:, 1]], positions[tris[:, 2]]
    face = np.cross(p1 - p0, p2 - p0)  # area-weighted; degenerate pole slivers contribute nothing
    for k in range(3):
        np.add.at(normals, tris[:, k], face)
    lengths = np.linalg.norm(normals, axis=1, keepdims=True)
    lengths[lengths < 1e-12] = 1.0
    return normals / lengths


def lobe_field(positions, t_arc, rng):
    """The cauliflower: gaussian bumps along the surface, large on the cap, small on the column."""
    displacement = np.zeros(len(positions))
    regions = [
        # (t range, lobe count, radius range, amplitude range)
        ((CAP_START, 1.00), 58, (0.09, 0.22), (0.045, 0.095)),  # the cap's lobes - pronounced, cauliflower
        ((SKIRT_END, COLUMN_END), 30, (0.05, 0.11), (0.018, 0.040)),  # the column's billows
        ((0.00, SKIRT_END), 8, (0.07, 0.13), (0.010, 0.022)),  # low mounds of dust in the skirt
    ]
    for (t0, t1), count, (r0, r1), (a0, a1) in regions:
        candidates = np.where((t_arc >= t0) & (t_arc <= t1))[0]
        centres = positions[rng.choice(candidates, size=count, replace=False)]
        radii = rng.uniform(r0, r1, size=count)
        amps = rng.uniform(a0, a1, size=count)
        for c, r, a in zip(centres, radii, amps):
            d2 = np.sum((positions - c) ** 2, axis=1)
            displacement += a * np.exp(-d2 / (r * r))
    return displacement


def build_mesh():
    y_prof, r_prof, t_prof = resample_profile()
    positions, uvs, t_arc, tris = build_revolve(y_prof, r_prof, t_prof)
    rng = np.random.default_rng(SEED)

    normals = vertex_normals(positions, tris)
    bumps = lobe_field(positions, t_arc, rng)
    fine = (fbm(positions[:, 0] * 6.0, positions[:, 1] * 6.0, positions[:, 2] * 6.0, SEED) - 0.5) * 0.05
    displacement = np.clip(bumps + fine, -0.05, 0.16)
    positions = positions + normals * displacement[:, None]

    # Re-normalise after displacement: base back to y=0, height back to exactly 1.
    positions[:, 1] -= positions[:, 1].min()
    positions /= positions[:, 1].max()

    normals = vertex_normals(positions, tris)
    return positions, uvs, normals, tris


def paint_texture():
    """The diffuse map, painted in UV space: u wraps around the cloud, v runs bottom to top."""
    u = (np.arange(TEXTURE_SIZE) + 0.5) / TEXTURE_SIZE
    v = (np.arange(TEXTURE_SIZE) + 0.5) / TEXTURE_SIZE
    uu, vv = np.meshgrid(u, v)  # rows are v, columns are u; row 0 is written as the image's bottom

    # Sample noise on a cylinder so the left and right edges meet without a seam.
    cx, sx = np.cos(2.0 * np.pi * uu), np.sin(2.0 * np.pi * uu)
    broad = fbm(cx * 3.0, vv * 4.0, sx * 3.0, SEED + 7)            # large drifts of light and shade
    crevice = fbm(cx * 9.0, vv * 14.0, sx * 9.0, SEED + 23)        # the folds between lobes
    ember_noise = fbm(cx * 5.0, vv * 7.0, sx * 5.0, SEED + 41)

    # The colour ramp along the height: dust at the skirt, grey-brown column, white cap.
    stops = np.array([
        [0.00, 0.55, 0.48, 0.40],   # ground dust
        [SKIRT_END, 0.58, 0.52, 0.45],
        [0.35, 0.66, 0.62, 0.57],   # the column, paling as it climbs
        [COLUMN_END, 0.74, 0.71, 0.67],
        [CAP_START + 0.06, 0.88, 0.87, 0.85],  # the cap's vapour
        [0.85, 0.94, 0.93, 0.92],
        [1.00, 0.97, 0.96, 0.95],   # sunlit top
    ])
    r = np.interp(vv, stops[:, 0], stops[:, 1])
    g = np.interp(vv, stops[:, 0], stops[:, 2])
    b = np.interp(vv, stops[:, 0], stops[:, 3])
    rgb = np.stack([r, g, b], axis=2)

    # Broad shading, then the crevices cut in - deeper on the cap, where the lobes are.
    rgb *= (0.82 + 0.36 * broad)[:, :, None]
    crevice_depth = np.where(vv > CAP_START, 0.38, 0.24)
    rgb *= (1.0 - crevice_depth * (1.0 - crevice) ** 1.5)[:, :, None]

    # Embers: fire showing through the deepest folds of the column and the cap's underside,
    # the way the test footage glows orange from inside early on.
    ember_zone = np.clip(1.0 - np.abs(vv - 0.45) / 0.28, 0.0, 1.0)
    ember = ((1.0 - crevice) ** 2.2) * ember_zone * np.clip(ember_noise - 0.35, 0.0, 1.0) * 2.0
    ember = np.clip(ember, 0.0, 0.55)
    rgb[:, :, 0] += ember * 0.95
    rgb[:, :, 1] += ember * 0.38
    rgb[:, :, 2] += ember * 0.06

    return (np.clip(rgb, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def write_png(path, pixels):
    """Minimal PNG writer (RGB8), so the generator needs nothing beyond numpy."""
    import struct
    import zlib

    height, width, _ = pixels.shape
    # PNG rows run top to bottom; row 0 of the array is the bottom of the ramp (v=0), and OBJ
    # vt has v=0 at the bottom too, so flip for storage.
    flipped = pixels[::-1]
    raw = b"".join(b"\x00" + flipped[row].tobytes() for row in range(height))

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", header))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(__file__), "..", "..", "src", "MissileDisaster", "Models")
    os.makedirs(out_dir, exist_ok=True)

    positions, uvs, normals, tris = build_mesh()
    half_x = np.abs(positions[:, 0]).max()
    half_z = np.abs(positions[:, 2]).max()
    print(f"vertices={len(positions)} triangles={len(tris)}")
    print(f"normalised: height=1.0  cap half-width x={half_x:.3f} z={half_z:.3f}")

    header = ("# Procedurally generated by tools/cloud-model/generate.py (seed %d)."
              " Original work; MIT licence, same as the repository.\n"
              "# Do not edit by hand - regenerate instead.\n" % SEED)
    with open(os.path.join(out_dir, "MushroomCloud.obj"), "w", newline="\n") as f:
        f.write(header)
        f.write("mtllib MushroomCloud.mtl\nusemtl CloudSurface\n")
        for p in positions:
            f.write("v %.6f %.6f %.6f\n" % (p[0], p[1], p[2]))
        for t in uvs:
            f.write("vt %.6f %.6f\n" % (t[0], t[1]))
        for n in normals:
            f.write("vn %.6f %.6f %.6f\n" % (n[0], n[1], n[2]))
        for a, b, c in tris + 1:
            f.write("f %d/%d/%d %d/%d/%d %d/%d/%d\n" % (a, a, a, b, b, b, c, c, c))

    with open(os.path.join(out_dir, "MushroomCloud.mtl"), "w", newline="\n") as f:
        f.write(header)
        f.write("newmtl CloudSurface\nKd 1.0 1.0 1.0\nd 1.0\nmap_Kd MushroomCloud.png\n")

    write_png(os.path.join(out_dir, "MushroomCloud.png"), paint_texture())
    print(f"wrote MushroomCloud.obj / .mtl / .png ({TEXTURE_SIZE}px) to {os.path.abspath(out_dir)}")


if __name__ == "__main__":
    main()
