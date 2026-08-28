using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// What the blast throws: the rubble of whatever stood at ground zero, flung outward and
    /// upward and falling back across the city. Pure, with no UnityEngine dependency.
    ///
    /// The shock wave already carries the air and the dust it scrapes off the ground - a front
    /// racing outward and a wall of earth rolling behind it. What was missing is the solid part.
    /// A building inside the destruction radius does not fade away; it comes apart, and the
    /// pieces go with the blast. Drawing only smoke leaves a detonation looking like weather.
    ///
    /// The flight is ballistic and solved backwards from where the pieces should land. Given a
    /// range and a launch angle, the speed that reaches it is v = sqrt(g R / sin 2θ), and the
    /// time it spends in the air follows from the same throw. That way the debris is specified
    /// by the one number that matters visually - how far it is thrown - and the speed and the
    /// hang time come out of the physics rather than being dialled in against each other.
    /// </summary>
    public static class BlastDebris
    {
        public const float Gravity = 9.81f;

        /// <summary>
        /// How far the pieces are thrown, against what the warhead destroys. Well short of it:
        /// the far edge of a destruction radius is where buildings are damaged rather than
        /// demolished, and rubble raining down there would read as wrong. This is the ring where
        /// there is nothing left standing.
        /// </summary>
        public const float RangeFraction = 0.35f;

        /// <summary>The launch angle, in degrees. Not 45: a blast throws its rubble out rather than up, and a flatter arc keeps it in frame and lands it sooner.</summary>
        public const float LaunchAngleDegrees = 32f;

        /// <summary>
        /// How wide an area the rubble is thrown FROM, against the blast radius.
        ///
        /// Launching it all from a point at ground zero is what made it invisible at nuclear
        /// scale: at 150 kt the pieces left a 50 m circle and spent the first four seconds
        /// inside a 310 m fireball, which is simply a brighter object in the same place. It is
        /// also wrong. The fireball vaporises what stands at the centre; the rubble comes from
        /// the ring around it, where buildings were knocked down rather than consumed. So the
        /// pieces are thrown from across the destroyed area, and start their arc outside the
        /// fireball where there is something to see them against.
        /// </summary>
        ///
        /// The fraction was 0.30, which threw a 150 kt strike's rubble off a 1116 m disc, and
        /// that was an over-correction resting on an arithmetic slip: the disc was sized to
        /// clear "a 310 m fireball", but 310 m is the fireball's WIDTH and the radius it has to
        /// clear is 155 m. Spreading car-sized pieces over a square kilometre of ground is how
        /// you make them vanish just as surely as launching them from a point. So the disc is
        /// only as wide as it needs to be to start the pieces outside the fireball.
        public const float EmitFraction = 0.07f;
        public const float EmitRadiusMin = 12f;
        public const float EmitRadiusMax = 420f;

        /// <summary>The disc the rubble is thrown from, in metres.</summary>
        public static float EmitRadius(float blastRadius)
        {
            if (blastRadius <= 0f) return 0f;
            float r = blastRadius * EmitFraction;
            if (r < EmitRadiusMin) return EmitRadiusMin;
            return r > EmitRadiusMax ? EmitRadiusMax : r;
        }

        /// <summary>The bounds on the throw, in metres. The floor keeps a small charge from merely dribbling; the ceiling keeps a strategic one from raining masonry across the whole map.</summary>
        public const float RangeMin = 30f;
        // Solved from the hang time rather than picked, and solved for the LONGEST arc rather
        // than the average one. 620 m put the nominal chunk at 8.9 s, safely under the ceiling -
        // but chunks are thrown at a spread of angles and speeds, and the steepest of them was
        // 10.9 s, so the outliers were still being destroyed two seconds before they landed.
        // At 400 m the steepest chunk in the spread comes down at 8.95 s, so none of them do.
        public const float RangeMax = 400f;

        /// <summary>Hang time is capped so a big strike is not still dropping bricks a minute later. RangeMax is set so this never actually bites.</summary>
        public const float FlightSecondsMax = 9f;

        /// <summary>How far the pieces are thrown, in metres, for a warhead with this blast radius.</summary>
        public static float Range(float blastRadius)
        {
            if (blastRadius <= 0f) return 0f;
            float range = blastRadius * RangeFraction;
            if (range < RangeMin) return RangeMin;
            return range > RangeMax ? RangeMax : range;
        }

        /// <summary>
        /// The launch speed, in m/s, that carries a piece to that range on the launch angle:
        /// v = sqrt(g R / sin 2θ).
        /// </summary>
        public static float LaunchSpeed(float range)
        {
            if (range <= 0f) return 0f;
            double twoTheta = 2.0 * LaunchAngleDegrees * Math.PI / 180.0;
            double sin = Math.Sin(twoTheta);
            if (sin <= 0.0001) return 0f;
            return (float)Math.Sqrt(Gravity * range / sin);
        }

        /// <summary>
        /// How long a piece launched at that speed stays up: t = 2 v sin(theta) / g, held under
        /// the ceiling.
        /// </summary>
        public static float FlightSeconds(float launchSpeed)
        {
            if (launchSpeed <= 0f) return 0f;
            double theta = LaunchAngleDegrees * Math.PI / 180.0;
            float t = (float)(2.0 * launchSpeed * Math.Sin(theta) / Gravity);
            return t > FlightSecondsMax ? FlightSecondsMax : t;
        }

        /// <summary>
        /// The size of the largest pieces, in metres: the length of a car.
        ///
        /// This is a deliberate retreat from a readability allowance. The ceiling used to be
        /// 34 m so that one chunk could be picked out from the altitude a kilometre-wide strike
        /// is watched at. It worked, and it looked like a warhead throwing office blocks whole -
        /// nothing in a city is a single 34 m lump of masonry.
        ///
        /// So the figure is what it physically should be, measured off the game's own vehicles
        /// with UnityPy: 2.8-4.4 m for a car, 5-8 m for a van. What has to carry the effect at
        /// nuclear zoom is the mass of the spray instead - many more pieces (ChunksMax), and
        /// the dust travelling with them, which is sized from the blast and not from the chunk.
        /// </summary>
        // DebrisFlight then deals each piece somewhere between half this and all of it, so the
        // spread a strike throws runs from a hatchback to a van either way round.
        public const float ChunkSizeFraction = 0.01375f;
        public const float ChunkSizeMin = 4f;    // a sedan: 4.4 m in the game's own fleet
        public const float ChunkSizeMax = 5.5f;  // a van, the far end of car-sized

        public static float ChunkSize(float range)
        {
            float size = range * ChunkSizeFraction;
            if (size < ChunkSizeMin) return ChunkSizeMin;
            return size > ChunkSizeMax ? ChunkSizeMax : size;
        }

        /// <summary>
        /// How many pieces to throw. It grows with the throw - a bigger blast tears up more - but
        /// far slower than the area does, because the count is a drawing budget and not a census.
        ///
        /// The ceiling went up with the pieces coming down to car size: a strategic warhead
        /// throwing 260 four-metre chunks over a kilometre is a sparse scatter, and what should
        /// read there is a field of wreckage. The pieces are cheap - a few dozen triangles each -
        /// so the budget buys density rather than detail.
        /// </summary>
        public const int ChunksMin = 40;
        public const int ChunksMax = 520;

        public static int ChunkCount(float range)
        {
            if (range <= 0f) return 0;
            int n = (int)(ChunksMin + (ChunksMax - ChunksMin) * Math.Sqrt(range / RangeMax));
            if (n < ChunksMin) return ChunksMin;
            return n > ChunksMax ? ChunksMax : n;
        }
    }
}
