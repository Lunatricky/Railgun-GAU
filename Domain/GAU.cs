using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
namespace IngameScript.Domain
{
    partial class GAU
    {
        public string errorString = "";
        public string warningString = "";
        public string startString = "";

        public List<IMySmallMissileLauncherReload> RailgunBlockList { get; }
        public List<IMyDoor> DoorBlockList { get; }
        public List<IMyMotorStator> RotorBlockList { get; }


        public GAU()
        {}

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

        public string GAU_GROUP_NAME = "GAU";
        public string MAIN_ROTOR_NAME = "GAU Rotor";

        public float TORQUE = 100000000000f;
        public float TORQUENORMAL = 33600000;
        public float RPM = -30f;

        // Configurable variables
        public float shootDelay;  // Shooting delay in seconds
        public readonly float targetAngle = 0; // Target angle in degrees

        public GAUCommandEnum GAUTempCommand;
        public float rotationAngle;
        public bool areBlocksMissing = false;
        public float originPlaneAngleOffset = 0;

        public IMyMotorStator GAUCenterBlock;

        public Vector3I circleCenter = new Vector3I();
        public Vector3I circleCenter2 = new Vector3I();
        public Vector3I thridPoint = new Vector3I();

        public readonly List<IMySmallMissileLauncherReload> railgunBlockList = new List<IMySmallMissileLauncherReload>();
        public IMySmallMissileLauncherReload railgunReloadCheck;
        public readonly List<IMyDoor> doorBlockList = new List<IMyDoor>();
        public readonly List<IMyMotorStator> rotorBlockList = new List<IMyMotorStator>();

        public List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();
        public List<IMySmallMissileLauncherReload> tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>();
        public List<IMySmallMissileLauncherReload> tempRailgunListOff = new List<IMySmallMissileLauncherReload>();

        public string referenceBlockName = "Main Cockpit";  // Change this to your reference block name
        public string exhaustName = "Exhaust Cap";          // Must be part of exhaust block names
        public double groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair
        public int stepDelayTicks = 5;                     // Delay between group activations (~0.5s at 60 ticks/sec)

        public IMyTerminalBlock reference;
        public readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        public int state = 0;          // Which step we're on
        public int tickCounter = 0;    // Delay counter

        public float exhaustEffectDelay;
        public float fireDelay;
        
        public void GetBlocks(IMyGridTerminalSystem gridTerminalSystem, IMyCubeGrid CubeGrid)
        {
            errorString = "";
            warningString = "";
            startString = "";
            IMyBlockGroup GAUBlockGroup = gridTerminalSystem.GetBlockGroupWithName(GAU_GROUP_NAME);

            GAUBlockGroup?.GetBlocksOfType(RailgunBlockList);
            GAUBlockGroup?.GetBlocksOfType(DoorBlockList);
            GAUBlockGroup?.GetBlocksOfType(RotorBlockList);

            if (AreBlocksMissingFromGroupErrorMessage(RailgunBlockList, "Railgun") || AreBlocksMissingFromGroupErrorMessage(RotorBlockList, "Rotor"))
            {
                return;
            }

            AreBlocksMissingFromGroupWarningMessage(DoorBlockList, "Door");

            if (!(RotorBlockList.Count == 1 || RotorBlockList.Count == 2))
            {
                errorString = errorString + "\n" + $"Scrip only works with 1 or 2 rotors no more no less";
                areBlocksMissing = true;
                return;
            }

            Initialize(gridTerminalSystem);

            if (!SetRotorOrRotors(TORQUENORMAL))
            {
                errorString = errorString + "\n" + $"No rotor named {MAIN_ROTOR_NAME} found in group";
                areBlocksMissing = true;
                return;
            }

            CubeGrid = GAUCenterBlock.CubeGrid;
            VectorOffsets();

            bool isLG = RailgunBlockList.First().CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large);
            RailgunChargeStateEnum.CHARGED = (isLG ? RailgunChargeStateEnumLG.CHARGED : RailgunChargeStateEnumSG.CHARGED);
            RailgunChargeStateEnum.ALMOST = (isLG ? RailgunChargeStateEnumLG.ALMOST : RailgunChargeStateEnumSG.ALMOST);
            shootDelay = (isLG ? RailgunInfo.LG : RailgunInfo.SG);
            rotationAngle = (isLG ? RailgunInfo.rotationAngleLG : RailgunInfo.rotationAngleSG);

            originPlaneAngleOffset = ShootDelayOffsetAngle();
            fireDelay = shootDelay * 60;
            ExhaustReset();
        }

        private bool AreBlocksMissingFromGroupErrorMessage<T>(List<T> list, string blockType)
        {
            if (list?.Count == 0)
            {
                errorString = errorString + "\n" + $"No {blockType} block found in group";
                return true;
            }
            else
            {
                startString = startString + "\n" + $"{blockType} count: {list.Count}";
                return false;
            }
        }

        private bool AreBlocksMissingFromGroupWarningMessage<T>(List<T> list, string blockType)
        {
            if (list?.Count == 0)
            {
                warningString = warningString + "\n" + $"No {blockType} block found in group";
                return true;
            }
            else
            {
                startString = startString + "\n" + $"{blockType} count: {list.Count}";
                return false;
            }
        }

        public bool SetRotorOrRotors(float torque)
        {
            bool MainRotorExists = false;

            if (RotorBlockList.Count == 1)
            {
                ConfigureGAURotors(RotorBlockList.First(), TORQUE, RPM);
                MainRotorExists = true;
            }
            else
            {
                foreach (IMyMotorStator rotor in RotorBlockList)
                {
                    if (rotor.CustomName.Contains(MAIN_ROTOR_NAME))
                    {
                        ConfigureGAURotors(rotor, torque, RPM);
                        MainRotorExists = true;
                    }
                    else
                    {
                        ConfigureGAURotors(rotor, torque, -1 * RPM);
                    }
                }
            }
            return MainRotorExists;
        }

        private void ConfigureGAURotors(IMyMotorStator motorStator, float torque, float targetVelocityRPM)
        {
            motorStator.Torque = torque;
            motorStator.BrakingTorque = torque;
            motorStator.TargetVelocityRPM = targetVelocityRPM;
            GAUCenterBlock = motorStator;
            circleCenter = GAUCenterBlock.Position;
        }

        public void Initialize(IMyGridTerminalSystem gridTerminalSystem)
        {
            reference = gridTerminalSystem.GetBlockWithName(referenceBlockName);
            if (reference == null)
            {
                errorString = errorString + "\n" + $"No block named {referenceBlockName} found in group";
                return;
            }
            // Collect all exhaust caps
            List<IMyFunctionalBlock> allExhausts = new List<IMyFunctionalBlock>();
            gridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock>(allExhausts, b => b.CustomName.Contains(exhaustName));

            // Build a list of exhausts + distances
            List<IMyFunctionalBlock> sortedExhausts = new List<IMyFunctionalBlock>(allExhausts);
            sortedExhausts.Sort(delegate (IMyFunctionalBlock a, IMyFunctionalBlock b)
            {
                double da = Vector3D.Distance(reference.GetPosition(), a.GetPosition());
                double db = Vector3D.Distance(reference.GetPosition(), b.GetPosition());
                return da.CompareTo(db);
            });

            // Group exhausts by approximate distance
            exhaustLists.Clear();
            foreach (IMyFunctionalBlock sortedExhaust in sortedExhausts)
            {
                double dist = Vector3D.Distance(reference.GetPosition(), sortedExhaust.GetPosition());
                bool placed = false;

                foreach (List<IMyFunctionalBlock> exhaustList in exhaustLists)
                {
                    double groupDist = Vector3D.Distance(reference.GetPosition(), exhaustList[0].GetPosition());
                    if (Math.Abs(groupDist - dist) < groupTolerance)
                    {
                        exhaustList.Add(sortedExhaust);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    List<IMyFunctionalBlock> newGroup = new List<IMyFunctionalBlock>();
                    newGroup.Add(sortedExhaust);
                    exhaustLists.Add(newGroup);
                }
            }

            state = 0;
            tickCounter = 0;
        }

        public float ShootDelayOffsetAngle()
        {
            float anglesPerSecond = 360 * RPM / 60;
            return -1 * anglesPerSecond * shootDelay;
        }

        private void VectorOffsets()
        {
            Vector3I behind = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Backward);
            Vector3I below = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Down);

            circleCenter2 = behind;
            thridPoint = below;
        }
        public void ExhaustReset()
        {
            state = 0;
            tickCounter = 0;
            exhaustEffectDelay = (stepDelayTicks + 1) * exhaustLists.Count;
        }
    }
}
