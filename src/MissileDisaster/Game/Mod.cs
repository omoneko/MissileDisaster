using ICities;

namespace MissileDisaster.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Missile Disaster";
        public string Description =>
            "Launch missiles (conventional now; more warheads coming) at any spot. " +
            "Press F9 or use the button, then click a target.";
    }
}
