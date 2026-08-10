# The vanilla meteor impact effect, read out of the game's own assets

The Natural Disasters content is **baked into the base install** — there is no separate DLC
depot; buying the DLC only unlocks it. That is why Steam had nothing to download, and it is
what makes this reference possible on any install: the prefabs sit in
`Cities_Data/sharedassets55.assets`, readable with UnityPy.

Extracted 2026-08-10 with UnityPy 1.25.3 (Unity 5.6 serialized ParticleSystems).

## The effect family

`Huge Explosion Effect` (what `MeteorAI.m_impactEffect` points at), `Large Explosion Effect`,
`Meteor Effect` (the falling trail), each a `MultiEffect` of ParticleSystems plus a Light.

## The mushroom recipe (Huge Explosion)

| system | life (random 0..x) | launch speed | clamp | size | gravity | size over life | colour over life | peak alpha |
|---|---|---|---|---|---|---|---|---|
| Particles (flash) | 4 s | 150 m/s hemisphere | 30 m/s | up to 100 m | 0 | 0.41 → 1.0 | white → (0.64,0.40,0.37) | 0.68 at 7% of life |
| Particles 2 (cloud) | 6 s | 75 m/s | 20 m/s | up to 100 m | **−1.0** | 0.41 → 1.0 | white → (0.69,0.42,0.38) | 0.68 |
| Smoke (linger) | 10 s | 4 m/s | 20 m/s | up to 100 m | **−0.5** | — | white | **0.13** |
| Spark (debris) | 10 s | 800 m/s | 75 m/s | 2 m | +1.0 | 0.70 → 1.0 | white → warm | 1.0 |

Whole effect: duration 15 s. Renderer: billboards, `maxParticleSize` left at the 0.5 default.

## What this confirms about the mod's cloud

- **No uniform fade exists in vanilla either.** Every particle carries its own random
  lifetime (0–4/0–6/0–10 s) and its own alpha ramp to zero: the cloud dies particle by
  particle, staggered. The mod's per-puff staggered dissolve is the same idea, made
  deterministic.
- **Fast burst, hard clamp.** Launched at 75–150 m/s and immediately damped to 20–30 m/s —
  the same leap-then-brake the mod's shock ring and fireball use.
- **Buoyancy.** The cloud layers rise their whole life (negative gravity), which is what the
  mod's climb curves do explicitly.

## What was adopted

- **Growth over life** (every vanilla system swells 0.4→1.0): the mod's column and fire-smoke
  puffs now grow as they climb, which reads as smoke expanding while it rises and cools.
- **The palette**: the game's own aged-smoke brown (0.69, 0.42, 0.38) informs the fire smoke's
  dust colour, so the mod's smoke sits in the game's palette.

## What was deliberately not adopted

- **The thin alphas** (0.68 flash, 0.13 lingering smoke). The vanilla cloud is translucent;
  the playtest verdict on a see-through cloud was unambiguous, and the mod holds its measured
  0.997 cap opacity instead.
- **The brown-aging cap.** A meteor cloud is dirt and stays dirty; a nuclear cap turns white
  as its water condenses (both 1945 photographs). The mod keeps its physics colours for the
  cap and uses the vanilla brown only where dirt belongs - the column and the fire smoke.
- **`maxParticleSize` 0.5.** Vanilla lets the renderer clamp big sprites at close zoom; the
  mod raises it so the cloud does not shrink exactly when the camera comes to admire it.
