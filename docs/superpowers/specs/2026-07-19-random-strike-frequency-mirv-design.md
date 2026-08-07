# Random strikes: following the disaster frequency, plus simultaneous MIRV impacts - design

Written: 2026-07-19
Target: the Missile Disaster mod (Cities: Skylines 2015 / Unity 5.6)
Branch: feature/phase2-flight-intercept-core

## 1. Purpose and background

The random strikes are currently a stopgap that fires one missile at a fixed interval of real
time. Two things are to be rebuilt as the user asked:

1. **The frequency model** - tie it to how often vanilla natural disasters occur, and restart the
   missile countdown every time another natural disaster happens, so missiles land in the gaps
   between disasters.
2. **The impact pattern** - keep the current one-at-a-time behaviour and add a **MIRV** pattern
   that lands several at once.

## 2. How it works today (before the rebuild)

- `Game/Simulation/MissileThreadingExtension.cs`: `OnUpdate` accumulates
  `_randomTimer += realTimeDelta` and calls `RandomStrike.Fire()` once at
  `>= RandomInterval`, which defaults to 180 seconds. It is based on real time, unrelated to
  other disasters, and fires a single missile.
- `Game/RandomStrike.cs`: `Fire()` launches exactly one, at a building or a random position.
- `Game/ModSettings.cs`: `RandomEnabled` (0/1), `RandomIntervalSeconds` (default 180) and
  `RandomWarhead` (0 for random, 1 to 5 to fix it).

## 3. Technical groundwork (confirmed against the game's DLLs)

- These fields of `DisasterManager` are all **public**, so they can be read directly given the
  existing Assembly-CSharp reference - **no Harmony needed**.
  - `m_randomDisastersProbability : float` - how often natural disasters occur, from the map and
    the difficulty.
  - `m_randomDisasterCooldown : int` - the countdown to the next disaster, reset when one occurs.
  - `m_disasterCount : int` and `m_disasters : FastList<DisasterData>` - how many disasters are
    active and what they are.
- **The flight time is the same for every missile**: `Missile`'s `_groundDistance` equals
  `ModConfig.ApexHorizontalOffset` (a const 2200 m), the drop height is `ApexAltitude` (a const
  4000 m) and the speed is `MissileSpeed` (a const 900). The bearing is shared too
  (`IncomingBearingDegrees`, a const 315). The time to impact is therefore independent of the
  target, so **launching on the same frame means landing at the same moment** - no
  synchronisation is needed.

## 4. Design

### 4.1 The frequency model: sharing the vanilla disaster slot

**Approach**: poll `DisasterManager.instance`, read-only. Rejected alternatives were a Harmony
hook (needless complexity and a source of conflicts) and registering a real DisasterInfo
(a missile is not a disaster type, it would depend on the DLC, and it is overkill).

**The scheduler's pure logic** is separated into `Core/StrikeScheduler.cs`, with no UnityEngine
dependency, and tested with xUnit. It is driven by game time
(`SimulationManager.instance.m_currentGameTime`), so it follows the game speed and the pause
naturally.

State, touched by the simulation thread alone:
- `double _countdownDays` - in-game days until the next missile strike.
- `int _lastDisasterCount` - the `m_disasterCount` observed last time.
- `bool _initialized`.

The method, with its dependencies injected for testing:
```
// Called once per simulation tick; true means it fires this tick.
bool Advance(
    double gameDaysDelta,   // in-game days elapsed since the last call
    int    disasterCount,   // the current m_disasterCount
    float  probability,     // the current m_randomDisastersProbability (>= 0)
    double freqMultiplier,  // the setting, 0.25 to 3.0
    double rng)             // a value in [0,1), injected, to vary the interval
```

The logic:
1. First call: `_lastDisasterCount = disasterCount; _countdownDays = NextInterval(...);
   _initialized = true; return false;`
2. `disasterCount > _lastDisasterCount` (**another natural disaster occurred**): reset with
   `_countdownDays = NextInterval(...);`, set `_lastDisasterCount = disasterCount;` and
   `return false;`
3. `disasterCount < _lastDisasterCount` (a disaster ended): `_lastDisasterCount = disasterCount;`
   and carry on without resetting.
4. Otherwise: `_countdownDays -= gameDaysDelta; if (_countdownDays <= 0) { _countdownDays =
   NextInterval(...); return true; } return false;`

`NextInterval(probability, freqMultiplier, rng)`, the normalised form as implemented:
- `pf = ProbabilityFactor(probability)` - `probability / RefProbability` clamped to
  `[ProbFactorMin, ProbFactorMax]`. Even at a probability of about 0 - **a map with disasters
  disabled** - it bottoms out at `ProbFactorMin`, giving a finite interval, so the feature does
  not die.
- `mean = BaseIntervalDays / (freqMultiplier * pf)`: a higher frequency setting shortens the
  interval, and so does a higher disaster frequency.
- `interval = mean * (0.5 + clamp01(rng))`, a natural spread of 0.5x to 1.5x.
- `Clamp(interval, MinIntervalDays, MaxIntervalDays)`.

Constants, defined in `Core` and tuned by observation in game. They are pure logic, so changing
the numbers needs no test changes:
- `BaseIntervalDays = 20`: the baseline missile interval in in-game days at
  `freqMultiplier = 1` and `probability = RefProbability`. This is the main knob to turn.
- `RefProbability = 0.05`: the `m_randomDisastersProbability` assumed to be typical. Even if the
  real value differs, `ProbabilityFactor`'s clamp keeps the interval sane.
- `ProbFactorMin = 0.25`, `ProbFactorMax = 4.0`, `MinIntervalDays = 2`, `MaxIntervalDays = 365`,
  and `Epsilon`.

Note that the absolute scale of `m_randomDisastersProbability` is undocumented, so `probability`
is normalised against `RefProbability`. The design is therefore **proportional to the vanilla
disaster frequency** rather than "1x means exactly the vanilla rate";
`BaseIntervalDays` and `RefProbability` are calibrated in game.

**Self-triggering**: a missile impact may increase `m_disasterCount` through `DisasterHelpers`.
That is harmless: `_countdownDays` has already been reset at the moment of firing, so the next
tick merely resets it again as "another disaster occurred". It cannot fire twice. The actual
behaviour will be confirmed during implementation and noted here.

### 4.2 The impact pattern: Single, MIRV or Random

`RandomStrike` is extended. **When** it fires is decided by the scheduler in 4.1; **what**
pattern it fires is chosen at that moment from the `AttackPattern` setting.

- **Single** (the default): one missile, as today.
- **MIRV**: **3 to 6** missiles (`UnityEngine.Random.Range(3,7)`) launched **on the same frame**,
  so they land together. Each draws its own target - a different building or point across the
  city - by calling the existing `TryRandomBuilding` per missile, which spreads them over the
  built-up area.
- **Random**: drawn each time, mostly single with the occasional MIRV -
  **70% single, 30% MIRV**.

**Warheads**: each missile calls the existing `PickWarhead()`. With Warhead set to Random (0),
each draws its own; fixed at 1 to 5, they all share the type. The burst stays fixed at
`BurstType.Groundburst`, as today.

`RandomStrike`'s public API:
```
static void FireStrike();   // reads the AttackPattern setting and runs Single, MIRV or Random
static void FireOne();      // one missile; internal, equivalent to the existing Fire
```

### 4.3 Thread boundary

- **Simulation thread** (`OnAfterSimulationTick`): computes `gameDaysDelta` from the difference
  in `m_currentGameTime.Ticks`, reads `DisasterManager` and advances
  `StrikeScheduler.Advance(...)`. On `true` it raises a request flag, a lock-protected bool.
- **Main thread** (`OnUpdate`): if the flag is raised, clears it and runs
  `RandomStrike.FireStrike()`. Creating the GameObjects, drawing the warhead and choosing the
  target all happen on the main thread.
- While `RandomEnabled` is false the scheduler is not advanced - it is reset to
  `_initialized = false` - and no flag is raised.

### 4.4 The settings UI (`OnSettingsUI` in `Game/Mod.cs`)

The "Random missile strikes" group:
- `Enable random strikes` (existing, off by default).
- ~~`Interval between strikes (seconds)`~~ is removed and replaced by a
  **`Strike frequency (x natural disaster rate)`** slider, 0.25 to 3.0 in steps of 0.25,
  defaulting to 1.0.
- **`Attack pattern`** dropdown (**Single / MIRV / Random**), new, defaulting to **Single**.
- `Warhead` (existing).

### 4.5 Persisting the settings (`Game/ModSettings.cs`)

- `RandomIntervalSeconds` is dropped. The old key remaining in the settings file is harmless.
- Added `StrikeFrequencyPct : SavedInt`, 25 to 300, defaulting to 100, where
  `freqMultiplier = value / 100.0`. It is stored as a percentage in a SavedInt to avoid
  SavedFloat.
- Added `AttackPattern : SavedInt` (0 Single, 1 MIRV, 2 Random), defaulting to 0.
- `RandomEnabled` and `RandomWarhead` are unchanged.
- Helpers for reading them: `StrikeFrequency` (double, Pct/100) and `AttackPatternValue` (int).

## 5. Files changed

- New `Core/StrikeScheduler.cs` - the pure logic, Advance and NextInterval.
- New `tests/.../StrikeSchedulerTests.cs` - xUnit.
- `Game/ModSettings.cs` - drop RandomIntervalSeconds, add StrikeFrequencyPct and AttackPattern.
- `Game/Simulation/MissileThreadingExtension.cs` - drop the real-time timer; drive the
  DisasterManager-linked scheduler on the simulation tick and raise the flag. OnUpdate does
  nothing but consume it.
- `Game/RandomStrike.cs` - FireStrike branching on the pattern, FireOne, and the MIRV salvo.
- `Game/Mod.cs` - the replaced UI: the strike frequency slider and the attack pattern dropdown.

## 6. Testing

`StrikeScheduler` is pure, and is covered thoroughly with xUnit:
- the first call does not fire and initialises `_lastDisasterCount`
- an increase in `disasterCount` resets the countdown without firing
- a decrease in `disasterCount` does not reset it
- accumulating elapsed days until `_countdownDays <= 0` fires and sets a new interval
- a higher `freqMultiplier` shortens the mean interval, with 3x about a third of 1x
- a higher `probability` shortens the mean interval
- a `probability` of about 0 falls back and still yields a finite interval
- the interval is clamped to `[Min, Max]`
- an `rng` of 0 and of nearly 1 span the 0.5x to 1.5x range

The simultaneous MIRV impacts, the thread boundary and the UI are verified in game rather than
by automated tests.

## 7. Out of scope (YAGNI)

- Explicitly synchronising the MIRV impact times, unnecessary because the flight time is
  constant.
- Randomising the burst type.
- A slider for the number of MIRV missiles; the 3 to 6 range is fixed for now.
- Registering as a real DisasterInfo and appearing in the disaster panel.

## 8. Compatibility and release

- The save format does not change; only settings keys are added, which is backwards compatible.
- After implementing, verify the frequency, the simultaneous MIRV impacts and the options UI in
  game, then publish the update to the Workshop.
