namespace MissileDisaster.Core
{
    /// <summary>
    /// Impact parameters per warhead, as a plain table of numbers with no UnityEngine
    /// dependency. The radii come from real-world damage figures rather than game balance.
    /// The nuclear entry is calibrated on the blast radius going as the cube root of the yield:
    /// at the 150 kt baseline, 5 psi (buildings collapse) is about 3.7 km, thermal radiation and
    /// fires about 5.9 km, and fallout about 5.3 km. Multiplying those by
    /// Scaled(cbrt(kt/150)) gives the real radius R = C * kt^(1/3) at any yield - at 1 Mt, for
    /// instance, roughly 7.0 km of destruction and 11 km of fires, which matches published
    /// figures such as Nukemap's.
    /// The only impact APIs available are MakeCrater and DestroyStuff, so fires are represented
    /// as a burn band and fallout as ground pollution.
    /// </summary>
    public struct WarheadSpec
    {
        public WarheadType Type;
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;   // buildings collapse; for nuclear this is the 5 psi contour
        public int SubmunitionCount;
        public float SpreadRadius;
        public bool RaiseCraterEdges;
        public float BurnRadius;          // fires and thermal radiation; for nuclear this is third-degree burns, wider than the destruction
        public bool Contaminates;
        public float ContaminationRadius; // fallout; greater than zero for nuclear warheads only

        // Factor applied to the destruction and burn radii for an airburst, whose blast and
        // thermal radiation reach a wider area.
        public const float AirBurstBlastFactor = 1.35f;

        /// <summary>
        /// A new spec reflecting the burst height. This is immutable and leaves the original
        /// alone. A groundburst changes nothing; an airburst removes the crater and the
        /// contamination and widens the destruction and burn radii by AirBurstBlastFactor.
        /// </summary>
        public WarheadSpec WithBurst(BurstType burst)
        {
            if (burst == BurstType.Groundburst) return this;

            WarheadSpec s = this; // a copy of the struct; the original is untouched
            s.CraterRadius = 0f;
            s.CraterDepth = 0f;
            s.RaiseCraterEdges = false;
            s.Contaminates = false;
            s.ContaminationRadius = 0f;
            s.DestructionRadius *= AirBurstBlastFactor;
            s.BurnRadius *= AirBurstBlastFactor;
            return s;
        }

        /// <summary>A new spec with every effect radius - crater, destruction, burn and contamination - multiplied. Immutable; the original is untouched.</summary>
        public WarheadSpec Scaled(float multiplier)
        {
            WarheadSpec s = this; // a copy of the struct; the caller's spec is untouched
            s.CraterRadius *= multiplier;
            s.CraterDepth *= multiplier;
            s.DestructionRadius *= multiplier;
            s.BurnRadius *= multiplier;
            s.ContaminationRadius *= multiplier;
            return s;
        }

        public static WarheadSpec For(WarheadType type)
        {
            switch (type)
            {
                case WarheadType.Cluster:
                    // Cluster munition: the submunitions scatter widely. Each one is small, but
                    // together they cover a large area.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 4f, CraterDepth = 2f, DestructionRadius = 20f,
                        SubmunitionCount = 10, SpreadRadius = 260f,
                        RaiseCraterEdges = false, BurnRadius = 12f, Contaminates = false,
                    };
                case WarheadType.WhitePhosphorus:
                    // White phosphorus, an incendiary: little destruction from the blast itself
                    // but wide-spreading fires, scattered by submunitions.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 3f, CraterDepth = 1f, DestructionRadius = 15f,
                        SubmunitionCount = 14, SpreadRadius = 220f,
                        RaiseCraterEdges = false, BurnRadius = 70f, Contaminates = false,
                    };
                case WarheadType.Thermobaric:
                    // Thermobaric, equivalent to a large fuel-air explosive: the overpressure
                    // flattens a wide area, but the crater is shallow.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 20f, CraterDepth = 6f, DestructionRadius = 200f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, BurnRadius = 220f, Contaminates = false,
                    };
                case WarheadType.Nuclear:
                    // Nuclear, at the 150 kt baseline, using real groundburst radii: 3.7 km at
                    // 5 psi, 5.9 km of thermal radiation, 5.3 km of fallout and a 210 m crater.
                    // Any other yield is scaled to its real radii by Scaled(cbrt(kt/150)).
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 210f, CraterDepth = 64f, DestructionRadius = 3720f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, BurnRadius = 5850f,
                        Contaminates = true, ContaminationRadius = 5300f,
                    };
                default: // Conventional: the real radii of a large HE warhead of about 1 t
                    return new WarheadSpec
                    {
                        Type = WarheadType.Conventional,
                        CraterRadius = 10f, CraterDepth = 4f, DestructionRadius = 80f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = false, BurnRadius = 40f, Contaminates = false,
                    };
            }
        }
    }
}
