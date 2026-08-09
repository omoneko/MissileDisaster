# Handover — nuclear effect work

Branch `claude/mushroom-cloud-explosion-effects-anxonz`, 13 commits on top of `master` (`c8b7822`).
Everything described here is pushed. Nothing in this work exists on `master`.

## Read this first: the build in the game is not this build

The game log from the last session showed the mod loading correctly —

```
No source files found: MissileDisaster            <- normal. A DLL mod has no .cs sources
Loading ...\Addons\Mods\MissileDisaster\MissileDisaster.dll
Assembly MissileDisaster, Version=1.0.0.0 loaded.
```

— but **no `[MissileDisaster] build check - ...` line**, which `Mod.OnEnabled` writes as its first
statement, unconditionally. The DLL the game is running therefore predates `e85d401`.

That matters because four rounds of tuning were done on reports from a build that did not contain
them. Settle this before judging anything:

```powershell
cd <repo>
git fetch origin
git checkout claude/mushroom-cloud-explosion-effects-anxonz
git pull
git log --oneline -1          # expect f55952c or later

# with Cities: Skylines fully closed - the DLL is locked while it runs
.\build.ps1
```

Then in game: **Options → Mods → Missile Disaster → Build check**. Two lines:

```
150 kt draws: cloud top 751 m, cap 1440 m wide, fireball 408 m across, rise 31.1 s, screen top 1000 m
Loaded from: C:\...\Addons\Mods\MissileDisaster
```

The numbers are computed by the running code, so they are a fingerprint: a cloud top near **751 m**
is this build, **13245 m** is the original, and **no Build check group at all** is a DLL older than
`f55952c`. The path line settles the case where a Workshop subscription shadows a local build.

### One more thing to rule out

The log also shows `[CSWarfront] DisasterImpactBridge: detected MissileDisaster impact beacon.`
Another mod is reacting to this mod's impacts. If it draws effects of its own, what is on screen may
not be this code at all. Disable CSWarfront once, to see.

## The knobs

All in `src/MissileDisaster/Core/NuclearCloudDisplay.cs`. These are the only numbers in the model
that are taste rather than physics, and they are where to go for "bigger", "smaller", "longer".

| constant | now | what it does |
|---|---|---|
| `CloudScale` | 0.20 | the whole cloud against its real size. Raise for a bigger strike |
| `CloudHeightScale` | 0.35 | a further squash on the height alone, before the ceiling |
| `ScreenTopAltitude` | 1000 | hard bound on the drawn height. **Also `ModConfig.MaxBurstAltitude`** — lowering it lowers the airburst ceiling too |
| `FireballScale` | 0.50 | the fireball, which comes down less far than the cloud so it is not lost under it |
| `RiseCompression` | 12 | real seconds per drawn second. Lower is slower |
| `CapLifetime*` in `NuclearMushroomFx` | 35–60 s | how long the canopy stands |

What these currently produce:

| | cloud top | cap width | fireball | whole shot |
|---|---|---|---|---|
| Little Boy 15 kt | 461 m | 0.61 km | 162 m | 45 s |
| 150 kt baseline | 751 m | 1.44 km | 408 m | 67 s |
| B83 1.2 Mt | 869 m | 3.78 km | 938 m | 87 s |
| Tsar Bomba 50 Mt | 952 m | 10.4 km | 4013 m | 93 s |

If the height is still wrong once a correct build is running, `ScreenTopAltitude` is the lever — but
note it is shared with the airburst ceiling, so decouple them first if only the cloud should move.

## Verification: what is proven and what is not

| | status |
|---|---|
| `Core/**` logic | **232 xUnit tests pass.** `dotnet test tests/MissileDisaster.Core.Tests/` |
| effect code compiles | **passes.** `dotnet build tools/compile-check/CompileCheck.csproj` |
| `ExplosionFx`, `ImpactResolver`, `ModConfig`, `Mod` changes | inspected only — not in the compile check, which does not stub the Colossal API |
| the members exist in Unity 5.6 | **not verified.** See below |
| how any of it looks in game | **not verified.** Never ran |

Three Unity members are used that were not used before this branch. If the real build fails, suspect
these first:

- `ParticleSystemShapeType.Circle` — the canopy's flat disc emitter
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
5. **`18fb3d4`, `bbce362`, `57cfbff`** — playability: the cloud brought down to a fifth, the height
   squashed and then bounded at the altitude the mod already calls the top of the screen; the
   fireball's own radius budget fixed and given a larger scale; the whole shot stretched from 26 s
   to about a minute.
6. **`2c35cfc`, `e85d401`, `f55952c`** — the tooling: a compile check that works without the game,
   and the build fingerprint in the log and on the options screen.

`docs/effects/` holds renders of every stage and of each before/after, with the measured figures
they were checked against.

## Known gaps

- **No low-level layer.** Both 1945 photographs show a broad sheet of cloud across the ground far
  wider than the column. The ground dust here is a skirt about twice the stem's width.
- **No skirt or bell**, and **no true vortex rollover** — the canopy's rim only sags under gravity.
- **The cap is far flatter than the real thing**, because its depth is measured down from a cloud
  top that has been squashed for playability. A 10 Mt canopy comes out eighteen times wider than
  deep where the figures say four. The width is untouched, so the yield still reads.
- **Particle budget**: a nuclear strike now holds up to 900 stem + 420 dust + 400 cap particles for
  as long as 93 s. `StemMaxParticles` in `NuclearMushroomFx` is the one to lower if it costs frames.
