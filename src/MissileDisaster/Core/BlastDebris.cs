using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// How much rubble a blast moves, how big it is, and how far it goes. Pure, with no
    /// UnityEngine dependency. DebrisSweep is what actually moves it.
    ///
    /// The shock wave already carries the air and the dust it scrapes off the ground - a front
    /// racing outward and a wall of earth rolling behind it. What was missing is the solid part.
    /// A building inside the destruction radius does not fade away; it comes apart, and the
    /// pieces go with the blast. Drawing only smoke leaves a detonation looking like weather.
    ///
    /// The pieces are swept, not thrown. This class used to solve a ballistic arc as well -
    /// launch angle, muzzle speed, hang time - and that model is gone: a blast does not lob its
    /// rubble on individual parabolas, it drags everything loose along the ground in the
    /// direction the wave is travelling. See DebrisSweep for what replaced it and why.
    /// </summary>
    public static class BlastDebris
    {
        /// <summary>
        /// How far the pieces are swept, against what the warhead destroys. Well short of it:
        /// the far edge of a destruction radius is where buildings are damaged rather than
        /// demolished, and rubble piling up there would read as wrong. This is the ring where
        /// there is nothing left standing.
        /// </summary>
        public const float RangeFraction = 0.35f;

        /// <summary>
        /// How wide an area the rubble is swept FROM, against the blast radius.
        ///
        /// Starting it all from a point at ground zero is what made it invisible at nuclear
        /// scale: at 150 kt the pieces left a 50 m circle and spent their first seconds inside
        /// the fireball, which is simply a brighter object in the same place. It is also wrong.
        /// The fireball vaporises what stands at the centre; the rubble comes from the ring
        /// around it, where buildings were knocked down rather than consumed. So the pieces come
        /// off the whole destroyed area, outside the fireball, where there is something to see
        /// them against.
        ///
        /// The fraction was 0.30, which started a 150 kt strike's rubble on a 1116 m disc, and
        /// that was an over-correction resting on an arithmetic slip: the disc was sized to
        /// clear "a 310 m fireball", but 310 m is the fireball's WIDTH and the radius it has to
        /// clear is 155 m. Spreading car-sized pieces over a square kilometre of ground is how
        /// you make them vanish just as surely as starting them all from a point. So the disc is
        /// only as wide as it needs to be to keep the pieces out of the fireball.
        /// </summary>
        public const float EmitFraction = 0.07f;
        public const float EmitRadiusMin = 12f;
        public const float EmitRadiusMax = 420f;

        /// <summary>The disc the rubble is swept off, in metres.</summary>
        public static float EmitRadius(float blastRadius)
        {
            if (blastRadius <= 0f) return 0f;
            float r = blastRadius * EmitFraction;
            if (r < EmitRadiusMin) return EmitRadiusMin;
            return r > EmitRadiusMax ? EmitRadiusMax : r;
        }

        /// <summary>The bounds on the sweep, in metres. The floor keeps a small charge from merely nudging its rubble; the ceiling keeps a strategic one from strewing masonry across the whole map.</summary>
        public const float RangeMin = 30f;
        // The ceiling used to be solved from a ballistic hang time - how far a piece could be
        // thrown and still land inside its own lifetime. There is no arc any more, so the figure
        // stands on what it is for: 400 m is a wave of wreckage arriving several blocks out,
        // which is a strike tearing a district up. Further than that and a single warhead is
        // redecorating half the map, and the ring stops reading as coming from anywhere.
        public const float RangeMax = 400f;

        /// <summary>How far the pieces are swept, in metres, for a warhead with this blast radius.</summary>
        public static float Range(float blastRadius)
        {
            if (blastRadius <= 0f) return 0f;
            float range = blastRadius * RangeFraction;
            if (range < RangeMin) return RangeMin;
            return range > RangeMax ? RangeMax : range;
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
        // DebrisSweep then deals each piece somewhere between half this and all of it, so the
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
        /// How many pieces to sweep. It grows with the throw - a bigger blast tears up more - but
        /// far slower than the area does, because the count is a drawing budget and not a census.
        ///
        /// The ceiling went up with the pieces coming down to car size: a strategic warhead
        /// sweeping 260 four-metre chunks over a kilometre is a sparse scatter, and what should
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
