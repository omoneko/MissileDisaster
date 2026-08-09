// Minimal stand-ins for the UnityEngine surface the effects use, written to the shapes Unity
// declares them with, so that the mod's own effect code can be compiled without the game.
//
// This proves the code is well-formed C# and that every call matches the arity and types Unity
// declares. It does NOT prove a member exists in Unity 5.6 - a stub compiles whatever is
// written in it. Anything whose existence is in doubt is listed in the report, not here.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator *(float f, Vector3 a) { return a * f; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
    }

    public struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a) { }
    }

    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
    }

    public static class Mathf
    {
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public const float Rad2Deg = 57.29578f;
    }

    public struct Keyframe
    {
        public Keyframe(float time, float value) { }
        public Keyframe(float time, float value, float inTangent, float outTangent) { }
    }

    public class AnimationCurve
    {
        public AnimationCurve(params Keyframe[] keys) { }
    }

    public struct GradientColorKey
    {
        public GradientColorKey(Color col, float time) { }
    }

    public struct GradientAlphaKey
    {
        public GradientAlphaKey(float alpha, float time) { }
    }

    public class Gradient
    {
        public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys) { }
    }

    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
    }

    public class Transform : Object
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localScale { get; set; }
    }

    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
        public T GetComponent<T>() where T : Component { return null; }
    }

    public class MonoBehaviour : Component { }

    public static class Time
    {
        public static float deltaTime { get { return 0f; } }
    }

    public static class Random
    {
        public static float value { get { return 0f; } }
    }

    public class Shader : Object { }
    public class Texture : Object { }
    public class Texture2D : Texture { }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public bool HasProperty(string name) { return false; }
        public void SetFloat(string name, float value) { }
        public void SetTexture(string name, Texture value) { }
    }

    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { }
        public Vector3 size { get { return new Vector3(0, 0, 0); } }
        public Vector3 extents { get { return new Vector3(0, 0, 0); } }
    }

    public class Mesh : Object
    {
        public Bounds bounds { get { return new Bounds(); } }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh { get; set; }
    }

    public class MeshRenderer : Component
    {
        public Material[] materials { get; set; }
        public Material[] sharedMaterials { get; set; }
    }

    public class GameObject : Object
    {
        public GameObject(string name) { }
        public Transform transform { get { return null; } }
        public T AddComponent<T>() where T : Component { return null; }
        public T GetComponent<T>() where T : Component { return null; }
    }

    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemSortMode { None, Distance, OldestInFront, YoungestInFront }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemShapeType
    {
        Sphere, SphereShell, Hemisphere, HemisphereShell, Cone, Box, Mesh, ConeShell,
        ConeVolume, ConeVolumeShell, Circle, CircleEdge, SingleSidedEdge, MeshRenderer,
        SkinnedMeshRenderer, BoxShell, BoxEdge, Donut, Rectangle, Sprite, SpriteRenderer
    }

    public class ParticleSystemRenderer : Component
    {
        public Material material { get; set; }
        public ParticleSystemRenderMode renderMode { get; set; }
        public ParticleSystemSortMode sortMode { get; set; }
        public float maxParticleSize { get; set; }
    }

    public class ParticleSystem : Component
    {
        public struct Particle
        {
            public Vector3 position { get; set; }
            public float startSize { get; set; }
            public float rotation { get; set; }
            public Color32 startColor { get; set; }
            public float remainingLifetime { get; set; }
            public float startLifetime { get; set; }
        }

        public void SetParticles(Particle[] particles, int size) { }

        public struct MinMaxCurve
        {
            public MinMaxCurve(float constant) { }
            public MinMaxCurve(float min, float max) { }
            public MinMaxCurve(float multiplier, AnimationCurve curve) { }
            public static implicit operator MinMaxCurve(float constant) { return new MinMaxCurve(constant); }
        }

        public struct MinMaxGradient
        {
            public MinMaxGradient(Color color) { }
            public MinMaxGradient(Color min, Color max) { }
            public MinMaxGradient(Gradient gradient) { }
            public static implicit operator MinMaxGradient(Color color) { return new MinMaxGradient(color); }
        }

        public struct Burst
        {
            public Burst(float time, short count) { }
        }

        public struct MainModule
        {
            public float duration { get; set; }
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxCurve startDelay { get; set; }
            public MinMaxGradient startColor { get; set; }
            public float gravityModifier { get; set; }
            public int maxParticles { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
        }

        public struct EmissionModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve rateOverTime { get; set; }
            public void SetBursts(Burst[] bursts) { }
        }

        public struct ShapeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemShapeType shapeType { get; set; }
            public float radius { get; set; }
            public float angle { get; set; }
            public float arc { get; set; }
        }

        public struct ColorOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxGradient color { get; set; }
        }

        public struct SizeOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve size { get; set; }
        }

        public struct LimitVelocityOverLifetimeModule
        {
            public bool enabled { get; set; }
            public float dampen { get; set; }
            public MinMaxCurve limit { get; set; }
        }

        public struct VelocityOverLifetimeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemSimulationSpace space { get; set; }
            public MinMaxCurve y { get; set; }
        }

        public MainModule main { get { return new MainModule(); } }
        public EmissionModule emission { get { return new EmissionModule(); } }
        public ShapeModule shape { get { return new ShapeModule(); } }
        public ColorOverLifetimeModule colorOverLifetime { get { return new ColorOverLifetimeModule(); } }
        public SizeOverLifetimeModule sizeOverLifetime { get { return new SizeOverLifetimeModule(); } }
        public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get { return new LimitVelocityOverLifetimeModule(); } }
        public VelocityOverLifetimeModule velocityOverLifetime { get { return new VelocityOverLifetimeModule(); } }
        public void Play() { }
    }
}

// The pieces of the mod the effect files lean on that are not UnityEngine and not in the
// compile list themselves.
namespace MissileDisaster.Game
{
    public static class ModConfig
    {
        public static void Log(string message) { }
        public static void LogError(string message) { }
        public static void LogAlways(string message) { }
    }
}

namespace MissileDisaster.Game.Effects
{
    public static class ParticleAssets
    {
        public static UnityEngine.Material Fire { get { return null; } }
        public static UnityEngine.Material Smoke { get { return null; } }
        public static UnityEngine.Material Cloud { get { return null; } }
    }

    public static class RenderAssets
    {
        public static UnityEngine.Shader FindFirst(params string[] names) { return null; }
        public static void ApplyDepthOcclusion(UnityEngine.Material mat) { }
    }

    public static class ExplosionFallback
    {
        public static void Play(UnityEngine.Vector3 center, float radius) { }
    }
}

namespace MissileDisaster.Game.Models
{
    public static class MissileModelProvider
    {
        public static UnityEngine.GameObject CreateInstance(string name) { return null; }
    }
}
