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
    /// A real phenomenon, and worth being accurate about, because the first version of this was
    /// not. The base surge is chiefly a WATER effect: Crossroads Baker's - the one every
    /// photograph shows - was a dense cloud of droplets thrown off the collapsing column. It
    /// began to form about ten seconds after the burst, rolled outward at over a mile a minute,
    /// reached two and a half miles, and topped out at about a thousand feet against a column
    /// measured in kilometres. It is a doughnut: low, very wide, and rolling. On dry land the
    /// effect is weaker still, because soil does not make the droplet cloud water does.
    /// </para>
    ///
    /// <para>
    /// The first version had it at 0.62 of the cloud's height, growing until it swallowed the
    /// mushroom. That came from a Workshop request - "it should slowly grow bigger until it
    /// subsumes the mushroom cloud" - implemented as though it were physics. It is not, and it
    /// hid the thing the player came to watch. It is now built to the figures above: a wide, low,
    /// rolling collar that arrives a beat after the column, with its middle left open so the stem
    /// still stands visibly inside it.
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

        /// <summary>How wide it finishes, against the cloud's cap. Wide is the surge's whole character - Baker's ran to two and a half miles - and it is where the height went.</summary>
        public const float RadiusPerCap = 1.55f;

        /// <summary>
        /// How tall it finishes, against the cloud's top. Low, and that is the measured figure
        /// rather than a compromise: Baker's surge reached about 1,000 ft under a column
        /// kilometres high, so roughly a sixth of it. A surge that reaches the cap is not a
        /// surge, it is a second cloud - and it hides the mushroom, which is what sent this back.
        ///
        /// The figure is against the DRAWN cloud top, which this mod stretches to twice scale
        /// (CloudHeightScale) - so 0.09 of it, not Baker's 0.15 of a real column. At 150 kt that
        /// is a collar 143 m tall inside a ring 450 m across: about three times wider than tall,
        /// which is the shape, where 0.16 gave 254 m and read as a second cloud.
        /// </summary>
        public const float HeightPerCloudTop = 0.09f;

        /// <summary>Where it starts: a ring already outside the fireball at the moment it is first drawn.</summary>
        public const float BirthRadiusPerCap = 0.12f;

        /// <summary>
        /// It arrives after the column, not with it. Baker's began to form ten to twelve seconds
        /// in, once the plume was already collapsing - so the surge reads as something the
        /// explosion went on to cause, rather than as part of the same puff of smoke.
        /// </summary>
        public const float DelayPerRise = 0.35f;

        /// <summary>
        /// How much of the middle is left open. The surge is a doughnut rolling outward, not a
        /// filled dome, and leaving the centre clear is both the real shape and what lets the
        /// stem go on showing through it.
        /// </summary>
        public const float InnerHole = 0.62f;

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

        /// <summary>How long the surge waits before it begins to form, in seconds.</summary>
        public static float DelaySeconds(float cloudRiseSeconds)
        {
            return cloudRiseSeconds * DelayPerRise;
        }

        /// <summary>How long the surge spends growing, in seconds, for a cloud that rises in this many.</summary>
        public static float GrowthSeconds(float cloudRiseSeconds)
        {
            float t = cloudRiseSeconds * GrowthPerRise;
            return t < 2f ? 2f : t;
        }

        public static float TotalSeconds(float cloudRiseSeconds)
        {
            float g = GrowthSeconds(cloudRiseSeconds);
            return DelaySeconds(cloudRiseSeconds) + g * (1f + HoldFraction + FadeFraction);
        }

        /// <summary>Seconds since the surge itself began, which is later than the burst.</summary>
        private static float Since(float t, float cloudRiseSeconds)
        {
            float s = t - DelaySeconds(cloudRiseSeconds);
            return s < 0f ? 0f : s;
        }

        /// <summary>How far out the dome has reached, in metres, t seconds in.</summary>
        public static float RadiusAt(float t, float capRadius, float cloudRiseSeconds)
        {
            float growth = GrowthSeconds(cloudRiseSeconds);
            float u = growth <= 0f ? 1f : Since(t, cloudRiseSeconds) / growth;
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
            float u = growth <= 0f ? 1f : Since(t, cloudRiseSeconds) / growth;
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
            float since = Since(t, cloudRiseSeconds);
            if (since <= 0f) return 0f;
            float fadeIn = growth * 0.08f;
            if (since < fadeIn && fadeIn > 0f) return since / fadeIn;
            float steady = growth * (1f + HoldFraction);
            if (since <= steady) return 1f;
            float fade = growth * FadeFraction;
            if (fade <= 0f) return 0f;
            float u = (since - steady) / fade;
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
            // A doughnut: the puffs fill the outer annulus and leave the middle open, so the stem
            // goes on standing visibly inside it. sqrt spreads them evenly by area in that band.
            float band = (float)Math.Sqrt(Hash01(index, seed, 2));
            float horizontal = radius * (InnerHole + (1f - InnerHole) * band);
            p.X = horizontal * (float)Math.Cos(azimuth);
            p.Z = horizontal * (float)Math.Sin(azimuth);
            // Heaviest along the ground and thinning upward: a collar rolling outward, not a
            // shell standing up.
            p.Y = height * (float)Math.Pow(Hash01(index, seed, 3), 1.8);

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
            p.Dust = 1f - 0.35f * (height > 0f ? p.Y / height : 0f);
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
