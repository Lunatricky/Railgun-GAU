using Sandbox.ModAPI.Ingame;
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
                        tempRailgunListIsCharging.RemoveAt(i); // Safe in reverse
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
                bool result = false;

                if (!IsBlockMissing(railgunReloadCheck))
                {
                    return railgunReloadCheck.DetailedInfo.Contains(_railGunChargeStateDetailedInfoString);
                }
                else
                {
                    foreach (IMySmallMissileLauncherReload railgun in RailgunBlockList)
                    {
                        result = railgun.DetailedInfo.Contains(_railGunChargeStateDetailedInfoString);
                    }
                }
                return result;
            }
        }

        private static int CheckRailgunChargeStateCounter(int chargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            string detailString = railgun.DetailedInfo;

            if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
            {
                chargeCounter++;
            }

            return chargeCounter;
        }

        private static bool CheckRailgunChargeState(IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            string detailString = railgun.DetailedInfo;

            if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
            {
                return true;
            }
            return false;
        }
    }
}
