using System;

namespace MissileDisaster.Core
{
    /// <summary>One piece of rubble being swept outward: where it lies, where the blast pushes it, and how it tumbles getting there.</summary>
    public struct DebrisRide
    {
        public float StartX, StartZ;  // metres from ground zero, where the piece was standing
        public float DirX, DirZ;      // unit vector, straight out from ground zero
        public float Distance;        // how far the front carries it, metres
        public float CarrySeconds;    // how long it keeps moving
        public float HopHeight;       // the apex of its first skip, metres
        public int Hops;              // how many skips before it stops bouncing
        public float RollDegrees;     // total tumble about the axis it is rolling over
        public float YawDegreesPerSecond;
        public float Scale;           // metres, its longest axis
        public int Variant;           // which of the chunk shapes
    }

    /// <summary>
    /// How the blast moves the rubble: not thrown, swept. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// This replaces a ballistic model. Throwing each piece on its own arc is what a shell does,
    /// and drawn at city scale it read as confetti - a few hundred independent parabolas going
    /// their own way. A blast does not do that. The front passes, the flow behind it drags
    /// everything loose along the ground in the direction it is travelling, and what you see is
    /// one expanding ring of wreckage moving with the dust, not a scatter.
    /// </para>
    ///
    /// So every piece here travels straight out from ground zero, low, and on the same clock as
    /// the shock front: fast the moment the wave arrives and decelerating for the rest of its
    /// run, on the Sedov t^0.4 profile ShockWave already uses. The vertical part is a skip rather
    /// than an arc - the ground keeps knocking it back up as it is pushed, each bounce lower than
    /// the last.
    ///
    /// One departure from physics, declared: a piece's travel is aimed at a target radius rather
    /// than integrated from the flow it sits in, so the pieces converge into a band instead of
    /// preserving the spread of the disc they started on. That is what makes the ring read as a
    /// ring. The alternative is right and invisible: the flow near the centre is orders of
    /// magnitude stronger than at the rim, so an honest integration flings the inner pieces past
    /// the outer ones and the wave dissolves into the scatter this was meant to replace.
    /// </summary>
    public static class DebrisSweep
    {
        /// <summary>The sweep decelerates on the same law as the front that causes it: r goes as t^0.4.</summary>
        public const float SweepExponent = ShockWave.Exponent;

        /// <summary>
        /// Where the pieces end up, against the throw BlastDebris solves. They converge on this
        /// rather than each carrying its own start radius outward with it - see the class note.
        /// </summary>
        public const float TargetMin = 0.75f;
        public const float TargetMax = 1.15f;

        /// <summary>No piece is merely nudged: even one that starts out near the target still travels far enough to read as having been moved.</summary>
        public const float MinTravelFraction = 0.15f;

        /// <summary>How long the rubble keeps moving, against how long the front takes to cross the blast.</summary>
        public const float CarryFactor = 1.4f;
        public const float CarrySecondsMin = 1.5f;
        public const float CarrySecondsMax = 9f;

        /// <summary>The first skip's apex, against the size of the piece. Low: this is rubble being pushed over the ground, not launched off it.</summary>
        public const float HopHeightMin = 1.2f;
        public const float HopHeightMax = 3.0f;
        public const int HopsMin = 2;
        public const int HopsMax = 4;

        /// <summary>
        /// And the skip is held against the sweep as well as the piece, because "low" is a
        /// proportion, not a number of metres. A 1 t charge pushes its rubble 30 m, and a piece
        /// hopping 8 m on the way is flying however small it is - it is a quarter of the whole
        /// journey. This only bites at the small end; at nuclear scale the pieces never come
        /// near it.
        /// </summary>
        public const float HopHeightRangeCap = 0.10f;

        /// <summary>
        /// How much of a true roll the tumble is. A chunk rolling without slipping would turn
        /// once per circumference; rubble bounces and slides, so it turns rather less.
        /// </summary>
        public const float RollSlip = 0.35f;
        public const float YawDegreesPerSecondMax = 90f;

        /// <summary>How long the rubble is under way, for a front that takes this long to cross the blast.</summary>
        public static float CarrySeconds(float frontSeconds)
        {
            float t = frontSeconds * CarryFactor;
            if (t < CarrySecondsMin) return CarrySecondsMin;
            return t > CarrySecondsMax ? CarrySecondsMax : t;
        }

        /// <summary>
        /// Deals piece i of a strike: where it lies on the destroyed disc, and how far out the
        /// blast pushes it. Deterministic, so a strike sweeps the same way every time it is
        /// replayed.
        /// </summary>
        public static DebrisRide Deal(int index, int seed, float emitRadius, float range,
            float carrySeconds, float chunkSize, int variants)
        {
            var r = new DebrisRide();

            float azimuth = Hash01(index, seed, 1) * (float)(2.0 * Math.PI);
            // sqrt spreads the pieces evenly over the disc rather than crowding the middle.
            float start = emitRadius * (float)Math.Sqrt(Hash01(index, seed, 2));
            float cos = (float)Math.Cos(azimuth), sin = (float)Math.Sin(azimuth);

            r.StartX = start * cos;
            r.StartZ = start * sin;
            r.DirX = cos;
            r.DirZ = sin;

            float target = range * (TargetMin + (TargetMax - TargetMin) * Hash01(index, seed, 3));
            float travel = target - start;
            float floorTravel = range * MinTravelFraction;
            r.Distance = travel < floorTravel ? floorTravel : travel;

            // They do not all stop at the same instant, or the ring would switch off.
            r.CarrySeconds = carrySeconds * (0.8f + 0.4f * Hash01(index, seed, 4));

            // Many small pieces and a few large ones, the same bias the cloud's puffs use.
            float roll = Hash01(index, seed, 8);
            r.Scale = chunkSize * (0.5f + 0.5f * roll * roll);
            r.Variant = variants > 0 ? (int)(Hash01(index, seed, 9) * variants) % variants : 0;

            r.HopHeight = r.Scale * (HopHeightMin
                + (HopHeightMax - HopHeightMin) * Hash01(index, seed, 5));
            float hopCeiling = range * HopHeightRangeCap;
            if (r.HopHeight > hopCeiling) r.HopHeight = hopCeiling;
            r.Hops = HopsMin + (int)(Hash01(index, seed, 6) * (HopsMax - HopsMin + 1));
            if (r.Hops > HopsMax) r.Hops = HopsMax;

            // It turns in proportion to the ground it covers, which is what rolling means - so
            // the tumble decelerates with the sweep without being told to.
            float circumference = (float)Math.PI * r.Scale;
            r.RollDegrees = circumference > 0f
                ? 360f * r.Distance / circumference * RollSlip : 0f;
            r.YawDegreesPerSecond = (Hash01(index, seed, 7) - 0.5f) * 2f * YawDegreesPerSecondMax;
            return r;
        }

        /// <summary>How far along its run a piece is, 0 to 1, t seconds after the blast.</summary>
        public static float Progress(DebrisRide r, float t)
        {
            if (r.CarrySeconds <= 0f) return 1f;
            float u = t / r.CarrySeconds;
            if (u < 0f) return 0f;
            return u > 1f ? 1f : u;
        }

        /// <summary>
        /// How far out a piece has been carried, in metres, on the front's own deceleration:
        /// most of the ground is covered in the first moments and the rest is a long slide.
        /// </summary>
        public static float TravelAt(DebrisRide r, float t)
        {
            return r.Distance * (float)Math.Pow(Progress(r, t), SweepExponent);
        }

        /// <summary>
        /// How high a piece is, in metres. It skips: the ground throws it back up each time it
        /// comes down, and each bounce is lower than the last until it is sliding.
        /// </summary>
        public static float HeightAt(DebrisRide r, float t)
        {
            float u = Progress(r, t);
            if (u >= 1f || r.Hops <= 0) return 0f;
            float bounce = (float)Math.Abs(Math.Sin(Math.PI * r.Hops * u));
            float damping = (1f - u) * (1f - u);
            return r.HopHeight * bounce * damping;
        }

        /// <summary>Where a piece is, in metres from ground zero, t seconds after the blast.</summary>
        public static void PositionAt(DebrisRide r, float t, out float x, out float y, out float z)
        {
            float travel = TravelAt(r, t);
            x = r.StartX + r.DirX * travel;
            z = r.StartZ + r.DirZ * travel;
            y = HeightAt(r, t);
        }

        /// <summary>How far it has turned, in degrees, about the axis it is rolling over.</summary>
        public static float RollAt(DebrisRide r, float t)
        {
            if (r.Distance <= 0f) return 0f;
            return r.RollDegrees * TravelAt(r, t) / r.Distance;
        }

        /// <summary>A deterministic 0..1, so a strike sweeps the same rubble every time it is replayed.</summary>
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
