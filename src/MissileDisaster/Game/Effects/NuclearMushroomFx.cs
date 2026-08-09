using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// A nuclear detonation, built to the figures in MissileDisaster.Core.NuclearCloud rather
    /// than to taste. Main thread only.
    ///
    /// It runs as the real thing does, in five overlapping stages:
    ///
    ///  1. the fireball, at the point of burst. It is born small and blindingly white, swells to
    ///     its full radius over seconds - ten of them at 1 Mt - and cools through yellow and
    ///     orange to a dull red as it rises off the ground
    ///  2. the condensation cloud, the white dome that flashes into being a second or two behind
    ///     the front where the air rarefies behind the shock, and is gone within another second
    ///  3. the dust the afterwinds tear off the ground and feed into the base of the column
    ///  4. the stem, climbing at the rate the cloud really climbs, narrow against the cap - half
    ///     its width at 20 kt, a seventh of it in the megaton range
    ///  5. the cap, which swells at the top into a canopy of the stabilised cloud radius and
    ///     rolls over at the rim
    ///
    /// The one liberty taken is time. A real cloud takes about ten minutes to stabilise, so the
    /// rise is compressed by roughly twenty-five to one and held inside a range that is watchable
    /// without the player having to wait. Every dimension is the real one.
    ///
    /// Every size and duration comes from MissileDisaster.Core.NuclearCloudDisplay, which applies
    /// the engineering ceilings. They are soft: a bigger warhead is always a bigger cloud, right
    /// up to Tsar Bomba, rather than everything above a megaton coming out the same size.
    /// </summary>
    public static class NuclearMushroomFx
    {
        // The condensation dome forms out where the shock has passed, well beyond the fireball,
        // and lasts barely a second.
        private const float CondensationRadiusFactor = 2.6f;
        private const float CondensationLifetime = 1.3f;

        // How far up the column is when the cap starts to swell out of its head, as a fraction
        // of both the cloud top and the rise time. The two have to be the same number, or the
        // canopy appears somewhere the column is not.
        private const float CapEmergeFraction = 0.55f;

        // The tropopause, and the whole reason a mushroom is mushroom-shaped.
        //
        // Through the troposphere the air gets colder with height, so a fireball that cools as
        // it expands is still warmer than what surrounds it and keeps climbing. At the
        // tropopause the temperature stops falling and begins to rise, and the cloud runs into
        // a lid: it goes on cooling as it climbs while the air around it warms, so its buoyancy
        // is gone within a kilometre or two and it has nowhere left to go but sideways. That is
        // what spreads the canopy out flat - the same lid that gives a thunderstorm its anvil.
        //
        // So the base of the cap is not a fixed fraction of anything. It is the tropopause, for
        // any cloud with the energy to punch through - and about half the cloud's height for a
        // small one that tops out inside the troposphere and stays a rising ball. Both are
        // Glasstone's; the two together are why a 20 kt cap is round and a megaton cap is a
        // sheet. A fixed fraction gets a 20 kt cap half again too flat and a 10 Mt cap twice too
        // flat, which is exactly what it was doing.
        //
        // 11 km is the mid-latitude tropopause, where most of the charts the model is fitted to
        // were measured. It is higher over the tropics - the Pacific tests punched through at
        // nearer 17 km - which is part of why their canopies spread wider than the fit expects.
        private const float TropopauseAltitude = 11000f;

        // How far up the cloud the canopy's underside sits.
        private static float CapBase(float top)
        {
            float half = top * 0.5f;
            return half < TropopauseAltitude ? half : TropopauseAltitude;
        }

        // How many particles the column is allowed at once. It has to stand for the whole shot,
        // so this is what its emission rate is solved from rather than a rate being picked and
        // the ceiling then quietly starving it.
        private const int StemMaxParticles = 900;

        // The floor on how long the canopy lingers at the top. A cloud that took longer to climb
        // lingers for longer still, or a strategic cap starts fading while its own stem is still
        // rising into it.
        private const float CapLifetimeMin = 18f;

        // How far into the canopy's depth the column runs before it is spent.
        private const float StemIntoCapFraction = 0.3f;


        // Colours, following how a real detonation looks rather than a palette: white hot, then
        // sodium yellow, then orange, then the brown of nitrogen dioxide and lofted earth.
        private static readonly Color FireballCore = new Color(1f, 0.99f, 0.94f, 1f);
        private static readonly Color FireballMid = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color FireballEdge = new Color(1f, 0.42f, 0.10f, 1f);
        private static readonly Color FireballCool = new Color(0.42f, 0.13f, 0.05f, 1f);
        private static readonly Color Condensation = new Color(0.96f, 0.97f, 1f, 0.42f);
        private static readonly Color DustLight = new Color(0.55f, 0.49f, 0.40f, 0.75f);
        private static readonly Color DustDark = new Color(0.32f, 0.28f, 0.23f, 0.75f);
        private static readonly Color CapCool = new Color(0.24f, 0.23f, 0.22f, 0.72f);

        // The canopy's colour. A cloud is white because the water in it has condensed out, and
        // brown because of what it tore off the ground - and which of the two wins is decided by
        // the burst height. Both 1945 photographs are airbursts, and in each the cap is a
        // brilliant white cauliflower over a dark dust column; a groundburst - Castle Bravo, and
        // the fallout that came with it - keeps its dirt.
        // So the canopy is born the colour of the dust it came up with and pales as it
        // stabilises, and the burst height sets how far it gets. The particle is the pale vapour
        // and the gradient below tints it, since a colour curve can only ever darken.
        private static readonly Color CapVapour = new Color(0.90f, 0.91f, 0.93f, 0.72f);
        private static readonly Color CapVapourShade = new Color(0.70f, 0.71f, 0.74f, 0.72f);
        private static readonly Color CapTintDust = new Color(0.45f, 0.36f, 0.29f, 1f);    // at birth
        private static readonly Color CapTintAir = new Color(0.62f, 0.57f, 0.52f, 1f);
        private static readonly Color CapSettledGround = new Color(0.86f, 0.83f, 0.79f, 1f); // still dirty
        private static readonly Color CapSettledAir = new Color(1f, 1f, 1f, 1f);             // clean water

        /// <summary>
        /// Plays the whole detonation. groundZero is the spot on the ground the cloud rises from,
        /// detonation is where the warhead actually went off - the same point for a groundburst,
        /// the burst altitude above it for an airburst - kilotons is the yield everything is
        /// built from, and airburst decides how dirty the canopy stays. A yield of zero or less
        /// falls back to the 150 kt baseline.
        /// </summary>
        public static void Play(Vector3 groundZero, Vector3 detonation, float kilotons, bool airburst)
        {
            try
            {
                NuclearCloudDimensions d = NuclearCloudDisplay.For(kilotons);

                // How long the cloud is up for, defined once: the canopy lingers the longest, so
                // it sets the shot, and the column has to be fed for all of it.
                float capLifetime = Mathf.Max(CapLifetimeMin, d.RiseSeconds * 0.8f);
                float showSeconds = d.RiseSeconds * CapEmergeFraction + capLifetime;

                // Where the canopy sits: from its base up to the cloud top, with its particles
                // centred half its depth below that top so the cloud ends where the figures say
                // it does. The column climbs to the underside and stops there.
                float capBase = CapBase(d.CloudTop);
                float capDepth = d.CloudTop - capBase;
                float capCentre = d.CloudTop - capDepth * 0.5f;
                // The column runs a little way into the canopy's underside rather than stopping
                // dead at it. A round billboard fades from its middle outwards, so a cap whose
                // particles sit at its centre only looks solid across the middle of its depth:
                // a column that stops at the nominal underside stops short of anything visible,
                // and leaves a gap between the two. The stem's own fade is what keeps this from
                // drawing a dark line up the front of the cap.
                float stemTop = capBase + capDepth * StemIntoCapFraction;

                CreateFireball(detonation, d.FireballRadius, d.FireballSeconds);
                CreateCondensationDome(detonation, d.FireballRadius * CondensationRadiusFactor, d.FireballSeconds);
                CreateGroundDust(groundZero, d.StemRadius, d.RiseSeconds);
                CreateStem(groundZero, d.StemRadius, d.CloudTop, stemTop, d.RiseSeconds, showSeconds);
                CreateCap(groundZero, d.CapRadius, capCentre, capDepth, d.CloudTop, d.RiseSeconds,
                    capLifetime, airburst);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.Play error: " + e);
            }
        }

        /// <summary>
        /// The fireball: born a fraction of its final size, swelling to it over fireballT and
        /// cooling from white through orange to a dull red as it lifts. The size curve does the
        /// swelling, so the growth is the visible thing rather than a puff appearing full size.
        /// </summary>
        private static void CreateFireball(Vector3 center, float radius, float fireballT)
        {
            var go = ParticleBuilder.NewSystem("NuclearFireball", center, ParticleAssets.Fire);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = fireballT * 1.7f;
            main.startSpeed = radius * 0.05f;
            main.startSize = radius * 0.55f; // the size curve below takes it from a third of this to full
            main.startColor = new ParticleSystem.MinMaxGradient(FireballCore, FireballMid);
            main.maxParticles = 240;

            ParticleBuilder.Burst(ps, 90);
            ParticleBuilder.Sphere(ps, radius * 0.28f);
            ParticleBuilder.LimitSpeed(ps, radius * 0.05f, 0.15f);
            ParticleBuilder.Rise(ps, radius * 0.12f); // the ball lifts as it burns, dragging the stem up after it

            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(FireballCore, 0f), new GradientColorKey(FireballMid, 0.25f),
                    new GradientColorKey(FireballEdge, 0.6f), new GradientColorKey(FireballCool, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0.55f, 0.8f), new GradientAlphaKey(0f, 1f),
                });
            ParticleBuilder.Colour(ps, grad);
            ParticleBuilder.SizeCurve(ps, 0.35f, 2.0f);
            ParticleBuilder.PlayAndDestroy(go, fireballT * 1.7f + 1f);
        }

        /// <summary>
        /// The condensation cloud. It appears a beat after the burst, out where the shock has
        /// already gone by, as a dome that turns into a ring and vanishes - which is exactly what
        /// a wide, thin, briefly-lived shell of white particles does on its own.
        /// </summary>
        private static void CreateCondensationDome(Vector3 center, float radius, float fireballT)
        {
            var go = ParticleBuilder.NewSystem("NuclearCondensation", center, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = fireballT * 0.3f; // a second or two at the yields normally used
            main.startLifetime = CondensationLifetime;
            main.startSpeed = radius * 0.35f;
            main.startSize = radius * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(Condensation);
            main.maxParticles = 140;

            ParticleBuilder.Burst(ps, 70);
            ParticleBuilder.Hemisphere(ps, radius * 0.7f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, 1.5f);
            ParticleBuilder.PlayAndDestroy(go, fireballT * 0.3f + CondensationLifetime + 0.5f);
        }

        /// <summary>
        /// The dirt the afterwinds tear off the ground. The updraft under the rising ball pulls
        /// air in along the surface and up into the base of the column, which is where a
        /// groundburst's fallout comes from.
        /// </summary>
        private static void CreateGroundDust(Vector3 groundZero, float stemR, float rise)
        {
            float life = rise * 0.55f;
            var go = ParticleBuilder.NewSystem("NuclearGroundDust", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemR * 0.06f;
            main.startSize = stemR * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
            main.maxParticles = 300;
            main.duration = rise * 0.4f;
            main.loop = false;

            ParticleBuilder.Stream(ps, 45f);
            ParticleBuilder.ConeUp(ps, stemR * 1.7f, 22f); // drawn inwards and up around the base
            ParticleBuilder.Rise(ps, stemR * 0.25f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.6f, 1.9f);
            ParticleBuilder.PlayAndDestroy(go, rise * 0.4f + life + 1f);
        }

        /// <summary>
        /// The stem, climbing at the rate the cloud really climbs. Barely any outward speed: the
        /// column has to stay narrow for the whole thing to read as a mushroom under its cap.
        ///
        /// Two things keep it a column rather than a slug of smoke thrown upwards. Each particle
        /// stops climbing once it has covered the cloud's height, instead of carrying on at the
        /// same rate for the rest of its life and taking the column out through the top of its
        /// own cap. And it is fed for as long as the canopy is up: emission used to stop when the
        /// cloud finished rising, after which the column drained away upwards and left the cap
        /// standing over clear air with its base several kilometres off the ground.
        /// </summary>
        private static void CreateStem(Vector3 groundZero, float stemR, float top, float climbTo,
            float rise, float showSeconds)
        {
            // A particle climbs into the canopy and is spent there. Letting it outlive the climb
            // by much only piles every particle that ever rose into one dark knot under the cap,
            // since they all stop at the same height; the column is a conveyor, and what reaches
            // the top is absorbed. The tail is what fades it into the canopy's underside.
            float climb = rise * (climbTo / top);
            float life = climb * 1.35f;
            // Where the particles are born and how large they have grown by the end have to add
            // up to stemR, exactly as the canopy's do to capR. Emitting them over the full stemR
            // and then growing each one to nearly as wide again drew a column 1.9 times the one
            // the figures describe - at 15 kt, a column as wide as its own cap.
            const float emitFraction = 0.45f;
            const float growth = 1.6f;
            float emitRadius = stemR * emitFraction;
            float finalDiameter = 2f * (stemR - emitRadius);

            var go = ParticleBuilder.NewSystem("NuclearStem", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemR * 0.02f;
            main.startSize = finalDiameter / growth;
            main.startColor = new ParticleSystem.MinMaxGradient(DustDark, CapCool);
            main.maxParticles = StemMaxParticles;
            main.duration = showSeconds;
            main.loop = false;

            // As fast as the budget will carry: emitting faster than maxParticles/life only buys
            // a burst at the start and then a starved column once the ceiling is hit.
            ParticleBuilder.Stream(ps, StemMaxParticles * 0.95f / life);
            ParticleBuilder.Sphere(ps, emitRadius);
            // It climbs at the rate the cloud climbs, and stops at the canopy's underside. Not
            // at its middle: the column is dark and the canopy is pale, so a column that carries
            // on into the cap is drawn straight through the front of it.
            ParticleBuilder.Rise(ps, ClimbThenSettle(climb, life), top / rise);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, growth);
            ParticleBuilder.PlayAndDestroy(go, showSeconds + life + 1f);
        }

        /// <summary>
        /// The canopy. Its three contributions - where the particles are emitted, how far they
        /// drift in their lifetime and how large they have grown by the end - are set so that
        /// they add up to capR, the stabilised cloud radius. Letting the drift run free, as an
        /// untuned speed does over an eighteen second lifetime, is what makes a cloud spread to
        /// several times the size the yield says it should be.
        ///
        /// It is born at the head of the column, not at the cloud top, and rides the rest of the
        /// way up at the column's own rate before settling. Emitting it at the top instead - as
        /// it used to be - hangs a finished canopy in clear air seconds before the stem arrives
        /// under it, which is the one thing that gives the effect away as particles.
        ///
        /// How deep it is is decided by the tropopause - see TropopauseAltitude. Its own width has
        /// nothing to say about it, and taking the depth from the width, which is what falls out
        /// of sizing the particles off capR, gives a 10 Mt canopy three times too deep: a ball on
        /// a stick rather than the sheet a megaton cloud spreads out along the tropopause.
        /// </summary>
        private static void CreateCap(Vector3 groundZero, float capR, float capCentre,
            float capDepth, float top, float rise, float lifetime, bool airburst)
        {
            const float emitFraction = 0.35f; // where the particles start, as a fraction of capR
            const float growth = 1.6f;        // how much larger a particle is by the end of its life

            // A round billboard grown to the cap's depth, spread across a horizontal disc until
            // the three together - where it starts, how far it spreads and how large it has
            // grown - add up to capR.
            float spriteRadius = capDepth * 0.5f;
            float emitRadius = Mathf.Min(capR * emitFraction, Mathf.Max(0f, capR - spriteRadius));
            float driftDistance = Mathf.Max(0f, capR - emitRadius - spriteRadius);
            float driftSpeed = driftDistance / lifetime;
            // Enough of them to cover the disc several times over at any yield: a 10 Mt canopy is
            // four times wider against its own depth than a 20 kt one, so a fixed count that
            // reads as solid at one would be gauze at the other.
            float cover = capR / Mathf.Max(spriteRadius, 1f);
            int count = Mathf.Clamp(Mathf.RoundToInt(8f * cover * cover), 100, 400);

            // The head of the column when the cap starts to swell, and the climb left to do. It
            // climbs at the cloud's own rate and settles at the canopy's middle.
            float delay = rise * CapEmergeFraction;
            float birthHeight = capCentre * CapEmergeFraction;
            float climbSeconds = (capCentre - birthHeight) / (top / rise);

            var go = ParticleBuilder.NewSystem("NuclearCap", groundZero + Vector3.up * birthHeight,
                ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = delay; // it starts to swell once the column is well up
            main.startLifetime = lifetime;
            // An outward drift of anywhere from nothing up to the full driftDistance over the
            // lifetime. The spread is what fills the canopy: give every particle the same speed
            // and they all leave the middle together, and the cap comes out a ring with a hole
            // in it rather than a disc. The speed is set here rather than through a speed limit
            // because the climb below already uses velocityOverLifetime, and the limit module is
            // applied to a particle's whole velocity - it would brake the climb along with the
            // drift.
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
            // The size curve grows it to the full depth. The spread is what makes a canopy read
            // as cauliflower rather than as an airbrushed lens: a real cap is a crowd of lobes
            // of visibly different sizes. The largest is the one the depth was solved for, so
            // the spread runs downwards from it and the canopy keeps the depth it was given.
            float sizeMax = capDepth / growth;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMax * 0.55f, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(CapVapour, CapVapourShade);
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;  // the rim droops, which is the cap's rollover

            ParticleBuilder.Burst(ps, count);
            ParticleBuilder.FlatDisc(ps, emitRadius);
            ParticleBuilder.Rise(ps, ClimbThenSettle(climbSeconds, lifetime), top / rise);
            ParticleBuilder.Colour(ps, CapGradient(airburst));
            ParticleBuilder.SizeCurve(ps, 0.7f, growth);
            ParticleBuilder.PlayAndDestroy(go, delay + lifetime + 2f);
        }

        /// <summary>
        /// The canopy's colour and opacity over a particle's life: the dust it came up with,
        /// clearing as the water condenses out, and stopping short of clean white for a
        /// groundburst, which has ground to lift and fallout to carry.
        /// </summary>
        private static Gradient CapGradient(bool airburst)
        {
            Color born = airburst ? CapTintAir : CapTintDust;
            Color settled = airburst ? CapSettledAir : CapSettledGround;
            Color half = Color.Lerp(born, settled, 0.55f);
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(born, 0f), new GradientColorKey(half, 0.4f),
                    new GradientColorKey(settled, 0.75f), new GradientColorKey(settled, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f),
                });
            return grad;
        }

        /// <summary>
        /// The cap's climb over a particle's life, as a fraction of the stem's rate: full rate
        /// for the seconds the column has left to run, then eased off to nothing so the canopy
        /// stops at the cloud top instead of carrying on out of the sky. The flat tangents are
        /// what keep the eased section from overshooting.
        /// </summary>
        private static AnimationCurve ClimbThenSettle(float climbSeconds, float lifetime)
        {
            float hold = Mathf.Clamp(climbSeconds / lifetime, 0.02f, 0.9f);
            float settled = Mathf.Min(hold * 1.15f, 0.99f);
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f), new Keyframe(hold, 1f, 0f, 0f),
                new Keyframe(settled, 0f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f));
        }
    }
}
