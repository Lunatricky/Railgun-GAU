using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using IngameScript.Utils;
namespace IngameScript.Domain
{
    partial class GAU
    {
        #region Properties
        public List<IMySmallMissileLauncherReload> RailgunBlockList { get; }
        public List<IMyDoor> DoorBlockList { get; }
        public List<IMyMotorStator> RotorBlockList { get; }

        public float ShootDelayOffsetAngle
        {
            get
            {
                float anglesPerSecond = 360 * _rpm / 60;
                return -1 * anglesPerSecond * _shootDelay;
            }
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
        public bool AreBlocksMissing
        {
            get
            {
                return _areBlocksMissing;
            }
        }

        private bool IsCharged
        {
            get
            {
                bool isCharged = false;
                int chargedCounter = 0;

                if (tempRailgunListIsCharged.Count == 0)
                {
                    tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>(RailgunBlockList);
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
        }

        private bool IsAlmostCharged
        {
            get
            {
                bool result = false;

                if (!IsBlockMissing(railgunReloadCheck))
                {
                    return railgunReloadCheck.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
                }
                else
                {
                    foreach (IMySmallMissileLauncherReload railgun in RailgunBlockList)
                    {
                        result = railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
                    }
                }
                return result;
            }       
        }

        private bool IsDoorOpen
        {
            get
            {
                foreach (IMyDoor door in DoorBlockList)
                {
                    if (IsBlockMissing(door) && door.Status != DoorStatus.Open)
                    {
                        GAUCommand = GAUCommandEnum.OPENINGDOOR;
                        return false;
                    }
                }
                return true;
            }         
        }

        public IMyMotorStator GAUCenterBlock { get; private set; }

        private GAUCommandEnum GAUCommandEnumTemp { get; set; } // Never used?

        #endregion Properties

        #region Fields
        private MyIni _ini = new MyIni();
        private IMyTerminalBlock _customDataProvider;
        private IMyGridTerminalSystem _gridTerminalSystem;
        public string errorString = ""; // Should make this a string builder, much faster
        public string warningString = ""; // Should make this a string builder, much faster
        public string startString = "";

        private float _shootDelay;  // Shooting delay in seconds
        private float _targetAngle = 0; // Target angle in degrees

        private float _rotationAngle;
        private bool _areBlocksMissing = false; // Keep as field in case property needs more complexity?
        private float _originPlaneAngleOffset = 0;

        private Vector3I _circleCenter = new Vector3I();
        private Vector3I _circleCenter2 = new Vector3I();
        private Vector3I _thridPoint = new Vector3I();

        private IMySmallMissileLauncherReload railgunReloadCheck;  // Never used?

        private List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();  // Never used?
        private List<IMySmallMissileLauncherReload> tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>();  // Never used?
        private List<IMySmallMissileLauncherReload> tempRailgunListOff = new List<IMySmallMissileLauncherReload>();  // Never used?


        private double _groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair

        private IMyTerminalBlock _referenceBlock;
        private readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        private int _state = 0;          // Which step we're on
        private int _tickCounter = 0;    // Delay counter

        private float _exhaustEffectDelay;
        private float _fireDelay;

        // Ini stuff
        private string _groupName = "GAU";
        private float _rpm = -30;
        private string _rotorName = "Gau Rotor";
        private string _exhaustTag = "Exhaust Cap";
        private int _stepDelayTicks = 5;
        private string _referenceBlockName = "Main Cockpit";  // Change this to your reference block name
        #endregion Fields

        #region Constants
        #region Ini
        // Keys
        private const string INI_KEY_GAU_GROUP_NAME = "Group Name";
        private const string INI_KEY_GAU_RPM = "RPM";
        private const string INI_KEY_GAU_MAIN_ROTOR_NAME = "Rotor Name";
        private const string INI_KEY_GAU_EXHAUST_TAG = "Exhaust Tag";
        private const string INI_KEY_GAU_STEP_DELAY_TICKS = "Step Delay Ticks";
        private const string INI_KEY_GAU_TARGET_ANGLE = "Target Angle";
        private const string INI_KEY_GAU_REFERENCE_BLOCK_NAME = "Main Cockpit";

        // Sections
        private const string INI_SECTION_GAU_GENERAL = "GAU - Settings";
        #endregion Ini

        private const float TORQUE = 100000000000f;
        private const float TORQUENORMAL = 33600000;
        #endregion Constants

        #region Constructors
        public GAU(IMyTerminalBlock customDataProvider, IMyGridTerminalSystem gridTerminalSystem)
        {
            _customDataProvider = customDataProvider;
            _gridTerminalSystem = gridTerminalSystem;

            ParseIni();

            GetBlocks();
        }
        #endregion Constructors

        #region Init
        public void GetBlocks()
        {
            errorString = "";
            warningString = "";
            startString = "";
            IMyBlockGroup GAUBlockGroup = _gridTerminalSystem.GetBlockGroupWithName(_groupName);

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
                _areBlocksMissing = true;
                return;
            }

            Initialize(_gridTerminalSystem);

            if (!TrySetRotorOrRotors(TORQUENORMAL))
            {
                errorString = errorString + "\n" + $"No rotor named {_rotorName} found in group";
                _areBlocksMissing = true;
                return;
            }

            SetVectorOffsets();

            bool isLG = RailgunBlockList.First().CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large);
            RailgunChargeStateEnum.CHARGED = (isLG ? RailgunChargeStateEnumLG.CHARGED : RailgunChargeStateEnumSG.CHARGED);
            RailgunChargeStateEnum.ALMOST = (isLG ? RailgunChargeStateEnumLG.ALMOST : RailgunChargeStateEnumSG.ALMOST);
            _shootDelay = (isLG ? RailgunInfo.LG : RailgunInfo.SG);
            _rotationAngle = (isLG ? RailgunInfo.rotationAngleLG : RailgunInfo.rotationAngleSG);

            _originPlaneAngleOffset = ShootDelayOffsetAngle;
            _fireDelay = _shootDelay * 60;
            ExhaustReset();
        }

        public void Initialize(IMyGridTerminalSystem gridTerminalSystem)
        {
            _referenceBlock = gridTerminalSystem.GetBlockWithName(_referenceBlockName);
            if (_referenceBlock == null)
            {
                errorString = errorString + "\n" + $"No block named {_referenceBlockName} found in group";
                return;
            }
            // Collect all exhaust caps
            List<IMyFunctionalBlock> allExhausts = new List<IMyFunctionalBlock>();
            gridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock>(allExhausts, b => b.CustomName.Contains(_exhaustTag));

            // Build a list of exhausts + distances
            List<IMyFunctionalBlock> sortedExhausts = new List<IMyFunctionalBlock>(allExhausts);
            sortedExhausts.Sort(delegate (IMyFunctionalBlock a, IMyFunctionalBlock b)
            {
                double da = Vector3D.Distance(_referenceBlock.GetPosition(), a.GetPosition());
                double db = Vector3D.Distance(_referenceBlock.GetPosition(), b.GetPosition());
                return da.CompareTo(db);
            });

            // Group exhausts by approximate distance
            exhaustLists.Clear();
            foreach (IMyFunctionalBlock sortedExhaust in sortedExhausts)
            {
                double dist = Vector3D.Distance(_referenceBlock.GetPosition(), sortedExhaust.GetPosition());
                bool placed = false;

                foreach (List<IMyFunctionalBlock> exhaustList in exhaustLists)
                {
                    double groupDist = Vector3D.Distance(_referenceBlock.GetPosition(), exhaustList[0].GetPosition());
                    if (Math.Abs(groupDist - dist) < _groupTolerance)
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

            _state = 0;
            _tickCounter = 0;
        }

        private void ConfigureGAURotors(IMyMotorStator motorStator, float torque, float targetVelocityRPM)
        {
            motorStator.Torque = torque;
            motorStator.BrakingTorque = torque;
            motorStator.TargetVelocityRPM = targetVelocityRPM;
            GAUCenterBlock = motorStator;
            _circleCenter = GAUCenterBlock.Position;
        }

        public bool TrySetRotorOrRotors(float torque)
        {
            bool MainRotorExists = false;

            if (RotorBlockList.Count == 1)
            {
                ConfigureGAURotors(RotorBlockList.First(), TORQUE, _rpm);
                MainRotorExists = true;
            }
            else
            {
                foreach (IMyMotorStator rotor in RotorBlockList)
                {
                    if (rotor.CustomName.Contains(_rotorName))
                    {
                        ConfigureGAURotors(rotor, torque, _rpm);
                        MainRotorExists = true;
                    }
                    else
                    {
                        ConfigureGAURotors(rotor, torque, -1 * _rpm);
                    }
                }
            }
            return MainRotorExists;
        }

        // This is a weird method, maybe use an out variable for the message instead?
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

        // This is a weird method, maybe use an out variable for the message instead?
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

        private void SetVectorOffsets()
        {
            Vector3I behind = _circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Backward);
            Vector3I below = _circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Down);

            _circleCenter2 = behind;
            _thridPoint = below;
        }
        #endregion Init

        #region Gau Primary Methods
        #region Static
        private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
        {
            railgun.Enabled = true;
            railgun.ShootOnce();
        }
        #endregion Static

        #region Non-Static
        private void Off()
        {
            CloseDoors();

            if (tempRailgunListOff.Count == 0)
            {
                tempRailgunListOff = new List<IMySmallMissileLauncherReload>(RailgunBlockList);
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
                ToggleBlocks(false, RailgunBlockList);
                ToggleBlocks(false, RotorBlockList);
            }
        }

        private void ToggleBlocks<T>(bool toggle, List<T> blockList)
        {
            blockList.ForEach(rotor => ((IMyFunctionalBlock)rotor).Enabled = toggle);
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
    

            
        private void OpenDoors()
        {
            foreach (IMyDoor door in DoorBlockList)
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
            foreach (IMyDoor door in DoorBlockList)
            {
                if (IsBlockMissing(door))
                {
                    continue;
                }
                door.CloseDoor();
            }
        }


        #region Bools
        private bool IsBlockMissing<T>(T blockT)
        {
            IMyTerminalBlock block = (IMyTerminalBlock)blockT;
            return block == null || block.Closed || block.IsFunctional != true;
        }

        // I don't know what this does luna, what is point 4. Because I do not know what this is I am not going to convert it into a property because I do not know if it represents
        // a state of the GAU object or of something else
        public bool IsPointBetweenAngles(List<Plane> rotatedPlanes, Vector3D railgunPosition)
        {
            // Check if the fourth point is between the two planes
            float distanceToRotated = rotatedPlanes[0].SignedDistance(railgunPosition);
            float distanceToRotated2 = rotatedPlanes[1].SignedDistance(railgunPosition);

            return distanceToRotated2 > 0 && distanceToRotated < 0;
        }
        #endregion Bools

        private static int CheckCounter(int chargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            string detailString = railgun.DetailedInfo;

            if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
            {
                chargeCounter++;
            }

            return chargeCounter;
        }

        public Vector3D GetWorldPosition(Vector3I localPosition)
        {
            // Convert the grid coordinates to a local position in 3D space
            Vector3D localCoords = (Vector3D)localPosition * GAUCenterBlock.CubeGrid.GridSize;

            // Transform the local position to world coordinates using the grid's WorldMatrix
            Vector3D worldCoords = Vector3D.Transform(localCoords, GAUCenterBlock.CubeGrid.WorldMatrix);

            return worldCoords;
        }
        #endregion Gau Primary Methods

        #region Exhaust Methods
        public void TriggerExhaustEffect()
        {
            // === TURNING ON ===
            if (_state < exhaustLists.Count)
            {
                if (_tickCounter >= _stepDelayTicks)
                {
                    exhaustLists[_state].ForEach(exhaust => exhaust.Enabled = true);
                    _state++;
                    _tickCounter = 0;
                }
                else
                {
                    _tickCounter++;
                }
            }
        }
        public void ExhaustOff()
        {
            // === TURNING OFF ===
            exhaustLists.ForEach(exhaustList => exhaustList.ForEach(exhaust => exhaust.Enabled = false));
        }
        public void ExhaustReset()
        {
            _state = 0;
            _tickCounter = 0;
            _exhaustEffectDelay = (_stepDelayTicks + 1) * exhaustLists.Count;
        }
        #endregion Exhaust Methods

        #endregion Gau Primary Methods

        #region Plane
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
            Vector3D center1 = GetWorldPosition(_circleCenter);
            Vector3D center2 = GetWorldPosition(_circleCenter2);
            Vector3D point = GetWorldPosition(_thridPoint);

            // Create the axis of rotation
            Vector3D rotationAxis = Vector3D.Normalize(center2 - center1);

            // Rotate the third point around the axis by X degrees to find the rotated plane
            Vector3D rotatedPoint = RotationHelper.RotateVector(point - center1, rotationAxis, _rotationAngle + _targetAngle + _originPlaneAngleOffset) + center1;
            Vector3D rotatedPoint2 = RotationHelper.RotateVector(point - center1, rotationAxis, -_rotationAngle + _targetAngle + _originPlaneAngleOffset) + center1;

            return new List<Plane>
            {
                new Plane(center1, center2, rotatedPoint),
                new Plane(center1, center2, rotatedPoint2)
            };
        }
        #endregion Plane

        #region ini
        private void ParseIni()
        {
            _ini.Clear();
            string customData = _customDataProvider.CustomData;
            bool parsed = _ini.TryParse(customData);

            if (!parsed && !string.IsNullOrWhiteSpace(_customDataProvider.CustomData.Trim()))
            {
                _ini.EndContent = _customDataProvider.CustomData;
            }

            List<string> sections = new List<string>();
            _ini.GetSections(sections);

            foreach (string sectionName in sections)
            {
                if (sectionName.Contains(INI_SECTION_GAU_GENERAL))
                {
                    _groupName = _ini.Get(sectionName, INI_KEY_GAU_GROUP_NAME).ToString(_groupName);
                    _rpm = (float)_ini.Get(sectionName, INI_KEY_GAU_RPM).ToDouble(_rpm);
                    _rotorName = _ini.Get(sectionName, INI_KEY_GAU_MAIN_ROTOR_NAME).ToString(_rotorName);
                    _exhaustTag = _ini.Get(sectionName, INI_KEY_GAU_EXHAUST_TAG).ToString(_exhaustTag);
                    _stepDelayTicks = _ini.Get(sectionName, INI_KEY_GAU_STEP_DELAY_TICKS).ToInt32(_stepDelayTicks);
                    _targetAngle = (float) _ini.Get(sectionName, INI_KEY_GAU_TARGET_ANGLE).ToDouble(_targetAngle);
                    _referenceBlockName = _ini.Get(sectionName, INI_KEY_GAU_REFERENCE_BLOCK_NAME).ToString(_referenceBlockName);
                    continue;
                }
            }

            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_GROUP_NAME, _groupName);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_RPM, _rpm);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_MAIN_ROTOR_NAME, _rotorName);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_EXHAUST_TAG, _exhaustTag);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_STEP_DELAY_TICKS, _stepDelayTicks);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_TARGET_ANGLE, _targetAngle);
            _ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_REFERENCE_BLOCK_NAME, _referenceBlockName);

            string output = _ini.ToString();
            if (!string.Equals(output, _customDataProvider.CustomData))
            {
                _customDataProvider.CustomData = output;
            }
        }
        #endregion ini
    }
}
