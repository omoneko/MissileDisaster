using System;

namespace MissileDisaster.Core
{
    /// <summary>One puff of the anvil: where it sits in the sheet and how solid it is.</summary>
    public struct AnvilPoint
    {
        public float X, Y, Z;
        public float Size;
        public float Fade;
        public bool Upper;   // which of the two sheets it belongs to
    }

    /// <summary>
    /// The anvil: the wide, thin sheet a big cloud spreads into when it stops climbing. Pure,
    /// with no UnityEngine dependency.
    ///
    /// <para>
    /// A cloud rises until it is no longer warmer than the air around it. For anything above
    /// about a hundred kilotons that ceiling is the tropopause, and the column cannot punch
    /// through it: the top spills sideways instead and flattens into a sheet far wider than the
    /// cap and only a fraction as deep. Big shots show it in two layers - the main sheet at the
    /// tropopause and a thinner skirt hanging under it, where the column overshot, fell back and
    /// spread a second time.
    /// </para>
    ///
    /// <para>
    /// The trigger is the real cloud top against the real tropopause, NOT the drawn height
    /// against the mod's screen ceiling. The two are easy to confuse and the second one is wrong:
    /// the drawn ceiling only bites above about 15 Mt, so an anvil keyed to it would be a feature
    /// almost nobody ever saw. Keyed to the atmosphere it appears from 150 kt up, which is where
    /// it appears in life.
    /// </para>
    /// </summary>
    public static class AnvilCap
    {
        /// <summary>The tropopause, in metres. Mid-latitude average; it is a band rather than a line, which is why the spread eases in rather than switching on.</summary>
        public const float TropopauseAltitude = 11000f;

        /// <summary>How far above the tropopause a cloud has to reach before the sheet is at full width.</summary>
        public const float FullSpreadAltitude = 30000f;

        /// <summary>How wide the sheet is against the cap, at no overshoot and at full overshoot.</summary>
        public const float RadiusPerCapMin = 1.25f;
        public const float RadiusPerCapMax = 2.30f;

        /// <summary>How deep each sheet is, against its own radius. Thin is the whole character of it.</summary>
        public const float Thickness = 0.055f;

        /// <summary>
        /// How far the lower sheet hangs below the upper one, against the SHEET's radius.
        ///
        /// It was a fraction of the cap's depth, which at 150 kt put the two sheets 280 m apart
        /// inside a shape 400 m deep overall - two clouds stacked, not the thin double sheet that
        /// was asked for. Against the sheet's own radius it stays proportionate at every yield.
        /// </summary>
        public const float LayerGap = 0.10f;

        /// <summary>The lower sheet is the smaller of the two - it is what fell back, not what spread.</summary>
        public const float LowerRadiusFraction = 0.72f;

        /// <summary>
        /// How much the sheet dishes: the rim rides above the middle, because the spreading air
        /// is still rising at the edge while the centre has already settled. Against the radius.
        /// </summary>
        public const float BowlRise = 0.09f;

        public const int PuffCount = 260;
        public const float UpperShare = 0.62f;   // of the puffs; the rest make the lower skirt

        /// <summary>Puff sizes against the sheet's radius. Small, because a sheet this thin is drawn by many little puffs rather than a few big ones.</summary>
        public const float PuffSizeMin = 0.07f;
        public const float PuffSizeMax = 0.15f;

        /// <summary>Whether a cloud of this real height spreads into an anvil at all.</summary>
        public static bool Forms(float realCloudTopMetres)
        {
            return realCloudTopMetres > TropopauseAltitude;
        }

        /// <summary>
        /// How far past the tropopause the cloud pushed, 0 to 1. It is what the sheet's width
        /// grows with - a cloud that only just reaches the ceiling barely spreads.
        /// </summary>
        public static float Overshoot(float realCloudTopMetres)
        {
            if (realCloudTopMetres <= TropopauseAltitude) return 0f;
            float span = FullSpreadAltitude - TropopauseAltitude;
            float u = (realCloudTopMetres - TropopauseAltitude) / span;
            return u > 1f ? 1f : u;
        }

        /// <summary>The sheet's radius, in metres, for a cloud with this cap and this real height.</summary>
        public static float Radius(float capRadius, float realCloudTopMetres)
        {
            if (!Forms(realCloudTopMetres)) return 0f;
            float u = Overshoot(realCloudTopMetres);
            return capRadius * (RadiusPerCapMin + (RadiusPerCapMax - RadiusPerCapMin) * u);
        }

        /// <summary>
        /// Places puff i of the anvil. drawnCloudTop is the drawn figure the cap itself is built
        /// from, so the sheet sits on top of the cloud rather than beside it.
        /// </summary>
        public static AnvilPoint At(int index, int seed, float capRadius, float drawnCloudTop,
            float realCloudTopMetres, float widthFraction, float heightFraction)
        {
            var p = new AnvilPoint();
            float radius = Radius(capRadius, realCloudTopMetres) * widthFraction;
            if (radius <= 0f) return p;

            p.Upper = index < (int)(PuffCount * UpperShare);
            float sheetRadius = p.Upper ? radius : radius * LowerRadiusFraction;

            float azimuth = Hash01(index, seed, 1) * (float)(2.0 * Math.PI);
            // sqrt spreads them evenly over the disc; the sheet is filled, not a ring.
            float rho = (float)Math.Sqrt(Hash01(index, seed, 2));
            float dist = sheetRadius * rho;
            p.X = dist * (float)Math.Cos(azimuth);
            p.Z = dist * (float)Math.Sin(azimuth);

            float top = drawnCloudTop * heightFraction;
            float layerY = p.Upper ? top : top - sheetRadius * LayerGap;
            // The dish: the rim rides above the middle, and the sheet has a little thickness of
            // its own so it reads as cloud rather than as a decal.
            float bowl = sheetRadius * BowlRise * rho * rho;
            float thickness = sheetRadius * Thickness * (Hash01(index, seed, 3) - 0.5f) * 2f;
            p.Y = layerY + bowl + thickness;

            p.Size = sheetRadius * (PuffSizeMin + (PuffSizeMax - PuffSizeMin) * Hash01(index, seed, 4));
            // The rim is ragged and thinning, the middle solid.
            p.Fade = 1f - 0.55f * rho * rho;
            return p;
        }

        /// <summary>A deterministic 0..1, matching the hash the cloud's own puffs use.</summary>
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
