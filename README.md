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
  - The mushroom is a **cloud of soft smoke puffs computed along the vortex-ring flow a real
    one has** (`Core/CloudPuffs`): the cap's puffs circulate around a torus — up the inside,
    out over the top, down the outside, in underneath, the roll that folds a real cap into
    cauliflower — while the column's puffs climb a skirt‑waist‑throat profile from the ground
    into the cap and recycle, an endless conveyor. Every puff is placed every frame
    (`SetParticles`), so the crowd holds the mushroom silhouette while visibly boiling; the
    boil slows once the cloud stands and all but freezes as it fades. The cap goes on
    **spreading sideways for the whole shot** — the updraft is still feeding it — while the
    column, stopped by the tropopause, does not follow it outwards. Puffs come in a wide
    range of sizes weighted towards the small end — a few big lobes with many smaller ones
    packed around them, the way a real cloud is built. Fire glows through the
    folds for the first seconds; each strike boils its own way from its own seed. **Smoke also
    rises off the burning city across the burn field** and is gently drawn in toward the central
    updraft. The end is a **staggered dissolve** over 12–20 s — longer than the rise — with the
    column shredding first, the cap loosening after it, and the fire smoke outlasting both
  - An **airburst's cloud is white**, the way both 1945 photographs show; a groundburst's keeps
    its dirt
  - Ground dust drawn up by the afterwinds
  - **The size never stops following the yield.** Nothing is hard‑clamped: every dimension is
    exact up to the point the map can still carry it and then compressed smoothly towards a
    ceiling it never reaches, so a B83, an Ivy Mike and a Tsar Bomba are three visibly different
    clouds instead of the same picture
  - **What is deliberately not to scale**: the cloud is drawn at 6% of its real size — one
    number for width and height alike, so the drawn shape is the real shape — with two declared
    departures from it: the cap alone is spread a further 1.3× sideways, and the drawn height is
    bounded at 2 km (the airburst ceiling stays at 1 km; the two are decoupled). The fireball
    comes down less far, so it is not lost under the cloud. Time is compressed hard: the cloud
    forms in seconds and the whole 150 kt shot is about 24 s, because the playtest verdict on a
    slower cloud was that it was a wait, not a spectacle. The knobs are named constants in
    `Core/NuclearCloudDisplay`
- **Shock wave** — a blast front races out across the ground following Sedov–Taylor
  (`r ∝ t^0.4`): it leaves at several times the speed of sound and visibly decelerates, reaching
  the destruction radius at an average 540 m/s. Every warhead gets one. Behind it rolls a
  **concentric wall of dust** — a tsunami of earth the front tears off the ground, starting a
  beat after the blast passes, piling up and climbing as it spreads, and outlasting the front
  itself
- **Blast debris** — the rubble of whatever stood at ground zero is thrown out and up on real
  ballistic arcs, tumbling on all three axes and landing back across the city, with the dust it
  carries. The pieces are **real geometry, lit by the scene** — low‑poly chunks generated to the
  proportions of the game's own rock props (measured off them: ~31–58 triangles, 4.0 × 1.8 × 2.6 m,
  flat rather than cubic), not billboards. The launch speed and hang time are solved from how far
  the pieces should land, so a 1 t charge throws its wreckage 30 m and a strategic warhead throws
  it 620 m — the range chosen so the longest arc still lands before the chunk's life runs out
- **Trees catch fire** inside the thermal ring, fiercest at the centre and petering out at the
  edge, up to a few hundred at a time so a strike costs the simulation about what a natural
  disaster does. *(Needs the Natural Disasters DLC — the game's own tree‑burning API is gated on
  it. Without the DLC nothing happens; everything else in the mod is unaffected.)*
- **Distance‑based destruction** — total destruction near ground zero, falling off with distance
- **Fire** for incendiary/thermobaric/nuclear warheads
  - White Phosphorus is a pure incendiary: no crater and almost no blast **at any charge** —
    a bigger charge only spreads the fires further
- **Random strikes (disaster mode)** — **off by default.** Missiles never fall on their own
  until you tick it in Options; once on, the first strike in each city chirps to say what hit
  it and where the switch is
- **Radioactive fallout** (nuclear ground burst) — persistent soil contamination, expires after 50 in‑game years
- **Missile defense** — name‑detected interceptor buildings engage incoming missiles with realistic
  single‑shot kill probabilities (PAC‑3 / THAAD / Aegis) and a radar that boosts hit chance
- **Runs on the game's clock** — every effect the mod draws advances on simulation time, not
  the wall clock: pause the game and the cloud, the shock wave, the trails and the sounds all
  freeze mid-flight; run at triple speed and they run at triple speed, the way the base game's
  own effects do. Sounds are held silent rather than pitched up, which is also what vanilla does
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

## Handover

[docs/HANDOVER.md](docs/HANDOVER.md) — the state of the nuclear-effect work: the tuning
constants and what they currently produce, what is verified and what is not, and the open
question of whether the DLL the game is running is the one that was built.

## Checking which build is running

**Options → Mods → Missile Disaster** has a *Build check* group at the top showing two lines:

```
150 kt draws: cloud top 781 m, cap 431 m wide, fireball 131 m across, rise 31.1 s, screen top 1000 m
Loaded from: C:\Users\...\Addons\Mods\MissileDisaster
```

The numbers are computed by the code that is running, not written down, so they are a
fingerprint of the build: a cloud top of about **781 m** is a current build, **13245 m** is the
original one. The path is where the game actually loaded the DLL from, which settles the case
where a Steam Workshop subscription is shadowing a local build.

The same line goes to the game log at load and at each detonation. The log is written by Unity,
not by the game's own folder, so it is one of:

```
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities_Skylines\Player.log
<Steam>\steamapps\common\Cities_Skylines\Cities_Data\output_log.txt
```

## License

MIT — see [LICENSE](LICENSE). All assets are original.
