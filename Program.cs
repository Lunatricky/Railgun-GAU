using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        private const string GAU_GROUP_NAME = "GAU";
        private const string MAIN_ROTOR_NAME = "GAU Rotor";

        private const float TORQUE = 100000000000f;
        private const float RPM = -30f;

        private const float shootTimeLG = 2.00f;
        private const float shootTimeSG = 0.50f;


        // Configurable variables
        private float shootDelay;  // Shooting delay in seconds
        private readonly float targetAngle = 0; // Target angle in degrees

        private string errorString = "";
        private string warningString = "";
        private string startString = "";

        private GAUCommandEnum GAUCommand;
        private GAUCommandEnum GAUTempCommand;
        private float rotationAngle;
        private readonly float rotationAngleSG = 5f;
        private readonly float rotationAngleLG = 3.5f;
        private bool areBlocksMissing = false;
        private bool hasRecompiled = true;
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

        private float exhaustEffectTicks;
        private float fireDelay;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            GetBlocks();
        }

        public void Main(string argument)
        {
            Echo($"Cycle: {GAUCommand}");
            Echo($"{startString}");
            Echo($"exhaustEffectTicks : {exhaustEffectTicks}");
            Echo($"fireDelay : {fireDelay}");

            if (argument != null && argument.Length != 0 && argument != "")
            {
                try
                {
                    GAUTempCommand = (GAUCommandEnum)Enum.Parse(typeof(GAUCommandEnum), argument, true);
                }
                catch
                {
                    GAUTempCommand = GAUCommandEnum.ON;
                }
            }

            if (errorString != "")
            {
                Echo($"{errorString}");
                Echo(InstructionCount());
                return;
            }

            if (GAUCommand == GAUCommandEnum.OFF && GAUTempCommand != GAUCommandEnum.ON)
            {
                Echo(InstructionCount());
                return;
            }

            if (GAUTempCommand != GAUCommandEnum.NULL &&
                GAUCommand != GAUCommandEnum.CHARGING && 
                GAUCommand != GAUCommandEnum.ALMOSTCHARGED &&
                GAUCommand != GAUCommandEnum.OPENINGDOOR &&
                GAUCommand != GAUCommandEnum.CLOSINGDOOR
                )
            {
                GAUCommand = GAUTempCommand;
                GAUTempCommand = GAUCommandEnum.NULL;
            } else if (GAUTempCommand == GAUCommandEnum.FIRE ||
                GAUTempCommand == GAUCommandEnum.EXHAUST ||
                GAUTempCommand == GAUCommandEnum.EXHAUSTON)
            {
                GAUTempCommand = GAUCommandEnum.NULL;
            }


            if (GAUCommand == GAUCommandEnum.FIRE || GAUCommand == GAUCommandEnum.EXHAUST || 
                GAUCommand == GAUCommandEnum.EXHAUSTON || GAUCommand == GAUCommandEnum.CHARGING)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            if (warningString.Length > 0)
            {
                Echo($"-----WARNINGS-----{warningString}\n------------------");
            }

            switch (GAUCommand)
            {
                case GAUCommandEnum.ON:
                case GAUCommandEnum.RESET:
                    GetBlocks();
                    ToggleBlocks(true, rotorBlockList);
                    ToggleBlocks(false, railgunBlockList);
                    OpenDoors();
                    if (IsCharged())
                    {
                        GAUCommand = GAUCommandEnum.READY;
                    } else
                    {
                        GAUCommand = GAUCommandEnum.CHARGE;
                    }
                    break;

                case GAUCommandEnum.OFF:
                    if (IsCharged())
                        Off();
                    break;

                case GAUCommandEnum.EXHAUST:
                    ExhaustReset();
                    GAUCommand = GAUCommandEnum.EXHAUSTON;
                    break;

                case GAUCommandEnum.EXHAUSTON:
                    if (fireDelay >= exhaustEffectTicks)
                    {
                        RailgunShootSalvo();
                    }
                    if (exhaustEffectTicks > 0)
                    {
                        exhaustEffectTicks--;
                        ExhaustEffect();
                    }
                    break;

                case GAUCommandEnum.FIRE:
                    if (IsDoorOpen())
                    {
                        RailgunShootSalvo();
                    }
                    break;

                case GAUCommandEnum.CLOSINGDOOR:
                    if (!IsDoorOpen())
                    {
                        CloseDoors();
                    }
                    else
                    {
                        GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                case GAUCommandEnum.OPENINGDOOR:
                    if (!IsDoorOpen())
                    {
                        OpenDoors();
                    }
                    else
                    {
                        GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                case GAUCommandEnum.CHARGE:
                    CloseDoors();
                    ToggleBlocks(true, railgunBlockList);
                    ToggleBlocks(false, rotorBlockList);
                    GAUCommand = GAUCommandEnum.CHARGING;
                    break;

                case GAUCommandEnum.CHARGING:
                    if (IsAlmostCharged())
                    {
                        railgunReloadCheck = null;
                        OpenDoors();
                        ToggleBlocks(true, rotorBlockList);
                        GAUCommand = GAUCommandEnum.ALMOSTCHARGED;
                    }
                    break;
                case GAUCommandEnum.ALMOSTCHARGED:
                    if (IsCharged())
                    {
                        OpenDoors();
                        ToggleBlocks(true, rotorBlockList);
                        GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                default:
                    if (hasRecompiled && !IsCharged())
                    {
                        GAUCommand = GAUCommandEnum.CHARGE;
                        hasRecompiled = false;
                    } else
                    {
                        ToggleBlocks(false, railgunBlockList);
                    }
                    break;
            }

            if (areBlocksMissing)
            {
                GAUCommand = GAUCommandEnum.RESET;
            }

            Echo(InstructionCount());
        }

        private String InstructionCount()
        {
            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }

        private void Off()
        {
            CloseDoors();

            if (tempRailgunListOff.Count == 0)
            {
                tempRailgunListOff = new List<IMySmallMissileLauncherReload>(railgunBlockList);
            }

            tempRailgunListOff.ForEach(railgun => railgun.Enabled = true);

            foreach (IMySmallMissileLauncherReload railgun in tempRailgunListOff)
            {
                if (IsBlockMissing(railgun))
                {
                    continue;
                }

                string detailString = railgun.DetailedInfo;

                if (!detailString.Contains(RailgunChargeStateEnum.CHARGED))
                {
                    tempRailgunListOff.Remove(railgun);
                    break;
                }
                else
                {
                    railgun.Enabled = false;
                }
            }

            if (tempRailgunListOff.Count == 0)
            {
                ToggleBlocks(false, railgunBlockList);
                ToggleBlocks(false, rotorBlockList);
            }
        }

        private void RailgunShootSalvo()
        {

            if (tempRailgunListShootSalvo.Count == 0)
            {
                tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>(railgunBlockList);
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

                if (!railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED))
                {
                    tempRailgunListShootSalvo.Remove(railgun);
                }
            }

            if (tempRailgunListShootSalvo.Count == 0)
            {
                GAUCommand = GAUCommandEnum.CHARGE;
                ExhaustOff();
            }
        }

        private bool IsCharged()
        {
            bool isCharged = false;
            int chargedCounter = 0;

            if (tempRailgunListIsCharged.Count == 0)
            {
                tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>(railgunBlockList);
            }

            foreach (IMySmallMissileLauncherReload railgun in tempRailgunListIsCharged.ToList())
            {
                int checkCounter = CheckCounter(chargedCounter, railgun, RailgunChargeStateEnum.CHARGED);
                if (checkCounter != chargedCounter)
                {
                    chargedCounter = checkCounter;
                    railgun.Enabled = false;
                    tempRailgunListIsCharged.Remove(railgun);
                }
            }

            if (tempRailgunListIsCharged.Count == 0)
            {
                isCharged = true;
            }

            return isCharged;
        }

        private bool IsAlmostCharged()
        {
            bool result = false;

            if (!IsBlockMissing(railgunReloadCheck))
            {
                return railgunReloadCheck.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
            } else
            {
                foreach (IMySmallMissileLauncherReload railgun in railgunBlockList)
                {
                    result = railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
                }
            }
            return result;
        }

        private static int CheckCounter(int chargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            string detailString = railgun.DetailedInfo;

            if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
            {
                chargeCounter++;
            }

            return chargeCounter;
        }

        private void ToggleBlocks<T>(bool toggle, List<T> blockList)
        {
            blockList.ForEach(rotor => ((IMyFunctionalBlock)rotor).Enabled = toggle);
        }

        private bool IsDoorOpen()
        {
            foreach (IMyDoor door in doorBlockList)
            {
                if (IsBlockMissing(door) && door.Status != DoorStatus.Open)
                {
                    GAUCommand = GAUCommandEnum.OPENINGDOOR;
                    return false;
                }
            }
            return true;
        }

        void GetBlocks()
        {
            errorString = "";
            warningString = "";
            startString = "";
            IMyBlockGroup GAUBlockGroup = GridTerminalSystem.GetBlockGroupWithName(GAU_GROUP_NAME);

            GAUBlockGroup?.GetBlocksOfType(railgunBlockList);
            GAUBlockGroup?.GetBlocksOfType(rotorBlockList);
            GAUBlockGroup?.GetBlocksOfType(doorBlockList);

            if (AreBlocksMissingFromGroupErrorMessage(railgunBlockList, "Railgun") || AreBlocksMissingFromGroupErrorMessage(rotorBlockList, "Rotor"))
            {
                return;
            }

            AreBlocksMissingFromGroupWarningMessage(doorBlockList, "Door");

            if (!(rotorBlockList.Count == 1 || rotorBlockList.Count == 2))
            {
                errorString = errorString + "\n" + $"Scrip only works with 1 or 2 rotors no more no less";
                areBlocksMissing = true;
                return;
            }

            Initialize(GridTerminalSystem);

            bool MainRotorExists = false;

            if (rotorBlockList.Count == 1)
            {
                ConfigureGAURotors(rotorBlockList.First(), TORQUE, RPM);
                MainRotorExists = true;
            }
            else
            {
                foreach (IMyMotorStator rotor in rotorBlockList)
                {
                    if (rotor.CustomName.Contains(MAIN_ROTOR_NAME))
                    {
                        ConfigureGAURotors(rotor, TORQUE, RPM);
                        MainRotorExists = true;
                    }
                    else
                    {
                        ConfigureGAURotors(rotor, TORQUE, -1 * RPM);
                    }
                }
            }

            if (!MainRotorExists)
            {
                errorString = errorString + "\n" + $"No rotor named {MAIN_ROTOR_NAME} found in group";
                areBlocksMissing = true;
                return;
            }

            CubeGrid = GAUCenterBlock.CubeGrid;
            VectorOffsets();

            bool isLG = railgunBlockList.First().CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large);
            RailgunChargeStateEnum.CHARGED = (isLG ? RailgunChargeStateEnumLG.CHARGED : RailgunChargeStateEnumSG.CHARGED);
            RailgunChargeStateEnum.ALMOST = (isLG ? RailgunChargeStateEnumLG.ALMOST : RailgunChargeStateEnumSG.ALMOST);
            shootDelay = (isLG ? shootTimeLG : shootTimeSG);
            rotationAngle = (isLG ? rotationAngleLG : rotationAngleSG);

            originPlaneAngleOffset = ShootDelayOffsetAngle();
            fireDelay = shootDelay * 60;
            ExhaustReset();
        }

        private void ConfigureGAURotors(IMyMotorStator motorStator, float torque, float targetVelocityRPM)
        {
            motorStator.Torque = torque;
            motorStator.BrakingTorque = torque;
            motorStator.TargetVelocityRPM = targetVelocityRPM;
            GAUCenterBlock = motorStator;
            circleCenter = GAUCenterBlock.Position;
        }

        private void VectorOffsets()
        {
            Vector3I behind = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Backward);
            Vector3I below = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Down);

            circleCenter2 = behind;
            thridPoint = below;
        }

        private void OpenDoors()
        {
            foreach (IMyDoor door in doorBlockList)
            {
                if (IsBlockMissing(door))
                {
                    continue;
                }
                door.OpenDoor();
            }
        }

        private void CloseDoors()
        {
            foreach (IMyDoor door in doorBlockList)
            {
                if (IsBlockMissing(door))
                {
                    continue;
                }
                door.CloseDoor();
            }
        }

        private bool IsBlockMissing<T>(T blockT)
        {
            IMyTerminalBlock block = (IMyTerminalBlock)blockT;
            return block == null || block.Closed || block.IsFunctional != true;
        }

        private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
        {
            railgun.Enabled = true;
            railgun.ShootOnce();
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

        public Vector3D GetWorldPosition(Vector3I localPosition)
        {
            // Convert the grid coordinates to a local position in 3D space
            Vector3D localCoords = (Vector3D)localPosition * CubeGrid.GridSize;

            // Transform the local position to world coordinates using the grid's WorldMatrix
            Vector3D worldCoords = Vector3D.Transform(localCoords, CubeGrid.WorldMatrix);

            return worldCoords;
        }

        public class Plane
        {
            public Vector3 Normal { get; }
            public float D { get; } // The plane equation is: Normal.X * x + Normal.Y * y + Normal.Z * z + D = 0

            public Plane(Vector3D point1, Vector3D point2, Vector3D point3)
            {
                // Compute the normal vector using the cross product
                Normal = Vector3D.Normalize(Vector3D.Cross(point2 - point1, point3 - point1));
                // Calculate D for the plane equation
                D = -Vector3.Dot(Normal, point1);
            }

            public float SignedDistance(Vector3D point)
            {
                // Calculate the signed distance of the point from the plane
                return Vector3.Dot(Normal, point) + D;
            }
        }

        public static class RotationHelper
        {
            // Rotate a vector around a given axis by an angle (in degrees)
            public static Vector3D RotateVector(Vector3D vector, Vector3D axis, float angleDegrees)
            {
                float angleRadians = ((float)Math.PI) * angleDegrees / 180f;
                Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3D.Normalize(axis), angleRadians);
                return Vector3D.Transform(vector, rotation);
            }
        }

        public List<Plane> getRotatedPlanes()
        {
            Vector3D center1 = GetWorldPosition(circleCenter);
            Vector3D center2 = GetWorldPosition(circleCenter2);
            Vector3D point = GetWorldPosition(thridPoint);

            // Create the axis of rotation
            Vector3D rotationAxis = Vector3D.Normalize(center2 - center1);

            // Rotate the third point around the axis by X degrees to find the rotated plane
            Vector3D rotatedPoint = RotationHelper.RotateVector(point - center1, rotationAxis, rotationAngle + targetAngle + originPlaneAngleOffset) + center1;
            Vector3D rotatedPoint2 = RotationHelper.RotateVector(point - center1, rotationAxis, -rotationAngle + targetAngle + originPlaneAngleOffset) + center1;

            return new List<Plane>
            {
                new Plane(center1, center2, rotatedPoint),
                new Plane(center1, center2, rotatedPoint2)
            };
        }

        public bool IsPointBetweenAngles(List<Plane> rotatedPlanes, Vector3D railgunPosition)
        {
            // Check if the fourth point is between the two planes
            float distanceToRotated = rotatedPlanes[0].SignedDistance(railgunPosition);
            float distanceToRotated2 = rotatedPlanes[1].SignedDistance(railgunPosition);

            return distanceToRotated2 > 0 && distanceToRotated < 0;
        }

        public float ShootDelayOffsetAngle()
        {
            float anglesPerSecond = 360 * RPM / 60;
            return -1 * anglesPerSecond * shootDelay;
        }
        public void Initialize(IMyGridTerminalSystem gridTerminalSystem)
        {
            this.gridTerminalSystem = gridTerminalSystem;
            reference = gridTerminalSystem.GetBlockWithName(referenceBlockName);
            if (reference == null)
            {
                Echo("ERROR: Reference block not found!");
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

        public void ExhaustReset()
        {
            state = 0;
            tickCounter = 0;
            exhaustEffectTicks = (stepDelayTicks + 1) * exhaustLists.Count;
        }

        public void ExhaustEffect()
        {
            // === TURNING ON ===
            if (state < exhaustLists.Count)
            {
                if (tickCounter >= stepDelayTicks)
                {
                    exhaustLists[state].ForEach(exhaust => exhaust.Enabled = true);
                    state++;
                    tickCounter = 0;
                }
                else
                {
                    tickCounter++;
                }
            }
        }

        public void ExhaustOff()
        {
            // === TURNING OFF ===
            exhaustLists.ForEach(exhaustList => exhaustList.ForEach(exhaust => exhaust.Enabled = false));
        }
    }
}