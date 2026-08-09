using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Drives the mushroom cloud mesh through its life - grow, stand, fade - on the main
    /// thread's Update. All the shape of the animation lives in Core.CloudAnimation, where it is
    /// tested; this component only applies the state to the transform and the materials.
    ///
    /// The fade needs a shader that honours alpha. When the game has one, FadeMaterials carries
    /// the instance materials to write the alpha to; when it does not, FadeMaterials is null and
    /// the end of the cloud is covered instead: a burst of smoke at the cap swallows the mesh,
    /// the mesh is destroyed behind it, and the smoke thins away on its own.
    /// </summary>
    public class MushroomCloudAnimator : MonoBehaviour
    {
        public float HeightScale;    // localScale.y at full size
        public float WidthScale;     // localScale.x and .z at full size
        public float RiseSeconds;
        public float HoldSeconds;
        public float FadeSeconds;
        public float CapRadius;      // world metres, for the cover burst
        public float CloudTop;       // world metres, likewise
        public Material[] FadeMaterials; // null when the shader cannot fade

        private float _t;
        private bool _covered;

        private void Update()
        {
            _t += Time.deltaTime;
            CloudAnimationState s = CloudAnimation.At(_t, RiseSeconds, HoldSeconds, FadeSeconds);

            transform.localScale = new Vector3(
                WidthScale * s.WidthFraction, HeightScale * s.HeightFraction, WidthScale * s.WidthFraction);

            if (FadeMaterials != null)
            {
                for (int i = 0; i < FadeMaterials.Length; i++)
                {
                    if (FadeMaterials[i] == null) continue;
                    Color c = FadeMaterials[i].color;
                    c.a = s.Alpha;
                    FadeMaterials[i].color = c;
                }
            }
            else if (!_covered && _t >= RiseSeconds + HoldSeconds)
            {
                // No alpha to fade with: cover the cloud in its own smoke and take the mesh
                // down behind it.
                _covered = true;
                SpawnDissolveCover();
                Destroy(gameObject, 1.2f);
                return;
            }

            if (s.Finished) Destroy(gameObject);
        }

        /// <summary>A burst of cap-coloured smoke over the canopy and the column, large enough to hide both while they are removed.</summary>
        private void SpawnDissolveCover()
        {
            var go = ParticleBuilder.NewSystem("MushroomCloudDissolve",
                transform.position + Vector3.up * (CloudTop * 0.75f), ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 8f;
            main.startSpeed = CapRadius * 0.04f;
            main.startSize = CapRadius * 0.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.82f, 0.81f, 0.80f, 0.7f), new Color(0.62f, 0.60f, 0.58f, 0.7f));
            main.maxParticles = 96;

            ParticleBuilder.Burst(ps, 48);
            ParticleBuilder.Sphere(ps, CapRadius * 0.6f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.6f, 0.4f),
                new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.9f, 1.5f);
            ParticleBuilder.PlayAndDestroy(go, 9f);
        }
    }
}
