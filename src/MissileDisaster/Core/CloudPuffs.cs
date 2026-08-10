using System;

namespace MissileDisaster.Core
{
    /// <summary>One puff's fixed parameters, dealt deterministically from its index and the strike's seed.</summary>
    public struct PuffSpec
    {
        public bool Cap;       // circulates in the cap's vortex ring
        public bool Fire;      // smoke off the burning city, drawn in toward the updraft; neither flag means the column
        public float Azimuth;  // radians around the cloud's axis
        public float Swirl;    // slow azimuth drift, radians per second
        public float Rho01;    // cap: how far out from the ring core; fire: how far out in the burn field
        public float Theta0;   // cap: starting angle around the ring core
        public float Omega;    // cap: circulation rate, radians per rolled second
        public float Climb01;  // column and fire: phase offset along the climb loop
        public float Wobble;   // column: radial wobble phase
        public float Size01;   // relative size within its kind
        public float Spin;     // billboard rotation rate, degrees per second, signed
        public float Lag;      // 0..1, this puff's place in the staggered dissolve at the end
    }

    /// <summary>Where one puff is at one moment, in metres from ground zero, and how it is shaded.</summary>
    public struct PuffPoint
    {
        public float X, Y, Z;
        public float Size;     // billboard size, metres
        public float Fade;     // 0..1, the puff's own alpha factor on top of the cloud's
        public float Ember;    // 0..1, how much of the early fire glow it shows
        public float Dust;     // 0..1, how dusty its colour is against the cap's white vapour
    }

    /// <summary>
    /// The mushroom cloud as a crowd of soft cloud puffs, driven by the flow a real one has.
    /// Pure - every function here is deterministic in its inputs - so the shape can be tested.
    ///
    /// A mushroom cloud is a vortex ring. The hot core rises up the middle, rolls outward when
    /// it stops climbing, curls down around the outside of the cap and is drawn in again
    /// underneath: that circulation is what folds the surface into cauliflower and it is why
    /// the rim overhangs. So the cap's puffs are placed on a torus and advanced around its
    /// cross-section - up the inside, out over the top, down the outside, in along the bottom -
    /// while the column's puffs climb a billowing profile from the ground into the cap's
    /// underside and are recycled. Everything is scaled by CloudAnimationState, so the whole
    /// structure grows out of the fireball, stands, and disperses on the one tested timeline.
    ///
    /// Real seconds are first put through RollTime: the circulation runs at full rate while the
    /// cloud rises, slows once it stands, and all but freezes as it fades - a cloud that has
    /// stopped feeding stops boiling.
    /// </summary>
    public static class CloudPuffs
    {
        // Sizes, counts and alphas here were set in tools/effect-preview/cloud_preview.py,
        // which composites the puffs exactly as the game does and measures the opacity across
        // the cap's body. The target is near-total: a cloud the background shows through is
        // not a cloud. The shipped profile measures 0.997 standing and 0.955 while fading.
        public const int CapCount = 340;
        public const int ColumnCount = 160;
        public const int FireCount = 120;
        public const int TotalCount = CapCount + ColumnCount + FireCount;

        // The cap's torus, as fractions of the drawn cap radius and depth. Outer reach is
        // RingRadius + RingCross = 1.0 exactly, so the envelope honours the figure.
        public const float RingRadius = 0.55f;   // of cap radius: where the ring's core sits
        public const float RingCross = 0.45f;    // of cap radius: the cross-section's radial reach
        public const float RingCentreHeight = 0.5f; // of cap depth above the cap base

        // The column profile: wider at the dust skirt, a waist partway up, swelling again at
        // the throat where it disappears into the cap. From the same reference proportions the
        // mesh was built to.
        public const float ColumnSkirtFactor = 1.35f;
        public const float ColumnWaistFactor = 1.0f;
        public const float ColumnThroatFactor = 1.2f;
        public const float ColumnWaistAt = 0.35f;   // fraction of the climb where the waist sits
        public const float ColumnTopIntoCap = 0.2f; // how far into the cap's depth the column runs

        // How the circulation slows through the cloud's life.
        public const float RollRateHold = 0.35f;
        public const float RollRateFade = 0.1f;

        // The fire stops showing through the folds about here into the rise.
        public const float EmberDiesAtRiseFraction = 0.7f;

        // The fire smoke: born across the burn field, climbing slowly, gently drawn in toward
        // the central updraft over its loop and absorbed as it arrives. Its loop is longer than
        // the column's - the drift is supposed to read as gentle - and it dissolves last at the
        // end, because the city under the cloud is still burning.
        public const float FireLoopFactor = 1.6f;   // of the rise, per loop
        public const float FireInwardPull = 0.85f;  // how much of its birth radius a puff gives up
        public const float FireSmokeHeightFraction = 0.7f; // of the cap base, the height the smoke climbs to
        public const float FireEdgeFade = 0.1f;     // loop-end fade band, wider than the column's

        // The staggered dissolve: where each kind's thinning starts inside the fade window.
        // The column dies first - nothing is feeding it - the cap loosens next, and the fire
        // smoke outlasts them both.
        public const float DissolveLagColumn = 0.05f;
        public const float DissolveLagCap = 0.10f;
        public const float DissolveLagFire = 0.35f;
        public const float DissolveLagSpreadColumn = 0.40f;
        public const float DissolveLagSpreadCap = 0.50f;
        public const float DissolveLagSpreadFire = 0.50f;
        public const float DissolveLoosening = 0.30f; // how much a puff swells as it thins away
        // How much of the fade window one puff takes to go, once its turn comes. Shorter than
        // the window itself, so the early puffs are fully gone while the late ones still stand -
        // the cloud shreds away piece by piece rather than dimming as one.
        public const float DissolveWindow = 0.35f;

        /// <summary>The fixed parameters of puff i for one strike. Deterministic: the same index and seed always deal the same puff.</summary>
        public static PuffSpec Spec(int index, int seed)
        {
            var s = new PuffSpec();
            s.Cap = index < CapCount;
            s.Fire = index >= CapCount + ColumnCount;
            s.Azimuth = Hash01(index, seed, 1) * (float)(2.0 * Math.PI);
            s.Swirl = (Hash01(index, seed, 2) - 0.5f) * 0.08f;
            // sqrt biases the puffs outward, where the cauliflower is; the column fills the hole.
            s.Rho01 = 0.1f + 0.9f * (float)Math.Sqrt(Hash01(index, seed, 3));
            s.Theta0 = Hash01(index, seed, 4) * (float)(2.0 * Math.PI);
            // The inner puffs turn faster, as the inside of a vortex does.
            s.Omega = (0.55f + 0.75f * (1f - s.Rho01)) * (0.8f + 0.4f * Hash01(index, seed, 5));
            s.Climb01 = Hash01(index, seed, 6);
            s.Wobble = Hash01(index, seed, 7) * (float)(2.0 * Math.PI);
            s.Size01 = Hash01(index, seed, 8);
            s.Spin = (Hash01(index, seed, 9) - 0.5f) * 24f;
            s.Lag = Hash01(index, seed, 10);
            return s;
        }

        /// <summary>
        /// The seconds the circulation has effectively run for, after t real seconds: full rate
        /// through the rise, RollRateHold through the stand, RollRateFade through the fade.
        /// </summary>
        public static float RollTime(float t, float riseSeconds, float holdSeconds)
        {
            if (t <= riseSeconds) return t;
            float rolled = riseSeconds;
            float inHold = t - riseSeconds;
            if (inHold <= holdSeconds) return rolled + inHold * RollRateHold;
            rolled += holdSeconds * RollRateHold;
            return rolled + (t - riseSeconds - holdSeconds) * RollRateFade;
        }

        /// <summary>Puff p at t seconds into a cloud drawn to dims, in the state anim says the cloud is in.</summary>
        public static PuffPoint At(PuffSpec p, float t, NuclearCloudDimensions dims, CloudAnimationState anim)
        {
            // The structure at this moment: heights follow the height fraction, radii the width.
            float capR = dims.CapRadius * anim.WidthFraction;
            float stemR = dims.StemRadius * anim.WidthFraction;
            float capBase = dims.CapBase * anim.HeightFraction;
            float capDepth = dims.CapDepth * anim.HeightFraction;

            float azimuth = p.Azimuth + p.Swirl * t;
            float dist, y;
            var point = new PuffPoint();

            if (p.Fire)
            {
                // Smoke off the burning city. Born at its own spot in the burn field - fires
                // are there from the flash, so the field does not grow with the cloud - it
                // climbs slowly and is gently drawn in toward the central updraft, arriving
                // spent at the column's base. The pull uses the same smooth ease as everything
                // else: most of the drift happens mid-loop, none of it as a jerk.
                float floop = dims.RiseSeconds * FireLoopFactor;
                float fu = Frac(p.Climb01 + t / floop);
                float r0 = dims.FireFieldRadius * (0.3f + 0.7f * p.Rho01);
                dist = r0 * (1f - FireInwardPull * Smooth(fu));
                y = (float)Math.Pow(fu, 1.3) * capBase * FireSmokeHeightFraction;
                // Vanilla's smoke swells 0.4 -> 1.0 over its life; fresh smoke is small and
                // expands as it rises and cools. Same here, per loop.
                point.Size = dims.FireFieldRadius * (0.09f + 0.08f * p.Size01)
                    * (0.55f + 0.45f * Smooth(fu));
                point.Fade = EdgeFade(fu, FireEdgeFade);
                point.Dust = 1f - 0.3f * fu;
                // The fires themselves keep glowing where the smoke is freshest, for the whole
                // life of the effect - unlike the folds of the cap, which cool with the rise.
                float fresh = 1f - fu;
                point.Ember = 0.18f * fresh * fresh * fresh;
            }
            else if (p.Cap)
            {
                // The vortex ring: up the inside, out over the top, down the outside, in
                // underneath. Theta grows with the rolled time, so the boil slows as the cloud
                // stops feeding.
                float theta = p.Theta0 + p.Omega * RollTime(t, dims.RiseSeconds, dims.HoldSeconds);
                float ringCore = capR * RingRadius;
                float crossR = capR * RingCross * p.Rho01;
                float crossY = capDepth * 0.5f * p.Rho01;
                float centreY = capBase + capDepth * RingCentreHeight;
                dist = ringCore - crossR * (float)Math.Cos(theta);
                if (dist < 0f) dist = 0f;
                y = centreY + crossY * (float)Math.Sin(theta);
                point.Size = capR * (0.23f + 0.16f * p.Size01);
                point.Fade = 1f;
                point.Dust = 0.15f + 0.15f * (1f - p.Rho01); // the inner cap keeps a little of the column's dust
                // Early on the fire shows through the folds nearest the core.
                point.Ember = EmberEnvelope(t, dims.RiseSeconds) * (1f - p.Rho01) * 0.8f;
            }
            else
            {
                // The column: an endless conveyor of puffs climbing from the skirt into the
                // cap's underside, recycled at the top, fading in and out at the loop's ends so
                // the recycling never pops.
                float loopSeconds = dims.RiseSeconds * 0.9f;
                float u = Frac(p.Climb01 + t / loopSeconds);
                float columnTop = capBase + capDepth * ColumnTopIntoCap;
                y = EaseOutQuad(u) * columnTop;
                float shape = ColumnShape(u);
                float radial = 0.25f + 0.75f * p.Rho01;
                float wobble = 1f + 0.18f * (float)Math.Sin(p.Wobble + u * 9.4f + t * 0.4f);
                dist = stemR * shape * radial * wobble;
                point.Size = stemR * (0.7f + 0.5f * p.Size01) * (0.7f + 0.3f * Smooth(u));
                point.Fade = LoopFade(u);
                point.Dust = 0.85f - 0.5f * u; // dust at the base, paling as it climbs
                point.Ember = EmberEnvelope(t, dims.RiseSeconds) * u * 0.6f; // the glow is up near the fireball
            }

            // The staggered dissolve at the end: each puff thins away on its own cue inside
            // the fade window, swelling a little as it goes, so the cloud shreds gradually
            // instead of evaporating in one piece.
            if (dims.FadeSeconds > 0f)
            {
                float fp = (t - dims.RiseSeconds - dims.HoldSeconds) / dims.FadeSeconds;
                if (fp > 0f)
                {
                    if (fp > 1f) fp = 1f;
                    float lag = p.Fire ? DissolveLagFire + DissolveLagSpreadFire * p.Lag
                        : p.Cap ? DissolveLagCap + DissolveLagSpreadCap * p.Lag
                        : DissolveLagColumn + DissolveLagSpreadColumn * p.Lag;
                    float span = DissolveWindow;
                    if (span > 1f - lag) span = 1f - lag; // the last puffs still finish by the end
                    if (span < 0.05f) span = 0.05f;
                    float prog = (fp - lag) / span;
                    if (prog < 0f) prog = 0f;
                    if (prog > 1f) prog = 1f;
                    float dissolve = 1f - Smooth(prog);
                    point.Fade *= dissolve;
                    point.Size *= 1f + DissolveLoosening * (1f - dissolve);
                }
            }

            point.X = dist * (float)Math.Cos(azimuth);
            point.Y = y;
            point.Z = dist * (float)Math.Sin(azimuth);
            return point;
        }

        /// <summary>The column's silhouette factor along its climb: skirt, waist, throat.</summary>
        public static float ColumnShape(float u)
        {
            if (u < ColumnWaistAt)
            {
                float k = u / ColumnWaistAt;
                return ColumnSkirtFactor + (ColumnWaistFactor - ColumnSkirtFactor) * Smooth(k);
            }
            float k2 = (u - ColumnWaistAt) / (1f - ColumnWaistAt);
            return ColumnWaistFactor + (ColumnThroatFactor - ColumnWaistFactor) * Smooth(k2);
        }

        /// <summary>How strongly the fire still shows through the folds, t seconds in: full at the flash, gone most of the way up the rise.</summary>
        public static float EmberEnvelope(float t, float riseSeconds)
        {
            float dies = riseSeconds * EmberDiesAtRiseFraction;
            if (dies <= 0f) return 0f;
            float e = 1f - t / dies;
            return e < 0f ? 0f : e;
        }

        /// <summary>Fades a column puff in over the first and out over the last twentieth of its loop, so recycling never pops.</summary>
        public static float LoopFade(float u)
        {
            return EdgeFade(u, 0.06f);
        }

        /// <summary>The same in-and-out fade with the band width as a parameter - the fire smoke uses a wider one.</summary>
        public static float EdgeFade(float u, float edge)
        {
            if (u < edge) return Smooth(u / edge);
            if (u > 1f - edge) return Smooth((1f - u) / edge);
            return 1f;
        }

        private static float EaseOutQuad(float u)
        {
            return 1f - (1f - u) * (1f - u);
        }

        private static float Smooth(float k)
        {
            if (k < 0f) k = 0f;
            if (k > 1f) k = 1f;
            return k * k * (3f - 2f * k);
        }

        private static float Frac(float v)
        {
            return v - (float)Math.Floor(v);
        }

        /// <summary>A deterministic 0..1 from the puff's index, the strike's seed and a salt - the same everywhere, forever.</summary>
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
