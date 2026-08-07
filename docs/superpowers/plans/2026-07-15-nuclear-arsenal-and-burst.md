# A nuclear weapon catalogue, a typed-in yield, and air or ground burst - implementation plan

> Sources: the Wikipedia list of nuclear weapons, and nukesimulator.com.
> Requested: (1) choose from ten well-known weapons, (2) type the yield in kilotons directly, and
> (3) choose between an airburst and a groundburst.
> The three existing presets - tactical, standard and strategic - are replaced by the catalogue
> plus the typed-in yield.

## The catalogue (ten weapons, in ascending order of kilotons, using published figures)

| # | Name | Yield | Notes |
|---|---|---|---|
| 1 | Little Boy | 15 kt | Hiroshima |
| 2 | Fat Man | 22 kt | Nagasaki |
| 3 | Trinity | 25 kt | the first nuclear test |
| 4 | W87 | 300 kt | Minuteman III |
| 5 | B61 | 340 kt | variable yield; the maximum |
| 6 | W88 | 475 kt | Trident II |
| 7 | B83 | 1,200 kt | among the largest in current US service |
| 8 | Ivy Mike | 10,400 kt | the first hydrogen bomb |
| 9 | Castle Bravo | 15,000 kt | the largest US test |
| 10 | Tsar Bomba | 50,000 kt | the largest ever |

## Airburst and groundburst (following the physics)

- **Groundburst**: a crater and fallout. This is how the nuclear warhead behaved before.
- **Airburst**: no crater and almost no fallout, but the blast and thermal radiation **widen the
  destruction and the fires**, by AirBurstBlastFactor, about 1.35.
  Hiroshima and Nagasaki were airbursts precisely to maximise the area affected. It applies to
  the non-nuclear warheads too: an airburst has no crater but reaches further, a groundburst
  leaves a crater.

## Architecture

- `Core/NuclearWeapons.cs`, new and written test-first: `NuclearWeapon{Name,Kilotons}` and `Catalog`, ten of them in ascending order of kilotons. Pure.
- `Core/NuclearYield.cs`, tidied: drop the three-preset enum and keep only
  `NuclearYields.Multiplier(int kt)` and `StandardKilotons`, where the scale factor is
  cbrt(kt/150). Both the typed-in yield and the catalogue go through that one function.
- `Core/BurstType.cs`, new: the enum `{ Airburst, Groundburst }`.
- `Core/WarheadSpec.cs`: add `WithBurst(BurstType)`, returning a **new struct** where an airburst zeroes the crater and the contamination and multiplies the destruction and fires by 1.35. Immutable.
- `Game/UI/MissileTool.cs`: `CurrentYieldKilotons` defaulting to 150 and `CurrentBurst`
  defaulting to Groundburst, both used at launch through `Multiplier(kt)` and `burst`.
- `Game/MissileManager.cs`: `Launch(target, type, yieldMultiplier, burst)`. The spec is `For(type)`, then `Scaled(mult)` for a nuclear warhead, then `WithBurst(burst)`.
- `Game/UI/MissilePanel.cs`: rework the nuclear yield section.
  - A UIDropDown of the ten catalogue weapons; choosing one fills in its kilotons.
  - A UITextField for the kilotons, an integer of 1 or more. What is typed in wins.
  - Two buttons toggling air against ground, highlighted, and applying to every warhead.
  - Show the current yield, warhead and burst height.

## Testing (test-first)

- `NuclearWeaponsTests`: ten entries, every yield positive, no empty names, ascending order, and the known values - Little Boy at 15 and Tsar Bomba at 50000.
- `NuclearYieldTests`: `Multiplier(150)=1`, the cube-root relationship, monotonic increase and positive values. The enum tests are removed.
- `WarheadSpecTests`: `WithBurst(Ground)` changes nothing; `WithBurst(Air)` zeroes the crater and the contamination and increases the destruction and fires; the original struct is untouched.
- The UI (UIDropDown, UITextField, UIButton) is verified in game.

## Definition of done

- Every Core test is green and the build and deployment succeed.
- In game: the yield varies with the catalogue choice or the typed-in kilotons, and air and ground bursts differ visibly - the crater, the fallout and how far the destruction reaches.
