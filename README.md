# Missile Disaster

A Cities: Skylines (2015 / base game) mod that lets you launch missiles with several warhead
types at any location — and defend against them. Nuclear strikes leave realistic, persistent
radioactive fallout that you can clean up with a dedicated decontamination facility.

> Base game only (no DLC required). If the **Natural Disasters** DLC is present, the vanilla
> meteor impact effect is used for the explosion flash; otherwise a built‑in particle fireball is
> used. The nuclear mushroom cloud is always the mod's own, so its size can be tied to the blast.

## Features

- **5 warhead types** with distinct impact behavior:
  - Conventional, Cluster, White Phosphorus (incendiary), Thermobaric (overpressure), Nuclear
- **Adjustable yield**
  - Nuclear: pick from 10 real weapons (Little Boy … Tsar Bomba) or type a custom **kt** value
  - Non‑nuclear: type a custom charge in **kg TNT**
  - Blast radii follow the real cube‑root scaling law (`radius ∝ yield^(1/3)`)
  - Craters are deliberately exaggerated (~2.4× for kg‑yield warheads) so a single hit visibly
    scars the ground, while the destruction radius is pulled back ~10% so it does not flatten the
    whole block around it
- **Air burst / ground burst**
  - Ground burst: forms a crater and destroys roads/bridges/foundations; leaves radioactive fallout (nuclear)
  - Air burst: **detonates in mid‑air above the target** — the fireball hangs over the city, no
    crater is formed, the blast/fire reach wider, and roads / water pipes / metro / foundations
    survive. Burst altitude follows the yield (≈30–150 m conventional, ~900 m for a 150 kt
    nuclear) and is capped at 1 km so the explosion stays on screen
  - The **mushroom cloud always rises from the ground**, even for an air burst
- **Physically modelled nuclear detonation** — every dimension comes from Glasstone & Dolan,
  *The Effects of Nuclear Weapons* (1977), ch. II, not from taste:
  - Fireball radius `55·W^0.4` m, swelling over `10·(W/1Mt)^0.4` s (1 Mt → 869 m in 10 s), cooling
    white → yellow → orange → dull red
  - Wilson **condensation cloud** — the white dome that flashes out behind the shock and is gone
    within a second
  - Stabilised cap radius `0.6 km·10^(0.0137L³−0.0358L²+0.37L)` and cloud top
    `3.0 km·10^(0.006941L⁴−0.06216L³+0.1526L²+0.1878L)`, `L = log₁₀(W/kt)`
    — at 150 kt that is a 3.6 km cap under a 13 km column, which happens to match the 3.7 km
    destruction radius within a few per cent
  - Stem half the cap's width at 20 kt, a seventh of it in the megaton range
  - The cap swells out of the head of the column and rides the rest of the way up with it,
    rather than appearing finished at the cloud top before the stem gets there
  - The canopy's underside is the **tropopause** — the lid that stops a cloud rising and spreads
    it sideways — or half the cloud's height for a small one that never reaches it. That is why a
    20 kt cap is a ball and a megaton cap is a sheet, and it reproduces Ivy Mike's and Castle
    Bravo's measured cap bases to within a few per cent
  - An **airburst's canopy turns white** as the water in it condenses, over a dark dust column,
    the way both 1945 photographs show; a groundburst's keeps its dirt
  - Ground dust drawn up by the afterwinds
  - **The size never stops following the yield.** Nothing is hard‑clamped: every dimension is
    exact up to the point the map can still carry it and then compressed smoothly towards a
    ceiling it never reaches, so a B83, an Ivy Mike and a Tsar Bomba are three visibly different
    clouds instead of the same picture
  - **What is deliberately not to scale**, and the only such thing in the model: the cloud is
    drawn at a fifth of its real size with the height halved again, because a real 150 kt cloud
    stands 13 km and would leave the top of the screen; the fireball comes down only half way, so
    it is not lost under it; and the rise is compressed about twelve to one, a strike running
    about a minute from flash to fading. Three named constants in `Core/NuclearCloudDisplay`.
    See [docs/effects](docs/effects) for renders of every stage, and for what was checked against
    the measured tests and the photographs
- **Shock wave** — a blast front races out across the ground following Sedov–Taylor
  (`r ∝ t^0.4`): it leaves at several times the speed of sound and visibly decelerates, reaching
  the destruction radius at an average 540 m/s. Every warhead gets one
- **Distance‑based destruction** — total destruction near ground zero, falling off with distance
- **Fire** for incendiary/thermobaric/nuclear warheads
  - White Phosphorus is a pure incendiary: no crater and almost no blast **at any charge** —
    a bigger charge only spreads the fires further
- **Radioactive fallout** (nuclear ground burst) — persistent soil contamination, expires after 50 in‑game years
- **Missile defense** — name‑detected interceptor buildings engage incoming missiles with realistic
  single‑shot kill probabilities (PAC‑3 / THAAD / Aegis) and a radar that boosts hit chance
- **Explosions & sound** — the fireball is sized from the yield (a 100 kg charge and a 20 t one no
  longer look alike), nuclear mushroom cloud, launch/impact/intercept SFX with 3D falloff

## Required companion assets (important)

The mod detects buildings **by name**. To use missile defense and decontamination you must
subscribe/create building assets whose names contain these keywords:

| Purpose | Name must contain |
|---|---|
| Terminal‑tier interceptor | `PAC3` |
| Mid‑tier interceptor | `THAAD` |
| High‑tier interceptor | `Aegis` (or `イージス`) |
| Radar (boosts intercept chance) | `Radar` (or `レーダー`) |
| Decontamination | `Decontamination` (e.g. "Decontamination facility") |

Build these anywhere; the mod picks them up automatically once completed.

> Note: interceptors engage by altitude band (PAC‑3 0–800 m, THAAD 800–2500 m, Aegis above).
> A **150 kt nuclear air burst** goes off at ~900 m, just above PAC‑3's ceiling, so terminal point
> defense alone cannot stop one — keep a higher layer if you expect them. Smaller yields burst
> lower and do come within PAC‑3's reach.

## How to use

1. Open the **Missile Launch Control** panel (top‑left) or press **F9**.
2. Choose a warhead, set the yield (nuclear kt / conventional kg), and pick air or ground burst.
3. Click **Start Targeting**, then click the map to launch.
4. Place interceptor assets near likely impact areas for defense.
5. Place a "Decontamination facility" in a contaminated area to remove fallout (~5% per in‑game month).

## Companion mod

Works alongside the **NuclearMeltdown** mod: the same "Decontamination facility" also cleans
reactor‑meltdown fallout. (Water treatment plants do **not** decontaminate in either mod.)

## Building from source

- `build.ps1` builds the mod (MSBuild, .NET Framework 3.5 target for Unity 5.6) and deploys the
  DLL plus `Models/` and `Sounds/` assets to the local Addons folder. It needs the game's managed
  DLLs, so it only runs on a machine that owns Cities: Skylines.
- Pure logic lives in `src/MissileDisaster/Core` (UnityEngine‑free) and is covered by xUnit tests
  in `tests/` (`dotnet test`). No game install needed.
- `tools/compile-check` type-checks the particle-effect code against stand-ins for the Unity
  types, so a change to the effects can be caught without the game
  (`dotnet build tools/compile-check/CompileCheck.csproj`). It is a syntax and shape check only —
  a stub compiles whatever is written in it, so it cannot tell you a member exists in Unity 5.6.
- `tools/effect-preview` renders the nuclear effect offline; see [docs/effects](docs/effects).

## License

MIT — see [LICENSE](LICENSE).
