using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>Mod-wide constants and shared logging.</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[MissileDisaster] ";

        // Hotkey that opens the manual launch tool; F9, to avoid Alien Invasion's F7.
        public const KeyCode ManualTriggerKey = KeyCode.F9;

        // Flight, driven on the main thread by simulationTimeDelta.
        // Only the descending half of the trajectory is interpolated: from an apex at high
        // altitude on a fixed bearing, down to the impact.
        public const float MissileSpeed = 900f;            // descent pace, in metres per second against the horizontal distance
        public const float IncomingBearingDegrees = 315f;  // bearing they arrive from, clockwise from north; 315 is north-west, and every missile shares it
        public const float ApexHorizontalOffset = 2200f;   // horizontal offset of the apex in metres; larger means a shallower angle
        public const float ApexAltitude = 4000f;           // height of the apex above the ground in metres; higher means a steeper dive from further up

        // Model for the incoming missile, Models/&lt;name&gt;.obj, with +Z as the nose.
        public const string ModelsFolderName = "Models";
        public const string IncomingMissileModelName = "IncomingWarhead";
        // The mushroom cloud mesh, Models/MushroomCloud.obj: normalised to height 1 with its
        // base at y=0, textured, procedurally generated (original work, fixed seed) by
        // tools/cloud-model/generate.py.
        public const string MushroomCloudModelName = "MushroomCloud";
        public const float IncomingMissileScale = 9f;      // takes the ~2 m model to about 19 m in game
        public const float ObjMetallic = 0.6f;             // the Standard shader's metallic parameter
        public const float ObjGlossiness = 0.5f;           // the Standard shader's smoothness parameter
        public static readonly Color ObjFallbackColor = new Color(0.25f, 0.25f, 0.25f, 1f); // fallback when the MTL is missing

        // The burning trail on an incoming missile, in the style of a meteor, created on the
        // main thread.
        // It deliberately does not streak: short lifetimes keep the particles near the body and
        // leave no wake behind, and the smoke is thin and sparse.
        public const float TrailFireRate = 70f;       // sparks emitted per second
        public const float TrailFireLifetime = 0.3f;  // lifetime of a spark in seconds; short, so it does not streak
        public const float TrailFireSize = 10f;       // base size of a spark in metres
        public const float TrailFireSpeed = 1.5f;     // initial spread speed of a spark, m/s; low, so they stay near the body
        public const float TrailSmokeRate = 14f;      // smoke puffs emitted per second; kept sparse
        public const float TrailSmokeLifetime = 0.45f;// lifetime of a puff in seconds; short, so no wake is left
        public const float TrailSmokeSize = 18f;      // base size of a puff in metres
        public static readonly Color TrailFireCoreColor = new Color(1f, 0.85f, 0.35f, 1f);  // bright yellow-orange core
        public static readonly Color TrailFireEdgeColor = new Color(0.9f, 0.28f, 0.06f, 1f); // red-orange edge
        public static readonly Color TrailSmokeColor = new Color(0.16f, 0.15f, 0.14f, 0.2f); // dark, thin smoke

        // The interceptor sites are ordinary assets made in the Asset Editor - PAC3, THAAD,
        // Aegis and Radar. This mod does not override their cost, power or water: it detects
        // the placed buildings by name and does nothing but resolve interceptions.
        // The interception logic, the building scan and the cooldowns all run on the main
        // thread, from MissileManager.UpdateVisual.
        public const int InterceptorScanIntervalFrames = 30;  // how often the buildings are rescanned, about 0.5 s at 60 fps
        public const float RadarSupportMultiplier = 1.5f;     // multiplier on the hit probability while a radar is operating

        // Flash on a successful interception: a simple particle burst owing nothing to the base
        // game. Main thread.
        public const int InterceptFlashBurst = 40;            // sparks emitted in the burst
        public const float InterceptFlashLifetime = 0.5f;     // lifetime of a spark in seconds
        public const float InterceptFlashSpeed = 60f;         // initial spread speed of a spark, m/s
        public const float InterceptFlashSize = 40f;          // base size of a spark in metres
        public static readonly Color InterceptFlashCoreColor = new Color(1f, 0.95f, 0.7f, 1f);  // white-orange core
        public static readonly Color InterceptFlashEdgeColor = new Color(1f, 0.55f, 0.15f, 1f); // orange edge

        // The interceptor missile itself, visible and on the main thread. It really is fired
        // from the launcher, whether or not it goes on to hit.
        // The model is Models/<name>.obj with +Z as the nose, and each layer gets a speed close
        // to its real counterpart.
        public const string InterceptorModelPac = "Interceptor_PAC";     // PAC-3
        public const string InterceptorModelThaad = "Interceptor_THAAD"; // THAAD
        public const string InterceptorModelArrow = "Interceptor_SM";    // SM-3(Aegis)
        public const float InterceptorModelScale = 6f;
        public const float InterceptorSpeedPac = 1700f;    // PAC-3 ~Mach5
        public const float InterceptorSpeedThaad = 2500f;  // THAAD ~Mach8
        public const float InterceptorSpeedArrow = 3000f;  // SM-3 ~Mach10
        public const float InterceptorCatchRadius = 60f;   // how close it must get to count as reaching the intercept point (m)
        public const float InterceptorMaxFlightSeconds = 8f; // safety net: it disappears after this if it never arrives
        public const int InterceptFizzleBurst = 14;        // particles in the small puff a miss leaves behind

        // Exhaust trail of an interceptor. Unlike the incoming missile's, this one does leave a
        // wake: the smoke lingers a while, in world space.
        public const float ExhaustFireRate = 90f;          // flame particles emitted per second at the nozzle
        public const float ExhaustFireLifetime = 0.25f;    // lifetime of a flame particle in seconds
        public const float ExhaustFireSize = 8f;           // base size of a flame particle in metres
        public const float ExhaustSmokeRate = 60f;         // smoke particles emitted per second
        public const float ExhaustSmokeLifetime = 2.5f;    // lifetime of a smoke particle in seconds; long, so the wake lingers
        public const float ExhaustSmokeSize = 7f;          // base size of a smoke particle in metres; kept narrow
        public static readonly Color ExhaustFireColor = new Color(1f, 0.9f, 0.6f, 1f);         // white-orange flame
        public static readonly Color ExhaustSmokeColor = new Color(0.85f, 0.85f, 0.85f, 0.32f); // thin whitish smoke

        // Impact of a conventional warhead; DisasterHelpers is called on the simulation thread.
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        // Radioactive contamination, for nuclear warheads only, written to
        // NaturalResourceManager on the simulation thread. The basic settings follow
        // NuclearMeltdown.
        public const byte ContaminationMaxIntensity = 255;   // peak intensity at the centre (0-255)
        public const int ContaminationExpiryYears = 50;      // a zone lifts on its own after this many in-game years
        // How often, in ticks, a zone is reasserted to counteract the game's natural decay.
        // Spacing it out keeps a large contamination radius from becoming expensive.
        public const int ContaminationMaintainInterval = 128;
        // Note that, unlike NuclearMeltdown, a water treatment plant does not decontaminate
        // here. Only a dedicated "Decontamination facility" building does: an operating building
        // whose name contains the keyword below, near a zone, removes
        // DecontaminationMonthlyFraction of what remains per in-game month.
        public const string DecontaminationKeyword = "Decontamination";
        public const float DecontaminationMonthlyFraction = 0.05f; // 5% of what remains, per month
        public const float DecontaminationFacilityRange = 1000f;    // a facility within the zone radius plus this decontaminates it
        public const byte DecontaminationMinIntensity = 5;          // a zone that falls to this or below disappears

        // Destruction is modelled as concentric rings: everything within
        // DestructionCoreFraction of the radius is destroyed outright, and beyond that the
        // probability falls off towards the full destruction radius. The ratio of about 0.2
        // follows Nukemap's near-total-destruction to residential-destruction radii.
        public const float DestructionCoreFraction = 0.2f;

        // Ceiling on the crater. This is an engineering safety limit that keeps the terrain
        // heightmap intact, not a balance decision: the real damage radii are expressed through
        // the destruction, fires and contamination, and only the crater - which deforms the
        // terrain - is rounded down.
        // The limit is soft (see MissileDisaster.Core.EffectCeiling): every yield up to about
        // 2 Mt digs its real hole, and beyond that the hole keeps widening towards the ceiling
        // instead of every strategic warhead leaving the same 500 m bowl. Castle Bravo's crater
        // really was about a kilometre across the radius, so the ceiling is not generous.
        public const float CraterRadiusKnee = 500f;
        public const float CraterRadiusMax = 900f;
        public const float CraterDepthKnee = 80f;
        public const float CraterDepthMax = 120f;

        // Engineering safety ceiling on the destruction and burn radii. A high-yield nuclear
        // weapon's real radii exceed the map, so this is the valve that stops DestroyStuff from
        // freezing the game on an extreme scan. It is not a balance decision.
        // The map is 17.3 km square, so its diagonal is 24.4 km: that is the reach a warhead
        // needs to touch the far corner from the near one, and the ceiling is set there. Nothing
        // beyond it can do anything, and the scan does not cost what the radius suggests - the
        // game's grids clamp to the map, so the work is bounded by the map rather than by the
        // radius asked for.
        public const float MaxEffectRadius = 24400f;
        // Ceiling on the contamination radius. The ground pollution grid spans about plus or
        // minus 8.6 km, so anything beyond this already covers the map and only wastes scan time.
        public const float MaxContaminationRadius = 8600f;

        // Impact explosion, borrowing the game's meteor impact effect and dispatched on the
        // main thread. A conventional or thermobaric warhead gets a single effect, a scattering
        // warhead gets one per submunition, and a nuclear warhead gets a single very large one
        // plus a mushroom cloud. How large each one is played is worked out from the yield by
        // MissileDisaster.Core.ExplosionScale.

        // Ceiling on how high above the target an airburst detonates. Partly an engineering
        // limit - the descent is only interpolated from ApexAltitude, so a burst altitude
        // approaching it would leave the missile no distance to fall - and partly a playability
        // one, since an explosion much higher than this is off the top of the screen at the zoom
        // the game is normally played at.
        // It is that second reading - where the top of the screen is - that the mushroom cloud
        // is also held under, so the two share one number rather than drifting apart.
        public const float MaxBurstAltitude = NuclearCloudDisplay.ScreenTopAltitude;

        // Sound: loaded from Sounds at runtime and played as positional 3D audio. Main thread.
        public const string SoundsFolderName = "Sounds";
        public const float SoundVolumeNormal = 0.5f;      // volume of the ordinary effects (0-1)
        public const float SoundVolumeNuclear = 1.0f;     // the nuclear blast is twice as loud; AudioSource caps at 1.0, so the others sit at 0.5
        // Minimum and maximum distances for the 3D rolloff, in metres: full volume inside the
        // minimum, silent at the maximum.
        public const float SoundLaunchMinDistance = 300f;
        public const float SoundLaunchMaxDistance = 8000f;   // the launch is audible a long way off, fading with distance
        public const float SoundExplosionMinDistance = 200f;
        public const float SoundExplosionMaxDistance = 5000f;
        public const float SoundNuclearMinDistance = 600f;
        public const float SoundNuclearMaxDistance = 16000f;  // the nuclear blast carries across the whole map
        public const float SoundInterceptMinDistance = 150f;
        public const float SoundInterceptMaxDistance = 4000f;

        // The warhead selection panel: main thread, and a permanent child of UIView.
        public const float PanelPosX = 16f;    // position from the left of the screen
        public const float PanelPosY = 200f;   // position from the top of the screen
        public const float PanelWidth = 264f;
        public const float PanelButtonHeight = 26f;
        public const float PanelButtonGap = 4f;

        // The launch button attached to the vanilla DisastersPanel, done the same way Alien
        // Invasion's button is.
        public const float TabButtonWidth = 46f;
        public const float TabButtonHeight = 36f;
        public const float TabButtonOffsetX = 8f;   // inner margin from the panel's right edge
        public const float TabButtonOffsetY = -40f; // Y relative to the panel's top edge; negative lifts it above the disaster icon row
        public const int TabButtonFallbackFrames = 600; // frames to wait for the disasters panel before falling back

        // Whether verbose logging is on. It is false in a release, to keep the log quiet; set
        // it to true and rebuild only when investigating a problem. Errors are always logged.
        // It is static readonly rather than const to avoid the unreachable-code warning CS0162.
        public static readonly bool DebugLogging = false;

        public static void Log(string msg) { if (DebugLogging) Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }

        /// <summary>
        /// Logs whatever DebugLogging is set to. Reserved for the handful of lines that answer
        /// "which build is actually running" - a question a quiet log cannot answer, and the one
        /// that has to be settled before any report of "it did not change" means anything.
        /// </summary>
        public static void LogAlways(string msg) { Debug.Log(LogPrefix + msg); }
    }
}
