"""Renders the mushroom cloud's five stages, its timeline, and the effect of the raised ceilings."""
import math, os, sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fxlib as fx

OUT = sys.argv[1] if len(sys.argv) > 1 else "docs/effects"
os.makedirs(OUT, exist_ok=True)
plt.rcParams.update({"font.family": "DejaVu Sans", "text.color": "#e8e8ea",
                     "axes.labelcolor": "#e8e8ea", "figure.facecolor": "#14161a"})


def frame(height, width_extent, w, h, fov=38.0, look=0.45, elev=0.30):
    """A camera far enough back that a scene `height` tall and `width_extent` wide fits."""
    half = math.tan(math.radians(fov) * 0.5)
    d_v = height * 1.35 / (2 * half)
    d_h = width_extent * 1.25 / (2 * half * (w / h))
    d = max(d_v, d_h, 200.0)
    return fx.Camera((0, height * elev, -d), (0, height * look, 0), w, h, fov)


def ground_grid(img, cam, spacing, reach, colour=(0.62, 0.66, 0.62), alpha=0.30):
    """A grid on the terrain, so a size can be read off the picture instead of guessed at."""
    h, w = img.shape[:2]
    lines = []
    n = int(reach / spacing)
    for i in range(-n, n + 1):
        p = i * spacing
        lines.append(np.stack([np.full(160, p), np.zeros(160), np.linspace(-reach, reach, 160)], 1))
        lines.append(np.stack([np.linspace(-reach, reach, 160), np.zeros(160), np.full(160, p)], 1))
    for pts in lines:
        x, y, z, fwd = cam.project(pts)
        ok = (fwd > 1) & (x > 0) & (x < w - 1) & (y > 0) & (y < h - 1)
        xi, yi = x[ok].astype(int), y[ok].astype(int)
        fade = np.clip(1.0 - z[ok] / (reach * 2.2), 0.05, 1.0) * alpha
        img[yi, xi] = img[yi, xi] * (1 - fade[:, None]) + np.array(colour) * fade[:, None]
    return img


def scene(cam, batches, grid_spacing, grid_reach, w, h):
    horizon = int(cam.project(np.array([[0.0, 0.0, 1e7]]))[1][0])
    img = fx.sky(w, h, max(0, min(h, horizon)))
    ground_grid(img, cam, grid_spacing, grid_reach)
    fx.draw(img, cam, [b for b in batches if b is not None])
    return img


def km(v):
    return f"{v/1000:.2f} km" if v < 10000 else f"{v/1000:.1f} km"


# ----------------------------------------------------------------- the stages

def render_stages(kt=150.0, w=760, h=560):
    d = fx.dimensions(kt)
    # Each stage is framed on its own scale: the fireball is 400 m across, the cap 7 km.
    life = max(18.0, d["rise"] * 0.8)
    setups = [
        ("1. Fireball", d["fireball_t"] * 0.6, [fx.stage_fireball], d["fireball"] * 3.0, 200,
         f"55·W^0.4 = {km(d['fireball'])} radius, swelling over {d['fireball_t']:.1f} s"),
        ("2. Condensation cloud", d["fireball_t"] * 0.3 + 0.45,
         [fx.stage_fireball, fx.stage_condensation], d["fireball"] * 8.0, 500,
         f"the Wilson dome, {km(d['fireball']*2.6)} out behind the shock, gone in 1.3 s"),
        ("3. Ground dust", 4.2, [fx.stage_fireball, fx.stage_ground_dust],
         d["stem"] * 3.4, 250,
         "afterwinds tear dirt off the ground into the base of the column"),
        ("4. Stem", d["rise"] * 0.5, [fx.stage_ground_dust, fx.stage_stem],
         d["top"] * 0.80, 1000,
         f"climbing at {d['top']/d['rise']:.0f} m/s, {km(d['stem'])} across "
         f"- a {fx.stem_fraction(kt)*100:.0f}% stem"),
        ("5. Cap", d["rise"] * 0.55 + life * 0.62,
         [fx.stage_ground_dust, fx.stage_stem, fx.stage_cap], d["top"] * 1.12, 1000,
         f"the canopy rolls over at {km(d['cap'])} radius, {km(d['top'])} up"),
    ]
    fig, axes = plt.subplots(2, 3, figsize=(16.5, 8.6))
    fig.patch.set_facecolor("#14161a")
    for ax, (name, t, stages, extent, grid, note) in zip(axes.flat, setups):
        cam = frame(extent, extent * 0.9, w, h)
        img = scene(cam, [s(d, t) for s in stages], grid, extent * 3.0, w, h)
        ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
        for sp in ax.spines.values(): sp.set_color("#3a3f47")
        ax.set_title(name, color="#ffd479", fontsize=13, fontweight="bold", pad=7)
        ax.set_xlabel(f"t = {t:.1f} s   ·   grid {km(grid)}\n{note}", fontsize=8.6, color="#b9bec7")
    ax = axes.flat[5]
    ax.set_xticks([]); ax.set_yticks([]); ax.set_facecolor("#181b20")
    for sp in ax.spines.values(): sp.set_color("#3a3f47")
    rows = [("yield", f"{kt:.0f} kt"), ("fireball radius", km(d["fireball"])),
            ("fireball swell", f"{d['fireball_t']:.1f} s"), ("cap radius", km(d["cap"])),
            ("cloud top", km(d["top"])), ("stem radius", km(d["stem"])),
            ("rise (compressed 25:1)", f"{d['rise']:.1f} s")]
    ax.set_title("all five, to scale", color="#ffd479", fontsize=13, fontweight="bold", pad=7)
    for i, (k, v) in enumerate(rows):
        ax.text(0.06, 0.88 - i * 0.115, k, fontsize=11, color="#9aa1ab", transform=ax.transAxes)
        ax.text(0.94, 0.88 - i * 0.115, v, fontsize=11, color="#e8e8ea", ha="right",
                fontweight="bold", transform=ax.transAxes)
    ax.set_xlabel("every figure from Glasstone & Dolan (1977) ch. II", fontsize=8.6, color="#b9bec7")
    fig.suptitle(f"Nuclear detonation, stage by stage — {kt:.0f} kt groundburst",
                 color="#ffffff", fontsize=17, fontweight="bold", y=0.985)
    fig.tight_layout(rect=[0, 0.015, 1, 0.955])
    path = os.path.join(OUT, "stages.png")
    fig.savefig(path, dpi=115, facecolor="#14161a"); plt.close(fig)
    return path


# --------------------------------------------------------------- the timeline

def render_timeline(kt=150.0, w=620, h=620):
    d = fx.dimensions(kt)
    times = [0.6, 2.5, 6.0, d["rise"] * 0.6, d["rise"] * 0.95, d["rise"] * 0.55 + 14.0]
    extent = d["top"] * 1.15
    cam = frame(extent, max(extent * 0.85, d["cap"] * 2.4), w, h)
    fig, axes = plt.subplots(1, 6, figsize=(22, 4.6))
    fig.patch.set_facecolor("#14161a")
    for ax, t in zip(axes, times):
        img = scene(cam, [s(d, t) for _, s in fx.STAGES], 1000, extent * 3.0, w, h)
        ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
        for sp in ax.spines.values(): sp.set_color("#3a3f47")
        ax.set_title(f"t = {t:.1f} s", color="#ffd479", fontsize=12, fontweight="bold", pad=6)
    axes[0].set_xlabel("grid 1 km · one fixed camera", fontsize=9, color="#b9bec7")
    fig.suptitle(f"The same {kt:.0f} kt detonation over time — one camera, 1 km grid",
                 color="#ffffff", fontsize=17, fontweight="bold", y=0.99)
    fig.tight_layout(rect=[0, 0.02, 1, 0.93])
    path = os.path.join(OUT, "timeline.png")
    fig.savefig(path, dpi=112, facecolor="#14161a"); plt.close(fig)
    return path


# ------------------------------------------------------- the raised ceilings

WEAPONS = [("Little Boy", 15), ("150 kt baseline", 150), ("B83", 1200),
           ("Ivy Mike", 10400), ("Tsar Bomba", 50000)]


def render_yields(w=560, h=680):
    """The same six weapons under the old hard clamps and under the new soft ceilings."""
    # One camera for all ten panels, framed on the largest cloud, so the sizes can be compared.
    biggest = fx.dimensions(50000)
    extent = biggest["top"] * 1.05
    cam = frame(extent, biggest["cap"] * 2.2, w, h, look=0.42, elev=0.22)
    fig, axes = plt.subplots(2, len(WEAPONS), figsize=(20.5, 10.6))
    fig.patch.set_facecolor("#14161a")
    for col, (name, kt) in enumerate(WEAPONS):
        for row, mode in enumerate(("old", "new")):
            d = fx.dimensions(kt, mode)
            t = d["rise"] * 0.55 + max(18.0, d["rise"] * 0.8) * 0.7
            ax = axes[row, col]
            img = scene(cam, [s(d, t) for _, s in fx.STAGES], 2000, extent * 3.0, w, h)
            ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
            for sp in ax.spines.values():
                sp.set_color("#8c5a3a" if mode == "old" else "#4a7f5a"); sp.set_linewidth(1.6)
            if row == 0:
                ax.set_title(f"{name}\n{kt:,} kt", color="#ffd479", fontsize=13,
                             fontweight="bold", pad=8)
            ax.set_xlabel(f"cap {km(d['cap'])} · top {km(d['top'])}", fontsize=9.5,
                          color="#d9a06a" if mode == "old" else "#8fd6a6")
    for row, label, colour in ((0, "BEFORE — hard clamps\ncap ≤ 8 km, top ≤ 12 km", "#d9a06a"),
                               (1, "AFTER — soft ceilings\ncap → 26 km, top → 30 km", "#8fd6a6")):
        axes[row, 0].text(-0.10, 0.5, label, transform=axes[row, 0].transAxes, rotation=90,
                          va="center", ha="center", fontsize=12.5, fontweight="bold", color=colour)
    fig.suptitle("Raising the ceiling: the same camera, the same 2 km grid, five yields",
                 color="#ffffff", fontsize=19, fontweight="bold", y=0.985)
    fig.tight_layout(rect=[0.015, 0.02, 1, 0.94])
    fig.subplots_adjust(hspace=0.15)
    path = os.path.join(OUT, "yield-ceilings.png")
    fig.savefig(path, dpi=105, facecolor="#14161a"); plt.close(fig)
    return path


# ------------------------------------------------------------- the cap's birth

def render_cap_fix(kt=150.0, w=620, h=660):
    """The canopy used to be emitted at the cloud top, finished, before the stem arrived."""
    d = fx.dimensions(kt)
    times = [d["rise"] * 0.62, d["rise"] * 0.75, d["rise"] * 0.95]
    extent = d["top"] * 1.12
    cam = frame(extent, d["cap"] * 2.4, w, h)
    others = [fx.stage_ground_dust, fx.stage_stem]
    fig, axes = plt.subplots(2, 3, figsize=(13.5, 10.4))
    fig.patch.set_facecolor("#14161a")
    for row, (capfn, colour) in enumerate(((fx.stage_cap_legacy, "#8c5a3a"),
                                           (fx.stage_cap, "#4a7f5a"))):
        for col, t in enumerate(times):
            ax = axes[row, col]
            img = scene(cam, [s(d, t) for s in others] + [capfn(d, t)], 1000, extent * 3.0, w, h)
            ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
            for sp in ax.spines.values(): sp.set_color(colour); sp.set_linewidth(1.6)
            if row == 0:
                ax.set_title(f"t = {t:.1f} s", color="#ffd479", fontsize=13,
                             fontweight="bold", pad=7)
    for row, label, colour in (
            (0, "BEFORE\na finished canopy hangs\nin clear air above the stem", "#d9a06a"),
            (1, "AFTER\nit swells out of the column's head,\nrides up with it, and is a lens", "#8fd6a6")):
        axes[row, 0].text(-0.12, 0.5, label, transform=axes[row, 0].transAxes, rotation=90,
                          va="center", ha="center", fontsize=12, fontweight="bold", color=colour)
    fig.suptitle("Where the cap is born — 150 kt, one camera, 1 km grid",
                 color="#ffffff", fontsize=18, fontweight="bold", y=0.98)
    fig.tight_layout(rect=[0.02, 0.01, 1, 0.94])
    path = os.path.join(OUT, "cap-birth.png")
    fig.savefig(path, dpi=110, facecolor="#14161a"); plt.close(fig)
    return path


# -------------------------------------------------------------- the cap's depth

SHAPE_WEAPONS = [("150 kt baseline", 150), ("B83", 1200), ("Ivy Mike", 10400)]


def render_cap_shape(w=660, h=620):
    """Sizing the canopy off its own width against off the cloud top, as Glasstone has it."""
    fig, axes = plt.subplots(2, len(SHAPE_WEAPONS), figsize=(14.5, 10.4))
    fig.patch.set_facecolor("#14161a")
    for col, (name, kt) in enumerate(SHAPE_WEAPONS):
        d = fx.dimensions(kt)
        g = fx.cap_geometry(d)
        t = d["rise"] * 0.55 + g["lifetime"] * 0.8
        extent = max(d["top"] * 1.25, d["cap"] * 2.4)
        cam = frame(extent, d["cap"] * 2.5, w, h)
        others = [fx.stage_ground_dust, fx.stage_stem]
        old_thick = d["cap"] * (1 - 0.35 - 0.45 * 1.6 * 0.5) + d["cap"] * 0.45 * 1.6
        for row, (capfn, colour, thick) in enumerate((
                (fx.stage_cap_thick, "#8c5a3a", old_thick),
                (fx.stage_cap, "#4a7f5a", g["thickness"]))):
            ax = axes[row, col]
            img = scene(cam, [s(d, t) for s in others] + [capfn(d, t)], 2000, extent * 3.0, w, h)
            ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
            for sp in ax.spines.values(): sp.set_color(colour); sp.set_linewidth(1.6)
            if row == 0:
                ax.set_title(f"{name} — {kt:,} kt\ncap {km(d['cap']*2)} wide, top {km(d['top'])}",
                             color="#ffd479", fontsize=12, fontweight="bold", pad=8)
            ax.set_xlabel(f"cap depth {km(thick)}   ({thick/(0.3*d['top']):.1f}× Glasstone's "
                          f"{km(0.3*d['top'])})", fontsize=9.5,
                          color="#d9a06a" if row == 0 else "#8fd6a6")
    for row, label, colour in (
            (0, "BEFORE — depth from the cap's own width", "#d9a06a"),
            (1, "AFTER — depth from the cloud top (0.3 × top)", "#8fd6a6")):
        axes[row, 0].text(-0.09, 0.5, label, transform=axes[row, 0].transAxes, rotation=90,
                          va="center", ha="center", fontsize=11.5, fontweight="bold", color=colour)
    fig.suptitle("How deep the canopy is — Glasstone puts its base at 0.7 of the cloud top",
                 color="#ffffff", fontsize=18, fontweight="bold", y=0.98)
    fig.tight_layout(rect=[0.012, 0.01, 1, 0.93])
    fig.subplots_adjust(hspace=0.16)
    path = os.path.join(OUT, "cap-shape.png")
    fig.savefig(path, dpi=110, facecolor="#14161a"); plt.close(fig)
    return path



# ------------------------------------------------ against the 1945 photographs

def render_1945(w=680, h=780):
    """The two bursts there are famous photographs of, at the scale and burst height the mod
    would fly them at, so the render can be held against the real thing."""
    shots = [("Little Boy — Hiroshima", 15, 580), ("Fat Man — Nagasaki", 22, 503)]
    fig, axes = plt.subplots(1, 2, figsize=(11.5, 7.2))
    fig.patch.set_facecolor("#14161a")
    for ax, (name, kt, real_hob) in zip(axes, shots):
        d = fx.dimensions(kt)
        g = fx.cap_geometry(d)
        t = d["rise"] * 0.55 + g["lifetime"] * 0.5
        extent = d["top"] * 1.18
        cam = frame(extent, d["cap"] * 2.6, w, h, look=0.46, elev=0.16)
        stages = [fx.stage_ground_dust(d, t), fx.stage_stem(d, t),
                  fx.stage_cap(d, t, airburst=True)]
        img = scene(cam, stages, 1000, extent * 3.0, w, h)
        ax.imshow(img); ax.set_xticks([]); ax.set_yticks([])
        for sp in ax.spines.values(): sp.set_color("#3a3f47")
        mod_hob = 900.0 * (kt / 150.0) ** (1 / 3)
        ax.set_title(f"{name} — {kt} kt airburst", color="#ffd479",
                     fontsize=13, fontweight="bold", pad=8)
        ax.set_xlabel(
            f"cap {km(d['cap']*2)} wide · {km(g['thickness'])} deep · top {km(d['top'])}\n"
            f"stem {km(d['stem']*2)} wide, {fx.stem_fraction(kt)*100:.0f}% of the cap"
            f"   ·   grid 1 km\n"
            f"burst height {mod_hob:.0f} m (the real one: {real_hob} m)",
            fontsize=9.5, color="#b9bec7")
    fig.suptitle("At the scale of the 1945 photographs — white canopy over a dark dust column",
                 color="#ffffff", fontsize=16, fontweight="bold", y=0.975)
    fig.tight_layout(rect=[0, 0.01, 1, 0.92])
    path = os.path.join(OUT, "nineteen-forty-five.png")
    fig.savefig(path, dpi=115, facecolor="#14161a"); plt.close(fig)
    return path


if __name__ == "__main__":
    for p in (render_stages(), render_timeline(), render_yields(),
              render_cap_fix(), render_cap_shape(), render_1945()):
        print("wrote", p)
