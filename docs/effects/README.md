# Nuclear effect previews

Renders of the nuclear detonation the mod plays, produced by `tools/effect-preview`. The
preview reproduces the particle systems in `Game/Effects/NuclearMushroomFx.cs` — the same
emitter shapes, start sizes, speeds, lifetimes, colour gradients and size curves, and the same
soft round sprite and blend modes from `Game/Effects/ParticleAssets.cs` — so these are the
shapes the game draws, at the dimensions
`Core/NuclearCloudDisplay.cs` gives for the yield, not artists' impressions.

Regenerate with:

```sh
python3 tools/effect-preview/render.py docs/effects     # needs numpy and matplotlib
```

## `stages.png`

The five overlapping stages of a 150 kt groundburst, each framed on its own scale — the
fireball is 400 m across and the cap 7 km, so one frame cannot show both.

## `timeline.png`

The same 150 kt detonation from one camera over its whole 22 s, on a 1 km grid.

## `yield-ceilings.png`

Five yields under the old hard clamps and under the soft ceilings that replaced them. Under the
old clamps everything from about 950 kt upwards came out at exactly the same 8 km cap under a
12 km top: a B83, an Ivy Mike and a Tsar Bomba were the same picture. The soft ceiling keeps
the real figures below the knee and compresses above it, so the size always tracks the yield.

## `cap-birth.png`

Why the canopy is now emitted at the head of the column rather than at the cloud top: at the
top it hung finished in clear air for several seconds before the stem arrived under it.

## `cap-shape.png`

Why the canopy's depth now comes from the cloud top rather than from its own width. Glasstone
puts the base of the cap at about 0.7 of the altitude of its top, so a cap is three tenths of
the column deep at any yield. Sizing its particles off the cap radius instead happens to be
right at 150 kt and is 3.3× too deep by 10 Mt — a ball on a stick rather than a lens.

## Against the measured tests

The cloud model itself holds up well against what was actually photographed and surveyed:

| | yield | cloud top | cap diameter | stem width |
|---|---|---|---|---|
| Castle Bravo, measured | 15 Mt | 40 km | 100 km | 7 km |
| the model's figures | 15 Mt | 32.1 km | 99.5 km | 9.9 km |
| Ivy Mike, measured | 10.4 Mt | 37 km | 161 km | 32 km |
| the model's figures | 10.4 Mt | 29.7 km | 75.1 km | 7.5 km |

Castle Bravo's canopy is matched to better than 1%. Ivy Mike was an outlier even against the
charts the fit is drawn from — it spread twice as wide as the fit says a 10 Mt cloud should.

What is *drawn* then departs from those figures, on purpose, because the playable map is 17 km
across and a 100 km canopy is not something a player can be under. The soft ceiling keeps the
drawn size exact to about 1 Mt and compresses it above:

| yield | cap drawn, against the figures | cloud top drawn |
|---|---|---|
| ≤ 475 kt | 100% | 100% |
| 1.2 Mt | 99% | 92% |
| 10.4 Mt | 60% | 78% |
| 50 Mt | 18% | 59% |

Sources for the measured figures: [Castle Bravo](https://en.wikipedia.org/wiki/Castle_Bravo),
[Ivy Mike](https://nuclearweaponarchive.org/Usa/Tests/Ivy.html), and Glasstone & Dolan,
[*The Effects of Nuclear Weapons* (1977) ch. II](https://atomicarchive.com/resources/documents/effects/glasstone-dolan/chapter2.html).

## Known gaps against photographs

- **No skirt or bell.** Many tests show a cone of condensation sliding down the stem where the
  humid air around it drops below its dew point. Nothing draws one.
- **No true rollover.** A real cap is a vortex ring: the rim curls down and back under itself.
  Here the rim only sags, under a small gravity.
- **No ice cap.** Above the tropopause the water in the cloud freezes into one or more bright
  white domes over the canopy. The cap here is one dust-coloured mass throughout, where
  photographs show it paling to a grey-white as it stabilises.
- **The aspect ratio drifts at high yield.** The ceiling bites the cap radius harder than the
  cloud top, so a 10 Mt cloud is drawn 1.9 times wider than tall where the figures say 2.5 and
  Ivy Mike was 4.4. This is the price of the map being 17 km wide.
