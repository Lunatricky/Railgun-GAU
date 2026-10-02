using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    /// <summary>
    /// Tagged-cockpit Custom Data: one 0-based surface index.
    /// Empty LCD = do not draw. Sync() rewrites the template.
    /// </summary>
    public class GauCockpitSpriteIni
    {
        public const string Section = "GAU";
        public const string LCD = "LCD";

        readonly MyIni ini = new MyIni();
        readonly IniSnapshot snapshot = new IniSnapshot();

        public string Lcd { get; private set; }

        public bool Sync(IMyTerminalBlock block)
        {
            ini.Clear();
            if (block == null || !ini.TryParse(block.CustomData ?? ""))
                ini.Clear();

            Lcd = ini.Get(Section, LCD).ToString("");

            bool changed = snapshot.ReadAndDetectChange(ini, Section, LCD, Lcd ?? "");
            block.CustomData = ini.ToString();
            return changed;
        }

        public static bool TryParseSlot(string value, int surfaceCount, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            int parsed;
            if (!int.TryParse(value.Trim(), out parsed))
                return false;

            if (parsed < 0 || parsed >= surfaceCount)
                return false;

            index = parsed;
            return true;
        }
    }
}
