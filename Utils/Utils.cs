using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Utils
{
    partial class Utils
    {
        private static bool IsBlockMissingInList<T>(List<T> listT)
        {
            foreach (IMyTerminalBlock block in listT)
            {
                return block == null || block.Closed == true || !block.IsFunctional == true;
            }
            return false;
        }
    }
}
