# Handover — nuclear effect work

> **Superseded in part.** After the playtest ("forms too slowly, stays too long, still not a
> mushroom"), the particle stem and cap described below were replaced wholesale by a textured
> 3D mesh (`Models/MushroomCloud.*`, CC-BY-4.0, converted by `tools/cloud-model/convert.py`),
> animated by `Core/CloudAnimation` + `Game/Effects/MushroomCloudAnimator`. Timings were cut to
> about a third (150 kt: 8.3 s rise, ~24 s total). The knobs table and fingerprint below are
> updated; the commit-by-commit history further down describes the particle era and is kept for
> the record.

Branch `claude/mushroom-cloud-explosion-effects-anxonz`, on top of `master` (`c8b7822`).
Everything described here is pushed. Nothing in this work exists on `master`.

## The one thing this work got wrong, and how it was found

For four rounds the game was running a DLL that did not contain the changes, so the reports coming
back were about code that had never loaded. The log settled it: the mod was loading fine from the
local Addons folder, but the `[MissileDisaster] build check` line that `Mod.OnEnabled` writes
unconditionally as its first statement was absent.

Once a correct build finally ran, the first real report was that it did not look like a mushroom.
It did not, and the reason was in this work rather than in the build: the height had been squashed
on its own, twice, to answer "the cloud is too tall". Width and height had separate scales, and a
150 kt column came out **459 m across and 375 m tall** under a canopy three and a half times too
wide for it. A stem wider than it is tall is not a stem.

The fix was to collapse the two scales into one. `CloudScale` now moves cap, stem and height
together, so the drawn shape is the shape the figures give it, and the height ceiling is a
guarantee rather than a shaping tool. **If a cloud has to be smaller, lower `CloudScale` and let
it take the width with it.** Two tests now pin the silhouette so this cannot come back.

## Checking which build is running

**Options → Mods → Missile Disaster → Build check**:

```
150 kt draws: cloud top 781 m, cap 431 m wide, fireball 131 m across, rise 31.1 s, screen top 1000 m
Loaded from: C:\...\Addons\Mods\MissileDisaster
```

The numbers are computed by the running code, so they are a fingerprint: a cloud top near **781 m**
is this build, **13245 m** is the original, and **no Build check group at all** is an older DLL. The
path line settles the case where a Workshop subscription shadows a local build.

Rebuild with Cities: Skylines fully closed — the DLL is locked while it runs.

### One more thing to rule out

The log also shows `[CSWarfront] DisasterImpactBridge: detected MissileDisaster impact beacon.`
Another mod is reacting to this mod's impacts. If it draws effects of its own, what is on screen may
not be this code at all. Disable CSWarfront once, to see.

## The knobs

All in `src/MissileDisaster/Core/NuclearCloudDisplay.cs`. These are the only numbers in the model
that are taste rather than physics, and they are where to go for "bigger", "smaller", "longer".

| constant | now | what it does |
|---|---|---|
| `CloudScale` | 0.06 | the whole cloud against its real size — **width and height alike**. Raise for a bigger strike, lower for a smaller one. Do not scale height on its own: that is what broke the mushroom shape once already |
| `ScreenTopAltitude` | 1000 | soft bound on the drawn height, knee at 700. **Also `ModConfig.MaxBurstAltitude`** — lowering it lowers the airburst ceiling too |
| `FireballScale` | 0.16 | the fireball, which comes down less far than the cloud so it is not lost under it |
| `RiseCompression` | 45 | real seconds per drawn second. Lower is slower; bounds 5/10/16 s |
| `HoldFactor` / `HoldSecondsMin/Max` | 1.2 / 8–16 s | how long the cloud stands at full size |
| `FadeSeconds` | 6 | how long it takes to thin away |
| `BirthFraction`, `WidthLagPower` in `Core/CloudAnimation` | 0.12 / 1.6 | how small the mesh is born, and how far the cap trails the column |

What these currently produce:

| | cloud top | cap width | fireball | whole shot |
|---|---|---|---|---|
| Little Boy 15 kt | 398 m | 184 m | 52 m | 19 s |
| 150 kt baseline | 781 m | 431 m | 131 m | 24 s |
| B83 1.2 Mt | 927 m | 1133 m | 300 m | 33 s |
| Tsar Bomba 50 Mt | 987 m | 3119 m | 1284 m | 38 s |

If the size is still wrong, **`CloudScale` is the lever** — it moves width and height together and
so cannot break the silhouette. `ScreenTopAltitude` is a guarantee, not a shaping tool, and it is
shared with the airburst ceiling; decouple them first if only the cloud should move.

## Verification: what is proven and what is not

| | status |
|---|---|
| `Core/**` logic | **251 xUnit tests pass.** `dotnet test tests/MissileDisaster.Core.Tests/` — includes a test that parses the shipped `MushroomCloud.obj` and checks its normalisation |
| effect code compiles | **passes**, against both the stubs (`tools/compile-check`) and the real game assemblies (`build.ps1`, 0 warnings) |
| the mesh-cloud Unity members exist in 5.6 | **verified by reflection against the game's own UnityEngine.dll**: `Texture2D.LoadImage`, `MeshRenderer.materials`, `ParticleSystemShapeType.Circle`, both `MinMaxCurve` ctors. The transparent-fade shader is probed at runtime and falls back to a smoke-covered teardown if absent |
| how it looks in game | **not verified since the mesh rework.** The next playtest is the judge |

The particle-era warnings that used to sit here (three unverified members) are resolved — the
build now compiles against the real assemblies on this machine. Suspects if something still looks
wrong in game:

- `ParticleSystemShapeType.Circle` — the canopy's flat disc emitter (still used by ShockWaveFx)
- `ParticleSystem.MinMaxCurve(float min, float max)` — random size and drift
- `ParticleSystem.MinMaxCurve(float multiplier, AnimationCurve)` via `ParticleBuilder.Rise` —
  the climb-then-settle curve. `SpeedCurve` already used this shape, so it is the safest of the three

`tools/compile-check` cannot answer this: a stub compiles whatever is written in it. It catches
syntax, arity and type mistakes, which is what it was added for.

## Local dev environment

| to do this | you need |
|---|---|
| build the mod | Windows, MSBuild, and Cities: Skylines installed (`build.ps1` finds its managed DLLs) |
| run the Core tests | .NET SDK 8. No game needed |
| run the compile check | .NET SDK 8. No game needed |
| regenerate the effect renders | Python 3 with `numpy` and `matplotlib`. `python3 tools/effect-preview/render.py docs/effects` |

## What changed, and why

Each of these is a commit with the reasoning in its message; `git log master..HEAD` reads as the
narrative.

1. **`dcb7486`** — every dimension was hard-clamped, and the clamps bit inside the shipped weapons:
   a B83, an Ivy Mike and a Tsar Bomba were the same picture. Replaced with a soft ceiling that is
   exact below the knee and asymptotic above it, so size never stops following yield.
2. **`514630d`** — the cap's depth came from its own width, making it exactly twice as wide as deep
   at *every* yield. Taken from the cloud top instead.
3. **`b7fb21b`** — from the 1945 photographs: a cloud is two colours (white canopy, dark column),
   the column was drawn 1.9× too wide, and it came off the ground because its particles never
   stopped climbing.
4. **`de19d69`** — the canopy's base is the tropopause, or half the cloud height below it. That
   reproduces Ivy Mike's and Castle Bravo's measured cap bases and is why a 20 kt cap is a ball and
   a megaton cap a sheet.
5. **`18fb3d4`, `bbce362`, `57cfbff`** — playability: the cloud brought down in size and its height
   bounded at the altitude the mod already calls the top of the screen; the fireball's own radius
   budget fixed and given a scale of its own; the whole shot stretched from 26 s to about a minute.
   The height was squashed independently of the width here, which is the mistake the next commit
   undoes.
6. **`2c35cfc`, `e85d401`, `f55952c`** — the tooling: a compile check that works without the game,
   and the build fingerprint in the log and on the options screen.

`docs/effects/` holds renders of every stage and of each before/after, with the measured figures
they were checked against.

## Known gaps

- **No low-level layer.** Both 1945 photographs show a broad sheet of cloud across the ground far
  wider than the column. The ground dust here is a skirt about twice the stem's width.
- **No skirt or bell**, and **no true vortex rollover** — the canopy's rim only sags under gravity.
- **The largest clouds are wider than their share**, because the height ceiling compresses them
  while the width is untouched. Below about a megaton the drawn shape is the real shape.
- **Particle budget**: a nuclear strike now holds up to 900 stem + 420 dust + 400 cap particles for
  as long as 93 s. `StemMaxParticles` in `NuclearMushroomFx` is the one to lower if it costs frames.
