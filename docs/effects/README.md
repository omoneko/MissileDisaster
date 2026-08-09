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
fireball is 400 m across and the cap 1.4 km, so one frame cannot show both.

## `timeline.png`

The same 150 kt detonation from one camera over its whole 67 s, on a 1 km grid.

## `yield-ceilings.png`

Five yields under the old hard clamps and under the soft ceilings that replaced them. Under the
old clamps everything from about 950 kt upwards came out at exactly the same 8 km cap under a
12 km top: a B83, an Ivy Mike and a Tsar Bomba were the same picture. The soft ceiling keeps
the real figures below the knee and compresses above it, so the size always tracks the yield.

## `cap-birth.png`

Why the canopy is now emitted at the head of the column rather than at the cloud top: at the
top it hung finished in clear air for several seconds before the stem arrived under it.

## `cap-shape.png`

Why the canopy's depth comes from where the cloud stopped rising rather than from its own
width. Sizing the particles off the cap radius made the cap exactly twice as wide as it was
deep at *every* yield — the one thing that cannot be true, since a 20 kt cap is a ball and a
10 Mt cap is a sheet.

## The tropopause, and why a mushroom is mushroom-shaped

Through the troposphere the air gets colder with height, so a fireball that cools as it expands
is still warmer than what surrounds it and keeps climbing. At the tropopause the temperature
stops falling and begins to rise: the cloud goes on cooling as it climbs while the air around it
warms, loses its buoyancy within a kilometre or two, and has nowhere left to go but sideways.
That lid is what spreads the canopy flat — the same one that gives a thunderstorm its anvil.

So the canopy's base is where the cloud stopped rising, and its depth is everything from there
to the top. `NuclearCloudDisplay` puts it at half the cloud top, never above the 11 km
mid-latitude tropopause. That reproduces what was measured:

| | cloud top | cap base | base ÷ top |
|---|---|---|---|
| Ivy Mike, measured | 37 km | ≈ 17 km (tropical tropopause) | 0.46 |
| Castle Bravo, measured | 40 km | ≈ 17 km | 0.42 |
| the model at 10.4 Mt | 23.2 km | 11.0 km | 0.47 |
| the model at 50 Mt | 27.3 km | 11.0 km | 0.40 |
| the model at 15–22 kt | 6.6–7.5 km | half the top | 0.50 |

and it is why the model's canopy is round at 15 kt (0.9× as wide as deep, which is what the
1945 photographs show) and a sheet at 10 Mt (3.7×).

The lid is in the cloud-height fit too, not just the cap. Differentiating
`NuclearCloud.CloudTop`, the height goes as `W^0.33` up to about 20 kt — free rise through the
troposphere — flattens to `W^0.16` in the megaton range as the tropopause brakes it, and steepens
again to `W^0.31` above 10 Mt, where a cloud has the energy to punch through and rise freely in
the stratosphere. Glasstone's charts have that flattening in them, and the fit inherited it.

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

## `cloud-scale.png`

The same 150 kt burst at four values of `NuclearCloudDisplay.CloudScale`, from a camera at the
height and distance the game is actually played at, with a city for a ruler. This is the figure
to look at when deciding how large clouds should be. With `CloudHeightScale` at 0.5 every one of
the four is in frame, so the choice is about how much of the sky a strike should fill rather
than about whether it fits.

## `nineteen-forty-five.png`

The two bursts there are famous photographs of, at the yield and burst height the mod would
fly them at, for holding the render against the real thing.

## What the photographs changed

Reading the Hiroshima and Nagasaki photographs against the render turned up three things the
figures alone had not:

- **A cloud is two colours, not one.** Both are airbursts, and in each a brilliant white
  cauliflower cap stands over a dark brown dust column. The canopy was one dust colour
  throughout. It is now born the colour of the dust it came up with and pales as the water in
  it condenses — to clean white for an airburst, stopping short of it for a groundburst, which
  has ground to lift and fallout to carry.
- **The column was drawn 1.9× too wide.** Its particles were emitted across the whole stem
  radius and then grown nearly as wide again, so at 15 kt the column came out as wide as its
  own cap. Where a particle starts and how large it grows now add up to the stem radius, the
  same budget the canopy uses.
- **The column came off the ground.** Its particles never stopped climbing and emission stopped
  when the cloud finished rising, so the column drained away upwards and out through the top of
  its own cap, leaving the canopy over clear air. It now stops under the canopy and is fed for
  as long as the cap is up.

## Playability: what is deliberately not to scale

Three numbers in `NuclearCloudDisplay` are admissions rather than figures, and they are the only
ones in the model that are:

- **`CloudScale = 0.20`** — the cloud is drawn at a fifth of its real size. A real 150 kt cloud
  stands 13.2 km, which from a game camera is a column leaving the top of the screen rather than
  a mushroom. It is applied to cap, stem and height together, so every proportion checked
  against the photographs survives it. Raise it for taller clouds; see `cloud-scale.png`.
- **`CloudHeightScale = 0.50`** — the height alone is halved again on top of that. Even at a
  fifth of its real size a cloud is a tall thing, because the figures make one taller than it is
  wide at every yield below a megaton, and height is what runs off the top of a screen. This is
  the one proportion checked against the photographs that is knowingly broken: the canopy comes
  out about twice as flat as the real thing, since its depth is measured from the cloud top and
  the top is what moved. Set it to 1 to have the cloud back in proportion at twice the height.
- **`FireballScale = 0.50`** — the fireball comes down only half way, so it keeps two and a half
  times its share of a cloud that has been brought down to a fifth. At full size it would be
  nearly as wide as a compressed 50 Mt canopy; in step with the cloud it is a spark. At 0.50 it
  reads as about a quarter to a third of the canopy's width at every yield, against the tenth to
  a seventh it really is.

The timings are stretched for the same reason. The rise is compressed 12:1 rather than the
real ten minutes, and the canopy then stands for 35 to 60 seconds, so a 150 kt strike runs
about 67 seconds from flash to fading — long enough to watch the dust well up, the column
climb, and the cap form and hold.

## Known gaps against photographs

- **No low-level layer.** Both 1945 photographs show a broad flat sheet of cloud and smoke
  spread across the ground far wider than the column. The ground dust here is a skirt about
  twice the stem's width; the blast front that crosses the ground is drawn, but it passes.
- **No skirt or bell.** Many tests show a cone of condensation sliding down the stem where the
  humid air around it drops below its dew point. Nothing draws one.
- **No true rollover.** A real cap is a vortex ring: the rim curls down and back under itself.
  Here the rim only sags, under a small gravity.
- **The aspect ratio drifts at high yield.** The ceiling bites the cap radius harder than the
  cloud top, so a 10 Mt cloud is drawn 1.9 times wider than tall where the figures say 2.5 and
  Ivy Mike was 4.4. This is the price of the map being 17 km wide.
