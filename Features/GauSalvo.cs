using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
        {
            railgun.Enabled = true;
            railgun.ShootOnce();
        }

        private void RailgunShootSalvo()
        {

            if (tempRailgunListShootSalvo.Count == 0)
            {
                tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>(RailgunBlockList);
            }

            List<Plane> rotatedPlanes = getRotatedPlanes();

            foreach (var railgun in tempRailgunListShootSalvo.ToList())
            {
                if (IsPointBetweenAngles(rotatedPlanes, railgun.GetPosition()))
                {
                    ShootRailgun(railgun);
                    if (railgunReloadCheck == null)
                    {
                        railgunReloadCheck = railgun;
                    }
                }

                if (!railgun.DetailedInfo.Contains(_railGunChargeStateDetailedInfoString))
                {
                    tempRailgunListShootSalvo.Remove(railgun);
                }
            }

            if (tempRailgunListShootSalvo.Count == 0)
            {
                GAUState = GAUActionEnum.CHARGE;
                ExhaustOff();
            }
        }

        public bool IsPointBetweenAngles(List<Plane> rotatedPlanes, Vector3D railgunPosition)
        {
            float distanceToRotated = rotatedPlanes[0].SignedDistance(railgunPosition);
            float distanceToRotated2 = rotatedPlanes[1].SignedDistance(railgunPosition);

            return distanceToRotated2 > 0 && distanceToRotated < 0;
        }
    }
}
