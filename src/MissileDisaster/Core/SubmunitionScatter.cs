using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Places the submunitions deterministically. Pure, with no UnityEngine dependency and no
    /// randomness, which makes where they land reproducible.
    /// The arrangement is phyllotactic, like a sunflower head: point k sits at k times the
    /// golden angle, at a radius of SpreadRadius * sqrt((k+0.5)/count). That spreads the points
    /// evenly across the scatter radius instead of bunching them at the centre.
    /// </summary>
    public static class SubmunitionScatter
    {
        // The golden angle, pi*(3 - sqrt 5) radians or about 137.5 degrees, which spaces
        // consecutive points as far from overlapping as possible.
        private const double GoldenAngle = 2.399963229728653;

        /// <summary>
        /// count scatter offsets as (X, Z). A count of 1 or less gives a single point at the
        /// origin, and a SpreadRadius of 0 puts every point there. No point is further from the
        /// origin than spreadRadius.
        /// </summary>
        public static Offset2[] Offsets(int count, float spreadRadius)
        {
            if (count <= 1)
            {
                return new[] { new Offset2 { X = 0f, Z = 0f } };
            }

            var result = new Offset2[count];
            for (int k = 0; k < count; k++)
            {
                double angle = k * GoldenAngle;
                double radius = spreadRadius * Math.Sqrt((k + 0.5) / count);
                result[k] = new Offset2
                {
                    X = (float)(radius * Math.Cos(angle)),
                    Z = (float)(radius * Math.Sin(angle)),
                };
            }
            return result;
        }
    }
}
