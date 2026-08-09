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
    /// The non-nuclear craters are deliberately not to scale. A real 1 t bomb digs a hole barely
    /// wider than a bus, which is invisible at the zoom the game is played at, so the crater of
    /// every kg-yield warhead is exaggerated by about 2.4x and its destruction radius pulled back
    /// by 10%: the result is a warhead that scars the ground plainly without flattening the whole
    /// block around it. Depth is exaggerated less than radius, leaving a wide, shallow bowl of
    /// roughly the 1:8 depth-to-diameter ratio a real explosion crater has.
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
        public float BurstAltitude;       // height above the target the warhead detonates at when fused for an airburst, in metres
        public bool Airburst;             // set by WithBurst; the detonation happens BurstAltitude above the ground and leaves no crater
        // The yield in kilotons, for nuclear warheads only, set at launch. The radii above are
        // all the yield has to say about the damage, but the fireball and the cloud are built to
        // real figures - see NuclearCloud - which need the yield itself and not a ratio.
        public float YieldKilotons;
        // An incendiary carries next to no kinetic punch: its charge disperses and ignites fuel
        // rather than driving a shock wave. Scaled therefore leaves its crater and destruction
        // radii alone and grows only the fires, however large the charge gets.
        public bool Incendiary;

        // Factor applied to the destruction and burn radii for an airburst, whose blast and
        // thermal radiation reach a wider area.
        public const float AirBurstBlastFactor = 1.35f;

        /// <summary>
        /// A new spec reflecting the burst height. This is immutable and leaves the original
        /// alone. A groundburst clears the burst altitude and changes nothing else; an airburst
        /// removes the crater and the contamination, widens the destruction and burn radii by
        /// AirBurstBlastFactor, and keeps the burst altitude the detonation is placed at.
        /// </summary>
        public WarheadSpec WithBurst(BurstType burst)
        {
            WarheadSpec s = this; // a copy of the struct; the original is untouched
            if (burst == BurstType.Groundburst)
            {
                s.Airburst = false;
                s.BurstAltitude = 0f;
                return s;
            }

            s.Airburst = true;
            s.CraterRadius = 0f;
            s.CraterDepth = 0f;
            s.RaiseCraterEdges = false;
            s.Contaminates = false;
            s.ContaminationRadius = 0f;
            s.DestructionRadius *= AirBurstBlastFactor;
            s.BurnRadius *= AirBurstBlastFactor;
            return s;
        }

        /// <summary>
        /// A new spec with every effect radius - crater, destruction, burn and contamination -
        /// multiplied, along with the burst altitude, since the optimum height of burst follows
        /// the same cube-root law as the radii. An incendiary keeps its crater and destruction
        /// radii fixed and scales only the fires. Immutable; the original is untouched.
        /// </summary>
        public WarheadSpec Scaled(float multiplier)
        {
            WarheadSpec s = this; // a copy of the struct; the caller's spec is untouched
            if (!s.Incendiary)
            {
                s.CraterRadius *= multiplier;
                s.CraterDepth *= multiplier;
                s.DestructionRadius *= multiplier;
            }
            s.BurnRadius *= multiplier;
            s.ContaminationRadius *= multiplier;
            s.BurstAltitude *= multiplier;
            return s;
        }

        public static WarheadSpec For(WarheadType type)
        {
            switch (type)
            {
                case WarheadType.Cluster:
                    // Cluster munition: the submunitions scatter widely. Each one is small, but
                    // together they cover a large area. The dispenser opens a few hundred metres
                    // up when it is fused for an airburst.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 9.5f, CraterDepth = 3f, DestructionRadius = 18f,
                        SubmunitionCount = 10, SpreadRadius = 260f,
                        RaiseCraterEdges = false, BurnRadius = 12f, Contaminates = false,
                        BurstAltitude = 150f,
                    };
                case WarheadType.WhitePhosphorus:
                    // White phosphorus, an incendiary. The filler burns rather than detonating,
                    // so there is no crater and barely any blast - just enough to break the
                    // building it lands on - and the damage is the fires the burning pellets
                    // start. Incendiary keeps that blast fixed however large the charge is; only
                    // the burn radius grows with it.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 0f, CraterDepth = 0f, DestructionRadius = 6f,
                        SubmunitionCount = 14, SpreadRadius = 220f,
                        RaiseCraterEdges = false, BurnRadius = 70f, Contaminates = false,
                        BurstAltitude = 125f, Incendiary = true,
                    };
                case WarheadType.Thermobaric:
                    // Thermobaric, equivalent to a large fuel-air explosive: the overpressure
                    // flattens a wide area, but the crater is shallow. The fuel cloud is meant to
                    // be ignited low down, so its airburst is only just above the rooftops.
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 47f, CraterDepth = 9f, DestructionRadius = 180f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, BurnRadius = 220f, Contaminates = false,
                        BurstAltitude = 30f,
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
                        BurstAltitude = 900f, // half the optimum height of burst for 150 kt, so the fireball stays in shot
                    };
                default: // Conventional: a large HE warhead of about 1 t
                    return new WarheadSpec
                    {
                        Type = WarheadType.Conventional,
                        CraterRadius = 24f, CraterDepth = 6f, DestructionRadius = 72f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = false, BurnRadius = 40f, Contaminates = false,
                        BurstAltitude = 40f,
                    };
            }
        }
    }
}
