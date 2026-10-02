using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript.Domain
{
    partial class Gau
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

        public void PaintLcds()
        {
            if (LcdBlockList == null || LcdBlockList.Count == 0)
                return;

            if (!_lcdSprite)
            {
                string text = Info.ToString();
                for (int i = 0; i < LcdBlockList.Count; i++)
                {
                    IMyTextSurface surface = LcdBlockList[i];
                    if (surface != null)
                        surface.WriteText(text);
                }
                return;
            }

            GauLcdSprites spt = new GauLcdSprites();
            AddSpriteRows(spt);
            spt.DrawTo(LcdBlockList);
        }

        public static void PaintCockpits(List<Gau> gaus)
        {
            if (s_cockpitSurfaces == null || s_cockpitSurfaces.Count == 0)
                return;
            if (gaus == null || gaus.Count == 0)
                return;

            GauLcdSprites spt = new GauLcdSprites();
            for (int i = 0; i < gaus.Count; i++)
            {
                Gau gau = gaus[i];
                if (gau != null)
                    gau.AddSpriteRows(spt);
            }
            spt.DrawTo(s_cockpitSurfaces);
        }

        void AddSpriteRows(GauLcdSprites spt)
        {
            spt.Add(_groupName ?? "");
            spt.Add("Cycle: " + CycleLabel());

            int working = 0;
            for (int i = 0; i < RailgunBlockList.Count; i++)
            {
                IMyFunctionalBlock railgun = RailgunBlockList[i];
                if (railgun != null && !railgun.Closed && railgun.IsFunctional)
                    working++;
            }
            spt.Add("Railguns: " + working + " / " + RailgunBlockList.Count);

            int charged = 0;
            for (int i = 0; i < RailgunBlockList.Count; i++)
            {
                charged = CheckRailgunChargeStateCounter(charged, RailgunBlockList[i], _railGunChargeStateDetailedInfoString);
            }
            spt.Add("Charged: " + charged + " / " + working);

            if (RotorBlockList.Count > 0)
                spt.Add("Rotor: " + GetRotorAngle360(RotorBlockList[0]));

            if (DoorBlockList.Count > 0)
            {
                int doors = 0;
                for (int i = 0; i < DoorBlockList.Count; i++)
                {
                    IMyFunctionalBlock door = DoorBlockList[i];
                    if (door != null && !door.Closed && door.IsFunctional)
                        doors++;
                }
                spt.Add("Doors: " + doors + " / " + DoorBlockList.Count);
            }

            if (_errorBuilder.Length > 0)
                spt.AddB("ERRORS", Color.DarkRed);
            if (_warningBuilder.Length > 0)
                spt.AddB("WARNINGS", Color.DarkOrange);
        }

        string CycleLabel()
        {
            if (GAUState == GauActionEnum.ON) return "ON";
            if (GAUState == GauActionEnum.OFF) return "OFF";
            if (GAUState == GauActionEnum.STANDBY) return "STANDBY";
            if (GAUState == GauActionEnum.READY) return "READY";
            if (GAUState == GauActionEnum.FIRE) return "FIRE";
            if (GAUState == GauActionEnum.FIRESTATE) return "FIRESTATE";
            if (GAUState == GauActionEnum.CANCEL) return "CANCEL";
            if (GAUState == GauActionEnum.RELOAD) return "RELOAD";
            if (GAUState == GauActionEnum.CHARGE) return "CHARGE";
            if (GAUState == GauActionEnum.CHARGING) return "CHARGING";
            if (GAUState == GauActionEnum.ALMOSTCHARGED) return "ALMOSTCHARGED";
            if (GAUState == GauActionEnum.EXHAUST) return "EXHAUST";
            if (GAUState == GauActionEnum.EXHAUSTEFFECT) return "EXHAUSTEFFECT";
            if (GAUState == GauActionEnum.EXHAUSTFIRE) return "EXHAUSTFIRE";
            return "";
        }
    }
}
