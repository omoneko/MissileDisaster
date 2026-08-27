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
        public const float EmitFraction = 0.30f;
        public const float EmitRadiusMin = 12f;
        public const float EmitRadiusMax = 1400f;

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
        // Solved from the hang time rather than picked: at this launch angle a 620 m throw is
        // just under nine seconds in the air. Setting it any further would make the flight
        // outlast the lifetime the effect gives a chunk, and the pieces would wink out in
        // mid-air instead of landing - which is what a 900 m throw was doing.
        public const float RangeMax = 620f;

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
        /// The size of the largest pieces, in metres. Tied to the throw rather than to the blast
        /// radius: a bomb that levels one building throws pieces of that building, and a warhead
        /// that levels a district throws the same masonry, just further and in more quantity.
        /// </summary>
        // The floor is the game's own rubble. Its rock props - rock_small_01..04 - measure about
        // 4.0 m on their longest axis, so a chunk smaller than that is smaller than the wreckage
        // the player already sees lying around the map, and reads as grit.
        //
        // The ceiling is a readability allowance, and the only one here. Debris size does not
        // really scale with yield: a warhead does not make bigger masonry, it breaks more of it
        // and throws it further. But a 4 m chunk seen from the altitude a kilometre-wide strike
        // is watched at is a speck, so the pieces are allowed to grow towards something
        // building-sized - and no further, because a 26 m boulder reads as a mountain, not as a
        // wall that used to be a bank.
        // Raised from 14 m on measurement rather than taste: at 150 kt a 14 m chunk is 4.5% of
        // the fireball's width and about a thousandth of the frame the strike is watched in.
        // Individual rubble cannot read at that zoom whatever colour it is, so the pieces are
        // allowed to grow until they can - and the count grows with them, because what actually
        // reads at nuclear scale is the mass of the spray rather than any one piece.
        public const float ChunkSizeFraction = 0.055f;
        public const float ChunkSizeMin = 4f;
        public const float ChunkSizeMax = 34f;

        public static float ChunkSize(float range)
        {
            float size = range * ChunkSizeFraction;
            if (size < ChunkSizeMin) return ChunkSizeMin;
            return size > ChunkSizeMax ? ChunkSizeMax : size;
        }

        /// <summary>
        /// How many pieces to throw. It grows with the throw - a bigger blast tears up more - but
        /// far slower than the area does, because the count is a drawing budget and not a census.
        /// </summary>
        public const int ChunksMin = 24;
        public const int ChunksMax = 260;

        public static int ChunkCount(float range)
        {
            if (range <= 0f) return 0;
            int n = (int)(ChunksMin + (ChunksMax - ChunksMin) * Math.Sqrt(range / RangeMax));
            if (n < ChunksMin) return ChunksMin;
            return n > ChunksMax ? ChunksMax : n;
        }
    }
}
