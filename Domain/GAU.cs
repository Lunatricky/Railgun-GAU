using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Domain
{
    partial class GAU
    {
        public List<IMySmallMissileLauncherReload> RailgunBlockList { get; }
        public List<IMyDoor> DoorBlockList { get; }
        public List<IMyMotorStator> RotorBlockList { get; }

        public GAU(List<IMySmallMissileLauncherReload> railgunBlockList, List<IMyDoor> doorBlockList, List<IMyMotorStator> rotorBlockList)
        {
            RailgunBlockList = railgunBlockList;
            DoorBlockList = doorBlockList;
            RotorBlockList = rotorBlockList;
        }

        public GAUCommandEnum GAUCommand
        {
            get { return GAUCommand; }
            set { GAUCommand = value; }
        }

        public float RotationAngle
        {
            get { return RotationAngle; }
            set { RotationAngle = value; }
        }
    }
}
