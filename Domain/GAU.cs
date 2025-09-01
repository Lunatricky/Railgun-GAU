using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;
using VRage.Game.ModAPI.Ingame;

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

        private const string GAU_GROUP_NAME = "GAU";
        private const string MAIN_ROTOR_NAME = "GAU Rotor";

        private const float TORQUE = 100000000000f;
        private const float RPM = -30f;

        // Configurable variables
        private float shootDelay;  // Shooting delay in seconds
        private readonly float targetAngle = 0; // Target angle in degrees

        private GAUCommandEnum GAUTempCommand;
        private float rotationAngle;
        private bool areBlocksMissing = false;
        private float originPlaneAngleOffset = 0;

        private IMyMotorStator GAUCenterBlock;

        private IMyCubeGrid CubeGrid;

        private Vector3I circleCenter = new Vector3I();
        private Vector3I circleCenter2 = new Vector3I();
        private Vector3I thridPoint = new Vector3I();

        private readonly List<IMySmallMissileLauncherReload> railgunBlockList = new List<IMySmallMissileLauncherReload>();
        private IMySmallMissileLauncherReload railgunReloadCheck;
        private readonly List<IMyDoor> doorBlockList = new List<IMyDoor>();
        private readonly List<IMyMotorStator> rotorBlockList = new List<IMyMotorStator>();

        private List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListOff = new List<IMySmallMissileLauncherReload>();


        private const string referenceBlockName = "Main Cockpit";  // Change this to your reference block name
        private const string exhaustName = "Exhaust Cap";          // Must be part of exhaust block names
        private const double groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair
        private const int stepDelayTicks = 5;                     // Delay between group activations (~0.5s at 60 ticks/sec)

        private IMyGridTerminalSystem gridTerminalSystem;

        private IMyTerminalBlock reference;
        private readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        private int state = 0;          // Which step we're on
        private int tickCounter = 0;    // Delay counter
    }
}
