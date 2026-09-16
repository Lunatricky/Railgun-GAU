using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        private bool IsCharged
        {
            get
            {
                bool isCharged = false;
                int chargedCounter = 0;

                if (tempRailgunListIsCharging.Count == 0)
                {
                    tempRailgunListIsCharging = new List<IMySmallMissileLauncherReload>(RailgunBlockList);
                }

                for (int i = tempRailgunListIsCharging.Count - 1; i >= 0; i--)
                {
                    var railgun = tempRailgunListIsCharging[i];
                    int checkCounter = CheckRailgunChargeStateCounter(chargedCounter, railgun, _railGunChargeStateDetailedInfoString);

                    if (checkCounter != chargedCounter)
                    {
                        chargedCounter = checkCounter;
                        railgun.Enabled = false;
                        tempRailgunListIsCharging.RemoveAt(i);
                    }
                }

                if (tempRailgunListIsCharging.Count == 0)
                {
                    isCharged = true;
                    tempRailgunListShootSalvo.Clear();
                }

                return isCharged;
            }
        }

        private bool IsAlmostCharged
        {
            get
            {
                if (!IsBlockMissing(railgunReloadCheck) && RailgunLooksFullyCharged(railgunReloadCheck))
                    return true;

                foreach (IMySmallMissileLauncherReload railgun in RailgunBlockList)
                {
                    if (RailgunLooksFullyCharged(railgun))
                        return true;
                }

                return false;
            }
        }

        private static string RailgunChargeInfo(IMySmallMissileLauncherReload railgun)
        {
            string detailed = railgun.DetailedInfo ?? "";
            string custom = railgun.CustomInfo ?? "";
            if (custom.Length == 0)
                return detailed;
            if (detailed.Length == 0)
                return custom;
            return detailed + "\n" + custom;
        }

        private bool RailgunLooksFullyCharged(IMySmallMissileLauncherReload railgun)
        {
            if (railgun == null || !railgun.IsFunctional)
                return false;
            return DetailLooksFullyCharged(RailgunChargeInfo(railgun), _railGunChargeStateDetailedInfoString);
        }

        private bool DetailLooksFullyCharged(string detailString, string railgunChargeState)
        {
            if (string.IsNullOrEmpty(detailString))
                return false;

            if (!string.IsNullOrEmpty(railgunChargeState)
                && detailString.IndexOf(railgunChargeState, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            double kwh;
            if (!TryGetStoredKwh(detailString, out kwh))
                return false;

            double fullKwh = isLG ? 500.0 : 16.0;
            return kwh + 0.05 >= fullKwh;
        }

        private static bool TryGetStoredKwh(string detailString, out double kwh)
        {
            kwh = 0;
            int idx = detailString.IndexOf("Stored Power:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            string rest = detailString.Substring(idx);
            int nlPos = rest.IndexOf('\n');
            if (nlPos < 0)
                nlPos = rest.IndexOf('\r');
            if (nlPos >= 0)
                rest = rest.Substring(0, nlPos);

            int colon = rest.IndexOf(':');
            if (colon >= 0)
                rest = rest.Substring(colon + 1);

            rest = rest.Replace('\u00A0', ' ').Trim();
            string[] parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            double val;
            if (!double.TryParse(parts[0], out val))
                return false;

            string unit = parts.Length > 1 ? parts[1] : "kWh";
            if (unit.IndexOf("MWh", StringComparison.OrdinalIgnoreCase) >= 0)
                kwh = val * 1000.0;
            else if (unit.Equals("Wh", StringComparison.OrdinalIgnoreCase))
                kwh = val / 1000.0;
            else
                kwh = val;
            return true;
        }

        private int CheckRailgunChargeStateCounter(int chargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            if (RailgunLooksFullyCharged(railgun))
            {
                chargeCounter++;
            }

            return chargeCounter;
        }

        private static bool CheckRailgunChargeState(IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            return railgun.IsFunctional && railgun.DetailedInfo.Contains(railgunChargeState);
        }
    }
}
