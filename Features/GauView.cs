using Sandbox.ModAPI.Ingame;
using System.Linq;
using System.Text;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        private StringBuilder InfoString()
        {
            StringBuilder _infoString = new StringBuilder();
            _infoString.AppendLine(new string('-', 28));
            _infoString.AppendLine(_groupName);
            _infoString.AppendLine("Railguns:");

            int workingRailguns = 0;
            foreach (IMyFunctionalBlock railgun in RailgunBlockList)
            {
                if (!railgun.Closed && railgun.IsFunctional) workingRailguns++;
            }

            _infoString.AppendLine($" -working: {workingRailguns} / {RailgunBlockList.Count}");

            int chargedRailgunCounter = 0;
            foreach (IMySmallMissileLauncherReload railgun in RailgunBlockList)
            {
                chargedRailgunCounter = CheckRailgunChargeStateCounter(chargedRailgunCounter, railgun, _railGunChargeStateDetailedInfoString);
            }

            _infoString.AppendLine($" -charged: {chargedRailgunCounter} / {workingRailguns}");

            _infoString.AppendLine($"Rotor position: {GetRotorAngle360(RotorBlockList.First())}");

            if (DoorBlockList.Count > 0)
            {
                int workingDoors = 0;
                foreach (IMyFunctionalBlock door in DoorBlockList)
                {
                    if (!door.Closed && door.IsFunctional) workingDoors++;
                }

                _infoString.Append($"Doors working: {workingDoors} / {DoorBlockList.Count}");
            }
            return _infoString;
        }
    }
}
