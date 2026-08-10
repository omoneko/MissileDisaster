using System;
using ColossalFramework;
using ICities;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// One Chirper message, which is all IChirperMessage asks for.
    /// </summary>
    public class StrikeChirp : IChirperMessage
    {
        private readonly string _sender;
        private readonly string _text;

        public StrikeChirp(string sender, string text) { _sender = sender; _text = text; }

        public uint senderID { get { return 0u; } }
        public string senderName { get { return _sender; } }
        public string text { get { return _text; } }
    }

    /// <summary>
    /// Tells the player, in the game, that a missile that nobody launched has just hit their
    /// city - and where the switch is. Main thread only.
    ///
    /// This exists because of a Workshop comment: "your missile mod destroyed my whole new town,
    /// i did not know that random disaster exist". Random strikes have always shipped off, so
    /// that player had turned them on; the real failure was that nothing in the game connected
    /// the crater to the setting that caused it. A mod that can flatten a city owes the player
    /// that link, once, in words.
    ///
    /// The first strike of a session says what happened and how to stop it. After that it stays
    /// quiet - the player has been told, and a chirp per warhead would be its own nuisance.
    /// </summary>
    public static class StrikeNotice
    {
        private const string Sender = "Civil Defence";

        private const string FirstStrike =
            "MISSILE STRIKE on the city. This is the Missile Disaster mod's random strike mode - " +
            "switch it off in Options > Mods > Missile Disaster > Random missile strikes.";

        private static bool _warned;

        /// <summary>Called when a random strike is launched. Only the first one in a session says anything.</summary>
        public static void RandomStrikeLaunched()
        {
            if (_warned) return;
            _warned = true;
            Chirp(FirstStrike);
            // Also in the log, so it is answerable from a bug report where the chirp was missed.
            ModConfig.LogAlways(
                "random strike fired - this is the optional disaster mode, off by default, " +
                "toggled in Options > Mods > Missile Disaster");
        }

        /// <summary>Called on level unload, so the next city warns again.</summary>
        public static void Reset()
        {
            _warned = false;
        }

        private static void Chirp(string text)
        {
            try
            {
                ChirpPanel panel = Singleton<ChirpPanel>.instance;
                if (panel == null) return; // no Chirper in this view; the log line still stands
                panel.AddMessage(new StrikeChirp(Sender, text));
            }
            catch (Exception e)
            {
                // A missed notification must never cost the player their strike.
                ModConfig.LogError("StrikeNotice.Chirp error: " + e);
            }
        }
    }
}
