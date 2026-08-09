using System;
using MissileDisaster.Core;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// A nuclear detonation, built to the figures in MissileDisaster.Core.NuclearCloud rather
    /// than to taste. Main thread only.
    ///
    /// The mushroom itself is a textured mesh - Models/MushroomCloud.obj, a sculpted cloud with
    /// the fire baked into its crevices - because that is the verdict of trying to build one out
    /// of billboard particles: however carefully the column and canopy budgets are solved, a
    /// crowd of round sprites reads as smoke in the shape of a mushroom, never as the solid,
    /// cauliflower thing in the photographs. The mesh is grown out of the fireball over the rise,
    /// the column shooting up first and the cap billowing after it (Core.CloudAnimation), stands
    /// at full size, and thins away.
    ///
    /// Around it, the stages a real detonation has and a static mesh cannot carry:
    ///
    ///  1. the fireball at the point of burst, swelling and cooling white through orange to a
    ///     dull red - it covers the mesh's small beginnings
    ///  2. the condensation cloud, the white dome that flashes into being behind the shock front
    ///     and is gone within a second or two
    ///  3. the dust the afterwinds tear off the ground and feed into the base of the column
    ///
    /// Every size and duration comes from MissileDisaster.Core.NuclearCloudDisplay: real figures
    /// under soft ceilings, so a bigger warhead is always a bigger cloud, right up to Tsar Bomba.
    /// </summary>
    public static class NuclearMushroomFx
    {
        // The condensation dome forms out where the shock has passed, well beyond the fireball,
        // and lasts barely a second.
        private const float CondensationRadiusFactor = 2.6f;
        private const float CondensationLifetime = 1.3f;

        // Colours, following how a real detonation looks rather than a palette: white hot, then
        // sodium yellow, then orange, then the brown of nitrogen dioxide and lofted earth.
        private static readonly Color FireballCore = new Color(1f, 0.99f, 0.94f, 1f);
        private static readonly Color FireballMid = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color FireballEdge = new Color(1f, 0.42f, 0.10f, 1f);
        private static readonly Color FireballCool = new Color(0.42f, 0.13f, 0.05f, 1f);
        private static readonly Color Condensation = new Color(0.96f, 0.97f, 1f, 0.42f);
        private static readonly Color DustLight = new Color(0.55f, 0.49f, 0.40f, 0.75f);
        private static readonly Color DustDark = new Color(0.32f, 0.28f, 0.23f, 0.75f);

        // The mesh's tint. Both 1945 photographs - airbursts - show a brilliant white cloud; a
        // groundburst has ground in it and keeps its dirt. The texture itself is pale, so the
        // tint only has to nudge it.
        private static readonly Color CloudTintAir = new Color(1f, 1f, 1f, 1f);
        private static readonly Color CloudTintGround = new Color(0.90f, 0.85f, 0.78f, 1f);

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
                float showSeconds = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds;

                // A nuclear strike is rare enough that one line about it is not noise, and it is
                // the only way to tell from a log what the effect actually drew.
                ModConfig.LogAlways(string.Format(
                    "nuclear {0:F0} kt {1}: cloud top {2:F0} m, cap {3:F0} m wide, " +
                    "fireball {4:F0} m across, up for {5:F0} s",
                    kilotons > 0f ? kilotons : NuclearYields.StandardKilotons,
                    airburst ? "airburst" : "groundburst",
                    d.CloudTop, d.CapRadius * 2f, d.FireballRadius * 2f, showSeconds));

                CreateFireball(detonation, d.FireballRadius, d.FireballSeconds);
                CreateCondensationDome(detonation, d.FireballRadius * CondensationRadiusFactor, d.FireballSeconds);
                CreateGroundDust(groundZero, d.StemRadius, d.RiseSeconds);
                CreateCloudMesh(groundZero, d, airburst);
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
            // Where a particle is born and how large it has grown by the end have to add up to
            // the fireball's radius, the same budget the canopy and the column are on. They did
            // not: a ball emitted over 0.28 R and grown to 0.55 R across reached only 0.83 R, so
            // a fireball whose figure is right was drawn a sixth short of it - and short is the
            // one direction it could not afford, being the smallest thing in the effect.
            const float emitFraction = 0.35f;
            const float growth = 2.0f;
            float finalDiameter = 2f * radius * (1f - emitFraction);

            var go = ParticleBuilder.NewSystem("NuclearFireball", center, ParticleAssets.Fire);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = fireballT * 1.7f;
            main.startSpeed = radius * 0.05f;
            main.startSize = finalDiameter / growth; // the size curve below swells it to full
            main.startColor = new ParticleSystem.MinMaxGradient(FireballCore, FireballMid);
            main.maxParticles = 240;

            ParticleBuilder.Burst(ps, 120);
            ParticleBuilder.Sphere(ps, radius * emitFraction);
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
            ParticleBuilder.SizeCurve(ps, 0.35f, growth);
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
            // The afterwinds blow for as long as the column is climbing, so the dust boils up
            // around its base for most of the rise rather than for a few seconds at the start -
            // this is the part of the effect the eye reads as the cloud welling up out of the
            // ground, and it used to be over before the column was halfway.
            float life = rise * 0.55f;
            var go = ParticleBuilder.NewSystem("NuclearGroundDust", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemR * 0.06f;
            main.startSize = stemR * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
            main.maxParticles = 420;
            main.duration = rise * 0.75f;
            main.loop = false;

            ParticleBuilder.Stream(ps, 420f * 0.95f / life);
            ParticleBuilder.ConeUp(ps, stemR * 2.4f, 22f); // drawn inwards and up around the base
            ParticleBuilder.Rise(ps, stemR * 0.25f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.6f, 1.9f);
            ParticleBuilder.PlayAndDestroy(go, rise * 0.75f + life + 1f);
        }

        /// <summary>
        /// The mushroom itself: the sculpted mesh, scaled so its top stands at CloudTop and its
        /// cap spans CapRadius, grown out of the fireball by MushroomCloudAnimator. Each strike
        /// gets a random heading, so two clouds never present exactly the same face.
        /// If the model cannot be loaded at all, a mass of smoke stands in - a nuclear strike
        /// with no cloud would read as a bug, not a fallback.
        /// </summary>
        private static void CreateCloudMesh(Vector3 groundZero, NuclearCloudDimensions d, bool airburst)
        {
            GameObject go = MissileModelProvider.CreateInstance(ModConfig.MushroomCloudModelName);
            if (go == null)
            {
                ExplosionFallback.Play(groundZero + Vector3.up * (d.CloudTop * 0.5f), d.CapRadius);
                return;
            }
            go.transform.position = groundZero; // the model's base sits at its own y=0
            go.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f);
            go.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f); // the animator takes over from the first frame

            // The converter normalises the model to height 1 with its base at y=0, but the
            // scales are read off the mesh itself so a re-exported model cannot silently skew.
            MeshFilter filter = go.GetComponent<MeshFilter>();
            Bounds bounds = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            float modelHeight = Mathf.Max(bounds.size.y, 0.0001f);
            float modelHalfWidth = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.0001f);

            Color tint = airburst ? CloudTintAir : CloudTintGround;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            Material[] fadeMats = TryMakeFadeMaterials(renderer, tint);
            if (fadeMats == null) MakeMatte(renderer, tint);

            var anim = go.AddComponent<MushroomCloudAnimator>();
            anim.HeightScale = d.CloudTop / modelHeight;
            anim.WidthScale = d.CapRadius / modelHalfWidth;
            anim.RiseSeconds = d.RiseSeconds;
            anim.HoldSeconds = d.HoldSeconds;
            anim.FadeSeconds = d.FadeSeconds;
            anim.CapRadius = d.CapRadius;
            anim.CloudTop = d.CloudTop;
            anim.FadeMaterials = fadeMats;
        }

        /// <summary>
        /// Rebuilds the renderer's materials on a transparency-capable shader, keeping the baked
        /// texture, so the animator can fade the cloud in and out through the alpha. Null when
        /// the game has no such shader, in which case the caller keeps the opaque materials and
        /// the animator covers the teardown with smoke instead.
        /// </summary>
        private static Material[] TryMakeFadeMaterials(MeshRenderer renderer, Color tint)
        {
            if (renderer == null) return null;
            Shader shader = RenderAssets.FindFirst(
                "Legacy Shaders/Transparent/Diffuse", "Transparent/Diffuse");
            if (shader == null) return null;
            try
            {
                Material[] old = renderer.materials; // instance copies, so the model cache is untouched
                var mats = new Material[old.Length];
                for (int i = 0; i < old.Length; i++)
                {
                    mats[i] = new Material(shader);
                    if (old[i] != null && old[i].mainTexture != null)
                    {
                        mats[i].mainTexture = old[i].mainTexture;
                    }
                    mats[i].color = new Color(tint.r, tint.g, tint.b, 0f); // born invisible; the animator fades it in
                    RenderAssets.ApplyDepthOcclusion(mats[i]);
                }
                renderer.materials = mats;
                return mats;
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.TryMakeFadeMaterials error: " + e);
                return null;
            }
        }

        /// <summary>The opaque path: the Standard materials the builder made, taken off the missile's metallic settings and tinted. A cloud is matte.</summary>
        private static void MakeMatte(MeshRenderer renderer, Color tint)
        {
            if (renderer == null) return;
            try
            {
                Material[] mats = renderer.materials; // instance copies
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i].color = tint;
                    if (mats[i].HasProperty("_Metallic")) mats[i].SetFloat("_Metallic", 0f);
                    if (mats[i].HasProperty("_Glossiness")) mats[i].SetFloat("_Glossiness", 0.08f);
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.MakeMatte error: " + e);
            }
        }
    }
}
