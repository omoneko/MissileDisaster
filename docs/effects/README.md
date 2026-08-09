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
