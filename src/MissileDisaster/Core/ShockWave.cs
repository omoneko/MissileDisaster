using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// How the blast front travels outwards from a detonation. Pure, with no UnityEngine
    /// dependency.
    ///
    /// A strong blast wave follows the Sedov-Taylor solution, r proportional to t^(2/5): it leaps
    /// out at many times the speed of sound and decays towards sonic as it spreads. That is what
    /// makes a real explosion read as a wave rather than a growing ball - the front is fastest in
    /// the moment nobody can see it and visibly slows as it crosses the ground.
    ///
    /// Glasstone and Dolan's 1 Mt figures anchor the timing: 10 s after the burst the front is
    /// about 3 miles beyond the 5,700 ft fireball, at 50 s it has run 12 miles and is down to
    /// 1,150 ft/s, barely above the speed of sound. Reaching the 5 psi contour, about 6.9 km out,
    /// therefore takes on the order of 13 s - an average of about 540 m/s. Both the radius and
    /// the time scale with the cube root of the yield, so that average holds at every yield.
    /// </summary>
    public static class ShockWave
    {
        public const float Exponent = 0.4f;          // r = R * (t/T)^0.4
        public const float AverageFrontSpeed = 540f; // metres per second, from the 1 Mt figures above

        // A 1 t charge's front crosses its 72 m of destruction in about a tenth of a second,
        // which is a frame or two. This floor is a playability concession, not physics, and it is
        // the only place the model departs from the figures.
        // Held at 0.35 s rather than the 0.9 s it used to be: at 0.9 s every conventional warhead
        // hit the floor and so every one of them took the same time to spread, which is exactly
        // the "it does not scale with the explosion" that was reported. 0.35 s is still about
        // twenty frames - long enough to read - and small charges now finish visibly sooner than
        // large ones.
        //
        // Do not raise it to buy a longer-lasting blast, however tempting. A 206 m front takes
        // 0.38 s of its own, so anything above about 0.35 puts every conventional warhead back on
        // the floor together and undoes the scaling above. What makes a small strike last is the
        // dust surge, the flame and the column of smoke, all of which are sized independently.
        public const float MinimumSeconds = 0.35f;

        /// <summary>
        /// Below this radius the rings run alone, without the rolling wall of earth behind them.
        ///
        /// <para>
        /// It was 250 m, which sounded like a judgement about bombs and was in practice a
        /// switch that turned the surge off entirely: a conventional warhead reaches 250 m of
        /// destruction at about 42 t of charge and a thermobaric at 2.7 t, so no ordinary strike
        /// ever saw it. The surge is the longest-lived and most substantial part of the blast, so
        /// losing it silently is most of what "the disaster does not have the same impact
        /// anymore" was describing.
        /// </para>
        ///
        /// At 110 m a thermobaric warhead gets one at its default charge and a conventional one
        /// from about 4 t up, while a single 1 t bomb still does not - a rolling wall of dirt
        /// behind one bomb really does read as a dust storm arriving from nowhere, which was the
        /// original and correct half of the argument. ShockWaveFx also scales the wall's particle
        /// count with the radius now, so the smaller ones are proportionate rather than a
        /// full-sized wall on a short leash.
        /// </summary>
        // Lowered again, from 110 m, on a Workshop report that conventional explosions have no
        // ground smoke worth the name. 110 m sounded modest and still excluded every ordinary
        // bomb: a 1 t charge destroys 72 m and a 1.5 t one 82 m, so neither raised a wall. At
        // 55 m every conventional warhead from about 500 kg up gets one, sized to its own
        // radius, and only the individual cluster bomblets - 18 m - still go without.
        public const float DustSurgeMinRadius = 55f;
        // The other end is a soft ceiling rather than a clamp: every strategic yield shared the
        // old 14 s, so a 50 Mt front - which really takes about 48 s to cross its 26 km - was
        // drawn crossing the ground three times faster than a 1 Mt one instead of taking longer
        // over it. Past the knee the duration keeps growing towards the ceiling.
        public const float MaximumSeconds = 14f;   // the knee: exact below this
        public const float CeilingSeconds = 26f;   // the front never lasts longer than this

        // The front is not tracked all the way in to t=0, where the Sedov speed goes to infinity.
        public const float MinFraction = 0.02f;

        /// <summary>Seconds the front takes to reach radius, held between the playable bounds.</summary>
        public static float Duration(float radius)
        {
            if (radius <= 0f) return 0f;
            float t = radius / AverageFrontSpeed;
            return EffectCeiling.Soft(t, MinimumSeconds, MaximumSeconds, CeilingSeconds);
        }

        /// <summary>
        /// Where the front already is by the time it is first drawn. The Sedov speed goes to
        /// infinity at t=0, so the front is only tracked from MinFraction onwards - and by then
        /// it has covered a fifth of its ground, because r goes as t^0.4. Starting the ring here
        /// rather than at the centre is what makes the drawn radius add up to the modelled one.
        /// </summary>
        public static float StartRadius(float radius)
        {
            return FrontRadius(radius, MinFraction);
        }

        /// <summary>Where the front is, in metres, a fraction u of the way through its life.</summary>
        public static float FrontRadius(float radius, float u)
        {
            if (radius <= 0f) return 0f;
            if (u <= 0f) return 0f;
            if (u > 1f) u = 1f;
            return radius * (float)Math.Pow(u, Exponent);
        }

        /// <summary>
        /// How fast the front is moving, in m/s, a fraction u of the way through its life: the
        /// derivative of FrontRadius. It starts several times the average and decays throughout.
        /// </summary>
        public static float FrontSpeed(float radius, float duration, float u)
        {
            if (radius <= 0f || duration <= 0f) return 0f;
            if (u < MinFraction) u = MinFraction;
            if (u > 1f) u = 1f;
            return Exponent * radius / duration * (float)Math.Pow(u, Exponent - 1.0);
        }
    }
}
