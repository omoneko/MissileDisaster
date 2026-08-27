using System;

namespace MissileDisaster.Core
{
    /// <summary>One chunk's launch: where it starts, where it is going, and how it tumbles.</summary>
    public struct DebrisLaunch
    {
        public float X, Y, Z;       // metres from ground zero at the moment of launch
        public float VX, VY, VZ;    // m/s
        public float SpinX, SpinY, SpinZ; // degrees per second
        public float Scale;         // metres, the chunk's longest axis
        public int Variant;         // which of the chunk shapes
    }

    /// <summary>
    /// The flight of one piece of rubble: a plain ballistic arc, worked out here rather than
    /// handed to a particle system. Pure, with no UnityEngine dependency.
    ///
    /// It is computed rather than simulated for the same reason the mushroom cloud's puffs are.
    /// A particle system decides for itself what it draws and when, and when the answer is
    /// "nothing" - as it was for two rounds of this effect - there is no way to tell from the
    /// outside whether the geometry, the material, the emission or the renderer was at fault.
    /// A chunk that is a real object at a position this class computes either exists or does
    /// not, and either shows up or does not, and those are separate questions with separate
    /// answers.
    /// </summary>
    public static class DebrisFlight
    {
        public const float Gravity = BlastDebris.Gravity;

        /// <summary>How much of a chunk's speed is lost to drag over its flight. Enough to keep the arcs from looking like artillery.</summary>
        public const float Drag = 0.12f;

        public const float SpinDegreesPerSecond = 260f;

        /// <summary>
        /// Deals chunk i of a strike: a launch point somewhere on the destroyed disc, a velocity
        /// out and up from the centre, a size and a shape.
        ///
        /// The direction is outward from ground zero rather than random, because that is what a
        /// blast does - and it is what makes the rubble read as being thrown by the explosion
        /// rather than as falling out of the sky.
        /// </summary>
        public static DebrisLaunch Launch(int index, int seed, float emitRadius, float speed,
            float chunkSize, int variants)
        {
            var l = new DebrisLaunch();

            float azimuth = Hash01(index, seed, 1) * (float)(2.0 * Math.PI);
            // sqrt spreads the launch points evenly over the disc rather than crowding the middle.
            float radius = emitRadius * (float)Math.Sqrt(Hash01(index, seed, 2));
            float cos = (float)Math.Cos(azimuth), sin = (float)Math.Sin(azimuth);

            l.X = radius * cos;
            l.Y = 0f;
            l.Z = radius * sin;

            // Out and up. The angle varies per chunk so the arcs are not a single fountain, and
            // the speed varies so they do not all land in one ring.
            float angle = BlastDebris.LaunchAngleDegrees * (0.7f + 0.6f * Hash01(index, seed, 3));
            double rad = angle * Math.PI / 180.0;
            float v = speed * (0.55f + 0.45f * Hash01(index, seed, 4));
            float horizontal = v * (float)Math.Cos(rad);

            l.VX = horizontal * cos;
            l.VY = v * (float)Math.Sin(rad);
            l.VZ = horizontal * sin;

            l.SpinX = (Hash01(index, seed, 5) - 0.5f) * 2f * SpinDegreesPerSecond;
            l.SpinY = (Hash01(index, seed, 6) - 0.5f) * 2f * SpinDegreesPerSecond;
            l.SpinZ = (Hash01(index, seed, 7) - 0.5f) * 2f * SpinDegreesPerSecond;

            // Many small pieces and a few large ones, the same bias the cloud's puffs use.
            float roll = Hash01(index, seed, 8);
            l.Scale = chunkSize * (0.35f + 0.65f * roll * roll);
            l.Variant = variants > 0 ? (int)(Hash01(index, seed, 9) * variants) % variants : 0;
            return l;
        }

        /// <summary>Where a chunk is t seconds after launch, relative to its launch point. Drag is a simple exponential bleed on the horizontal.</summary>
        public static void PositionAt(DebrisLaunch l, float t, out float x, out float y, out float z)
        {
            float decay = (float)Math.Exp(-Drag * t);
            // Horizontal distance under exponential drag: v/k * (1 - e^-kt).
            float travel = Drag > 0f ? (1f - decay) / Drag : t;
            x = l.X + l.VX * travel;
            z = l.Z + l.VZ * travel;
            y = l.Y + l.VY * t - 0.5f * Gravity * t * t;
        }

        /// <summary>How long until a chunk launched like this is back at ground level.</summary>
        public static float FlightSeconds(DebrisLaunch l)
        {
            if (l.VY <= 0f) return 0f;
            return 2f * l.VY / Gravity;
        }

        /// <summary>A deterministic 0..1, so a strike throws the same rubble every time it is replayed.</summary>
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
