using System;

namespace MissileDisaster.Core
{
    /// <summary>What one puff of the base surge is doing: where it sits in the dome and how solid it is.</summary>
    public struct SurgePoint
    {
        public float X, Y, Z;   // metres from ground zero
        public float Size;      // metres across
        public float Fade;      // 0..1
        public float Dust;      // 0 = pale vapour, 1 = dirt
    }

    /// <summary>
    /// The base surge: the dome of dust that rolls out from the foot of the column after a
    /// ground burst, expanding outward and upward at once and eventually swallowing the
    /// mushroom that raised it. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// This is a real phenomenon rather than an invention. A surface or shallow burst throws far
    /// more soil than it can lift, and what it cannot lift rolls: a dense, fast, ground-hugging
    /// cloud that spreads from the base of the stem, climbs as it goes, and outlives the column.
    /// It is the reason photographs of ground shots show the mushroom standing in a bowl of
    /// dirty cloud rather than on clean ground - and the reason the mod's ground bursts have
    /// looked thin, because it was simply missing.
    /// </para>
    ///
    /// <para>
    /// The growth is deliberately front-loaded and then slow: r goes as u^0.35, so the dome
    /// leaps out in the first seconds while the fireball is still collapsing and then creeps for
    /// the rest of the shot. That was the shape asked for - fast at first, much slower once the
    /// mushroom peaks - and it is also what the flow does, for the same reason the shock front
    /// decelerates.
    /// </para>
    ///
    /// An airburst does not get one: nothing is in contact with the ground to scour.
    /// </summary>
    public static class GroundDust
    {
        /// <summary>How the dome grows: fast out of the gate, then a long creep. Below one, and lower than the shock front's 0.4, because the surge is heavy and slows harder.</summary>
        public const float GrowthExponent = 0.35f;

        /// <summary>How wide the dome finishes, against the cloud's cap. Wider than the cap, which is what "it subsumes the mushroom" means from the ground.</summary>
        public const float RadiusPerCap = 1.3f;

        /// <summary>
        /// How tall it finishes, against the cloud's top. The cap's base sits at 0.58 of the top,
        /// so this puts the dome's crown just into the underside of the canopy: it swallows the
        /// stem and starts on the cap, which is what "it subsumes the mushroom" looks like from a
        /// camera angle anyone plays at. Taking it much higher hides the mushroom altogether,
        /// which loses the thing the effect exists for.
        /// </summary>
        public const float HeightPerCloudTop = 0.62f;

        /// <summary>Where it starts: a ring already outside the fireball at the moment it is first drawn.</summary>
        public const float BirthRadiusPerCap = 0.12f;

        /// <summary>How long it keeps growing, against the cloud's rise. Slower than the mushroom, which is the whole point - the two must not move as one object.</summary>
        public const float GrowthPerRise = 2.6f;

        /// <summary>It hangs on after it has stopped growing, then thins. Both against its own growth time.</summary>
        public const float HoldFraction = 0.35f;
        public const float FadeFraction = 0.8f;

        /// <summary>
        /// Puff sizes against the dome's current radius, and how many. Large, soft and crowded:
        /// this is a wall of dust, not a spray, and the mod has been told once already that a
        /// cloud you can see the background through is not a cloud.
        ///
        /// Raised from 220 puffs at 0.16-0.34 after measuring the dome's body in
        /// tools/effect-preview/surge_preview.py: that read 0.46 solid and this reads 0.64, with
        /// clear diminishing returns past here (440 puffs bought only 0.68). The absolute figure
        /// rests on a compositing model the preview assumes rather than measures, so the game is
        /// the arbiter - but a surge is dust and is meant to be half translucent, unlike the cap,
        /// which was tuned to 0.997 because a mushroom is not.
        /// </summary>
        public const float PuffSizeMin = 0.20f;
        public const float PuffSizeMax = 0.40f;

        public const int PuffCount = 340;

        /// <summary>How thick the shell is - puffs sit between this fraction of the radius and the surface, so the dome has a body rather than being a soap bubble.</summary>
        public const float ShellDepth = 0.42f;

        /// <summary>How long the whole surge lasts, in seconds, for a cloud that rises in this many.</summary>
        public static float GrowthSeconds(float cloudRiseSeconds)
        {
            float t = cloudRiseSeconds * GrowthPerRise;
            return t < 2f ? 2f : t;
        }

        public static float TotalSeconds(float cloudRiseSeconds)
        {
            float g = GrowthSeconds(cloudRiseSeconds);
            return g * (1f + HoldFraction + FadeFraction);
        }

        /// <summary>How far out the dome has reached, in metres, t seconds in.</summary>
        public static float RadiusAt(float t, float capRadius, float cloudRiseSeconds)
        {
            float growth = GrowthSeconds(cloudRiseSeconds);
            float u = growth <= 0f ? 1f : t / growth;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;
            float birth = capRadius * BirthRadiusPerCap;
            float final = capRadius * RadiusPerCap;
            return birth + (final - birth) * (float)Math.Pow(u, GrowthExponent);
        }

        /// <summary>How high the dome has climbed, in metres, t seconds in. It rises on the same clock as it spreads - the two are one motion, not a spread followed by a lift.</summary>
        public static float HeightAt(float t, float cloudTop, float cloudRiseSeconds)
        {
            float growth = GrowthSeconds(cloudRiseSeconds);
            float u = growth <= 0f ? 1f : t / growth;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;
            // A shade slower than the spread, so the dome starts as a skirt and thickens into a
            // dome rather than inflating as a hemisphere from the first frame.
            return cloudTop * HeightPerCloudTop * (float)Math.Pow(u, GrowthExponent * 1.45f);
        }

        /// <summary>How solid the surge is, t seconds in: it fades in over its first moments and thins out at the end.</summary>
        public static float AlphaAt(float t, float cloudRiseSeconds)
        {
            float growth = GrowthSeconds(cloudRiseSeconds);
            if (t < 0f) return 0f;
            float fadeIn = growth * 0.08f;
            if (t < fadeIn && fadeIn > 0f) return t / fadeIn;
            float steady = growth * (1f + HoldFraction);
            if (t <= steady) return 1f;
            float fade = growth * FadeFraction;
            if (fade <= 0f) return 0f;
            float u = (t - steady) / fade;
            return u >= 1f ? 0f : 1f - u;
        }

        /// <summary>
        /// Places puff i of the dome. Deterministic, so a strike raises the same surge every time
        /// it is replayed.
        /// </summary>
        public static SurgePoint At(int index, int seed, float t, float capRadius, float cloudTop,
            float cloudRiseSeconds)
        {
            var p = new SurgePoint();
            float radius = RadiusAt(t, capRadius, cloudRiseSeconds);
            float height = HeightAt(t, cloudTop, cloudRiseSeconds);

            float azimuth = Hash01(index, seed, 1) * (float)(2.0 * Math.PI);
            // The polar angle is biased towards the ground, so the dome is heaviest where a real
            // surge is heaviest - down at the skirt, rolling outward.
            float polar = (float)(Math.PI * 0.5) * (float)Math.Pow(Hash01(index, seed, 2), 1.6);
            // A shell with a body: puffs fill the outer part of the dome rather than its surface.
            float shell = 1f - ShellDepth * Hash01(index, seed, 3);

            float horizontal = radius * shell * (float)Math.Cos(polar);
            p.X = horizontal * (float)Math.Cos(azimuth);
            p.Z = horizontal * (float)Math.Sin(azimuth);
            p.Y = height * shell * (float)Math.Sin(polar);

            // A slow churn, so the wall boils as it rolls instead of being a static shape that
            // scales up. It is deliberately much slower than the cap's own roll.
            float churn = t * 0.35f + Hash01(index, seed, 4) * (float)(2.0 * Math.PI);
            p.X += radius * 0.03f * (float)Math.Sin(churn);
            p.Y += height * 0.05f * (float)Math.Sin(churn * 1.3f);
            p.Z += radius * 0.03f * (float)Math.Cos(churn * 0.8f);

            // The churn can push a skirt puff under the ground, where it is drawn inside the
            // terrain and simply lost. The dome sits ON the ground; nothing in it goes below.
            if (p.Y < 0f) p.Y = 0f;

            p.Size = radius * (PuffSizeMin + (PuffSizeMax - PuffSizeMin) * Hash01(index, seed, 5));
            p.Fade = AlphaAt(t, cloudRiseSeconds);
            // Dirtiest at the bottom, where it is scouring the ground; paler up at the crown.
            p.Dust = 1f - 0.35f * (float)Math.Sin(polar);
            return p;
        }

        /// <summary>A deterministic 0..1, matching the hash the cloud's puffs use.</summary>
        public static float Hash01(int index, int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)(index * 374761393 + seed * 668265263 + salt * 1274126177);
                h ^= h >> 13;
                h *= 1911520717u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
