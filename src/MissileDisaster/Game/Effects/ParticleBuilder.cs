using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The handful of ParticleSystem calls every effect in this mod repeats: making the object,
    /// setting the emission shape, and the colour, size and speed curves over a particle's life.
    /// Main thread only.
    /// Everything simulates in world space, so a system detached from whatever spawned it keeps
    /// drifting where it was rather than snapping to the origin.
    /// </summary>
    public static class ParticleBuilder
    {
        /// <summary>A new GameObject carrying a world-space, billboard ParticleSystem, not yet playing.</summary>
        public static GameObject NewSystem(string name, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return go;
        }

        /// <summary>Everything at once, at t=0, and nothing after.</summary>
        public static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        // A staggered-burst emitter used to live here, to give a column of free-running particles
        // a stem instead of a clump that arrives at the top as a ball. It was not enough - drifting
        // particles cannot hold a silhouette at all - and both mushroom clouds are now placed puff
        // by puff every frame instead. See MushroomCloudPuffsFx.

        /// <summary>A steady stream, for anything that has to keep feeding while it lasts.</summary>
        public static void Stream(ParticleSystem ps, float perSecond)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = perSecond;
        }

        public static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        public static void Hemisphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // the dome's flat face down on the ground
        }

        /// <summary>An upward cone, which spreads particles outwards as they climb. The cone's +Z is turned upwards.</summary>
        public static void ConeUp(ParticleSystem ps, float radius, float angle)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        /// <summary>
        /// A horizontal filled disc: particles start anywhere inside the circle and travel
        /// straight outwards along it, staying in its plane. This is what a cloud cap spreads
        /// across - unlike a cone, nothing is sent upwards, so the canopy's depth is left to the
        /// particle size instead of being set by how far it spreads.
        /// </summary>
        public static void FlatDisc(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.arc = 360f;
            ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lay the disc flat
        }

        /// <summary>
        /// A horizontal ring lying on the ground: particles start on the circle and travel
        /// straight outwards along it. This is the shape a blast front is drawn with.
        /// </summary>
        public static void GroundRing(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.CircleEdge;
            shape.radius = radius;
            shape.arc = 360f;
            ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lay the circle flat, so it spreads across the ground
        }

        /// <summary>Colour over a particle's life, from a gradient of colour and alpha keys.</summary>
        public static void Colour(ParticleSystem ps, Gradient gradient)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>The common case: white throughout, with the alpha following the given keys.</summary>
        public static void Fade(ParticleSystem ps, params GradientAlphaKey[] alphaKeys)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys);
            Colour(ps, grad);
        }

        /// <summary>Size over a particle's life, as a multiplier of its start size.</summary>
        public static void SizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to)));
        }

        /// <summary>Caps a particle's speed at a fixed value, which makes it settle and spread instead of flying on.</summary>
        public static void LimitSpeed(ParticleSystem ps, float limit, float dampen)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.dampen = dampen;
            lv.limit = new ParticleSystem.MinMaxCurve(limit);
        }

        /// <summary>
        /// Drives a particle's speed along a curve of its life with the brakes fully on, so the
        /// speed follows the curve rather than merely being bounded by it. This is what lets a
        /// blast front decelerate the way the physics says it should.
        /// </summary>
        public static void SpeedCurve(ParticleSystem ps, AnimationCurve curve, float multiplier)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.dampen = 1f; // clamp hard to the curve every frame
            lv.limit = new ParticleSystem.MinMaxCurve(multiplier, curve);
        }

        public static void Gravity(ParticleSystem ps, float modifier)
        {
            var main = ps.main;
            main.gravityModifier = modifier;
        }

        /// <summary>A constant climb in world space, in m/s, for anything that has to rise as it lives.</summary>
        public static void Rise(ParticleSystem ps, float metresPerSecond)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(metresPerSecond);
        }

        /// <summary>
        /// A climb that changes over a particle's life, in m/s, for anything that has to rise and
        /// then stop - a cloud cap carried up by its own column and left there, rather than one
        /// that keeps going.
        /// </summary>
        public static void Rise(ParticleSystem ps, AnimationCurve metresPerSecond, float multiplier)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(multiplier, metresPerSecond);
        }

        /// <summary>
        /// Plays the system and has it clean itself up once the last particle has gone.
        /// The lifetime is in simulation seconds and the particles advance at the simulation's
        /// rate, so the whole effect pauses with the game and speeds up with it - see
        /// SimulationTimed. Everything the mod spawns goes through here, which is what keeps
        /// that from having to be remembered at each call site.
        /// </summary>
        public static void PlayAndDestroy(GameObject go, float lifetimeSeconds)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            var timed = go.AddComponent<SimulationTimed>();
            timed.LifetimeSeconds = lifetimeSeconds;
        }
    }
}
