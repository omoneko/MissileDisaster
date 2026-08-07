# Differentiating the warhead types - implementation plan

> Where things stand: `WarheadType` already defines five - Conventional, Cluster,
> WhitePhosphorus, Thermobaric and Nuclear - but `WarheadSpec.For` returns the conventional
> figures for all of them, so they land identically. This plan differentiates them.
> **The only impact APIs available** are `DisasterHelpers.MakeCrater` and
> `DisasterHelpers.DestroyStuff`; there is no API to start a fire directly, and radioactive
> contamination would be a large port in itself. The differences are therefore expressed through
> the crater's shape, the destruction radius and how the submunitions scatter.
> The nuclear warhead only sets its `Contaminates` flag here; the actual contamination grid is
> left to a phase of its own later.

## Architecture

- `Core/WarheadSpec.cs`, extended and written test-first: the per-warhead table of numbers.
  New fields: `Type`, `SubmunitionCount` (1 for a single impact, more to scatter),
  `SpreadRadius` (how far the submunitions scatter), `RaiseCraterEdges` (whether to raise the
  rim) and `Contaminates` (nuclear only; the real contamination comes later). The three existing
  values are kept.
- `Core/SubmunitionScatter.cs`, new and written test-first: a pure function placing the
  submunitions **deterministically**, with no randomness, so the result is reproducible.
  The arrangement is phyllotactic: `angle=k*137.5 degrees` and
  `r=SpreadRadius*sqrt((k+0.5)/count)`, returning `Offset2[]`.
  A count of 1 or less gives a single point at the origin, and no point falls outside
  SpreadRadius; the square root is what spreads them evenly.
- `Game/ImpactResolver.cs`, changed: `spec.SubmunitionCount<=1` lands as a single impact, as
  today; above that, a small crater and area destruction are applied at each scatter point.
  `spec.RaiseCraterEdges` is passed to `MakeCrater`'s raiseEdges.
  A nuclear warhead only logs `Contaminates` for now. The simulation-thread contract is
  unchanged.

## The figures per warhead (provisional, for balancing; metres)

| Warhead | Crater R/D | Destroy R | Submunitions | Spread R | Raise rim | Contaminates | Intent |
|---|---|---|---|---|---|---|---|
| Conventional | 60 / 16 | 120 | 1 | 0 | no | no | unchanged; the baseline |
| Cluster | 18 / 5 | 45 each | 9 | 160 | no | no | wide, shallow damage at many points |
| WhitePhosphorus | 10 / 3 | 40 each | 12 | 140 | no | no | an incendiary spread wide; the fires are approximated by scattered destruction |
| Thermobaric | 70 / 10 | 220 | 1 | 0 | yes | no | the greatest destruction, flattening buildings with overpressure |
| Nuclear | 150 / 40 | 380 | 1 | 0 | yes | yes | an enormous crater and wide devastation, plus the contamination flag |

## Testing (test-first, red then green)

- `WarheadSpecTests`: each warhead returns the expected fields; Conventional keeps its existing
  values; only Nuclear contaminates; Cluster and white phosphorus have SubmunitionCount above 1
  and a positive SpreadRadius; Thermobaric and Nuclear raise the crater rim.
- `SubmunitionScatterTests`: the count matches; no point falls outside SpreadRadius; the same
  input gives the same output, i.e. it is deterministic; a count of 1 or less gives a single
  point at the origin; and a SpreadRadius of 0 puts every point there.
- `ImpactResolver` depends on DisasterHelpers and is verified in game: fire each warhead and look at how the damage differs.

## Definition of done

- Every Core test is green, including the new ones, and the build and deployment succeed.
- The five are visibly different in game: one large crater, many points over a wide area, overpressure flattening everything, and outright devastation.
- Starting real fires and the radioactive contamination grid are explicitly out of scope here and belong to a later phase.
