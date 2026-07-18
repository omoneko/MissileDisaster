# Missile Disaster

A Cities: Skylines (2015 / base game) mod that lets you launch missiles with several warhead
types at any location — and defend against them. Nuclear strikes leave realistic, persistent
radioactive fallout that you can clean up with a dedicated decontamination facility.

> Base game only (no DLC required). If the **Natural Disasters** DLC is present, the vanilla
> meteor impact effect is used for explosions; otherwise a built‑in particle fireball is used.

## Features

- **5 warhead types** with distinct impact behavior:
  - Conventional, Cluster, White Phosphorus (incendiary), Thermobaric (overpressure), Nuclear
- **Adjustable yield**
  - Nuclear: pick from 10 real weapons (Little Boy … Tsar Bomba) or type a custom **kt** value
  - Non‑nuclear: type a custom charge in **kg TNT**
  - Blast radii follow the real cube‑root scaling law (`radius ∝ yield^(1/3)`)
- **Air burst / ground burst**
  - Ground burst: forms a crater and destroys roads/bridges/foundations; leaves radioactive fallout (nuclear)
  - Air burst: no crater, wider blast/fire, and roads / water pipes / metro / foundations survive
- **Distance‑based destruction** — total destruction near ground zero, falling off with distance
- **Fire** for incendiary/thermobaric/nuclear warheads
- **Radioactive fallout** (nuclear ground burst) — persistent soil contamination, expires after 50 in‑game years
- **Missile defense** — name‑detected interceptor buildings engage incoming missiles with realistic
  single‑shot kill probabilities (PAC‑3 / THAAD / Aegis) and a radar that boosts hit chance
- **Explosions & sound** — scaled meteor blast, nuclear mushroom cloud, launch/impact/intercept SFX with 3D falloff

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
  DLL plus `Models/` and `Sounds/` assets to the local Addons folder.
- Pure logic lives in `src/MissileDisaster/Core` (UnityEngine‑free) and is covered by xUnit tests
  in `tests/` (`dotnet test`).

## License

MIT — see [LICENSE](LICENSE).
