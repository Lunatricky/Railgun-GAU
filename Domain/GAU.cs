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
        #region Static
        public static string GAUGroupTag { get; private set; } = "GAU";
        public static string GAUCustomDataProviderTag { get; private set;  } = "GAU Data Provider";
        #endregion Static
        public StringBuilder Info
        {
            get
            {
                StringBuilder info = new StringBuilder();
                info.Append(_statusBuilder.ToString());
                if (Errors.Length > 0)
                {
                    info.Append($"-----ERRORS-----{_errorBuilder}\n------------------");
                }
                if (Warnings.Length > 0)
                {
                    info.Append($"-----WARNINGS-----{_warningBuilder}\n------------------");
                }
                return info;
            }
        }
        public StringBuilder Errors
        {
            get
            {
                return _errorBuilder;
            }
        }
        public StringBuilder Warnings
        {
            get
            {
                return _warningBuilder;
            }
        }
        public StringBuilder Status
        {
            get
            {
                return _statusBuilder;
            }
        }
        public List<IMySmallMissileLauncherReload> RailgunBlockList { get; set; }
        public List<IMyDoor> DoorBlockList { get; set; }
        public List<IMyMotorStator> RotorBlockList { get; set; }

        public float ShootDelayOffsetAngle
        {
            get
            {
                float anglesPerSecond = 360 * _rpm / 60;
                return -1 * anglesPerSecond * _shootDelay;
            }
        }
        public GAUActionEnum GAUState
        {
            get { return GAUState; }
            set { GAUState = value; }
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
                    tempRailgunListIsCharged =  new List<IMySmallMissileLauncherReload>(RailgunBlockList);
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
                        GAUState = GAUActionEnum.OPENINGDOOR;
                        return false;
                    }
                }
                return true;
            }
        }

        public bool HasError
        {
            get
            {
                return AreBlocksMissing || !_hasReferenceBlock;
            }
        }

        public bool HasWarning { get; private set; } = false;

        public bool AllowsRuntimeModification { get;} = false;

        public bool IsCreated { get; private set; }

        public IMyMotorStator GAUCenterBlock { get; private set; }

        private GAUActionEnum GAUCommand
        {
            get
            {
                return _gauTempCommand;
            }
            set
            {
                _gauTempCommand = value;
            }
        }

        private string IniSectionGAU
        {
            get
            {
                return $"{INI_SECTION_GAU_GENERAL} | {_id}";
            }
        }



        #endregion Properties

        #region Fields

        #region Static
        private static MyIni _iniGeneral = new MyIni();     
        #endregion Static

        private MyIni _ini = new MyIni();
        private IMyTerminalBlock _customDataProvider;
        private IMyGridTerminalSystem _gridTerminalSystem;
        private MyGridProgram _gridProgram;

        private GAUActionEnum _gauTempCommand = GAUActionEnum.NULL;

        // Outputs
        private StringBuilder _errorBuilder = new StringBuilder();
        private StringBuilder _warningBuilder = new StringBuilder();
        private StringBuilder _statusBuilder = new StringBuilder();
        private string _startString = "";

        private float _shootDelay;  // Shooting delay in seconds
        private float _targetAngle = 0; // Target angle in degrees

        private float _rotationAngle;
        private bool _areBlocksMissing = false; // Keep as field in case property needs more complexity?
        private bool _hasReferenceBlock = false;
        private bool _hasCompletedfirstRun = true;
        private float _originPlaneAngleOffset = 0;

        private Vector3I _circleCenter = new Vector3I();
        private Vector3I _circleCenter2 = new Vector3I();
        private Vector3I _thridPoint = new Vector3I();

        private IMySmallMissileLauncherReload railgunReloadCheck;

        private List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListOff = new List<IMySmallMissileLauncherReload>();


        private double _groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair

        private IMyTerminalBlock _referenceBlock;
        private readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        private int _state = 0;          // Which step we're on
        private int _tickCounter = 0;    // Delay counter

        private float _exhaustEffectDelay;
        private float _fireDelay;

        private string _groupName = "GAU";
        private string _id;

        // Ini stuff        
        private float _rpm = -30;
        private string _rotorName = "Gau Rotor";
        private string _exhaustTag = "Exhaust Cap";
        private int _stepDelayTicks = 5;
        private string _referenceBlockName = "Main Cockpit";  // Change this to your reference block name
        #endregion Fields

        #region Constants

        #region Ini

        #region Static
        // Keys
        private const string INI_KEY_GENERAL_GAU_GROUP_TAG = "GAU Group Tag";

        // Sections
        private const string INI_SECTION_GENERAL = "GAU Script General Settings";
        #endregion Static

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
        public GAU(IMyTerminalBlock customDataProvider, IMyGridTerminalSystem gridTerminalSystem, string id = null, MyGridProgram gridProgram = null)
        {
            _customDataProvider = customDataProvider;
            _gridTerminalSystem = gridTerminalSystem;
            _groupName = id;
            _id = id;

            if (gridProgram != null)
            {
                _gridProgram = gridProgram;
                AllowsRuntimeModification = true;
            }
        
            ParseIni();
            GetBlocks();
            IsCreated = true;
        }
        #endregion Constructors

        #region Init
        public void GetBlocks()
        {
            IMyBlockGroup GAUBlockGroup = _gridTerminalSystem.GetBlockGroupWithName(_groupName);

            GAUBlockGroup?.GetBlocksOfType(RailgunBlockList);
            GAUBlockGroup?.GetBlocksOfType(DoorBlockList);
            GAUBlockGroup?.GetBlocksOfType(RotorBlockList);

            if (AreBlocksMissingFromGroupErrorMessage(RailgunBlockList, "Railgun") || AreBlocksMissingFromGroupErrorMessage(RotorBlockList, "Rotor"))
            {
                _areBlocksMissing = true;
                return;
            }

            AreBlocksMissingFromGroupWarningMessage(DoorBlockList, "Door");

            if (!(RotorBlockList.Count == 1 || RotorBlockList.Count == 2))
            {
                _errorBuilder.Append("\n" + $"Scrip only works with 1 or 2 rotors no more no less");
                _areBlocksMissing = true;
                return;
            }

            Initialize(_gridTerminalSystem);

            if (!TrySetRotorOrRotors(TORQUENORMAL))
            {
                _errorBuilder.Append("\n" + $"No rotor named {_rotorName} found in group");
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
            _hasReferenceBlock = _referenceBlock != null;
            if (!_hasReferenceBlock)
            {
                _errorBuilder.Append("\n" + $"No block named {_referenceBlockName} found in group");
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
                _errorBuilder.Append("\n" + $"No {blockType} block found in group");
                return true;
            }
            else
            {
                _startString = _startString + "\n" + $"{blockType} count: {list.Count}";
                return false;
            }
        }

        // This is a weird method, maybe use an out variable for the message instead?
        private bool AreBlocksMissingFromGroupWarningMessage<T>(List<T> list, string blockType)
        {
            if (list?.Count == 0)
            {
                _warningBuilder.Append("\n" + $"No {blockType} block found in group");
                return true;
            }
            else
            {
                _startString = _startString + "\n" + $"{blockType} count: {list.Count}";
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

        #region Gau Factory
        public static List<GAU> AcquireGAUs(IMyTerminalBlock customDataProvider, IMyGridTerminalSystem gridTerminalSystem, MyGridProgram gridProgram = null)
        {
            List<GAU> gauList = new List<GAU>();
            if (customDataProvider == null) return gauList;

            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            gridTerminalSystem.GetBlockGroups(groups, group => group.Name.Contains(GAUGroupTag));
            foreach (IMyBlockGroup group in groups)
            {
                //List<IMyTerminalBlock> customDataProviders = new List<IMyTerminalBlock>();
                //group.GetBlocks(customDataProviders, block => block.CustomName.Contains(GAU_CUSTOM_DATA_PROVIDER_TAG));
                try
                {
                    GAU gau;
                    gau = new GAU(customDataProvider, gridTerminalSystem, group.Name, gridProgram); 
                    if (gau.IsCreated)
                    {
                        gauList.Add(gau);
                    }
                }
                catch
                {
                    //TODO something something
                }
            }
            return gauList;
        }
        #endregion Gau Factory

        #region Gau Primary Methods
        #region Static
        public static void RunWithTag(string argument, List<GAU> gauList, string groupNameTag)
        {
            foreach (GAU gau in gauList)
            {
                if (gau._groupName.Contains(groupNameTag))
                {
                    gau.Run(argument);
                }
            }
        }
        private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
        {
            railgun.Enabled = true;
            railgun.ShootOnce();
        }      

        private static bool TryParseGauCommand(string input, out GAUActionEnum command)
        {
            command = GAUActionEnum.NULL;
            try
            {
                command = (GAUActionEnum)Enum.Parse(typeof(GAUActionEnum), input, true);
                return true;
            }
            catch
            {
                command = GAUActionEnum.ON;
            }
            return false;
            /*
            if (input != null && input.Length != 0 && input != "")
            {
                try
                {
                    command = (GAUCommandEnum)Enum.Parse(typeof(GAUCommandEnum), input, true);
                }
                catch
                {
                    command = GAUCommandEnum.ON; // Why is this like this
                }
            }*/
        }
        #endregion Static

        #region Non-Static
        #region Run
        public void Run(string argument = "")
        {
            _statusBuilder.Clear();
            _statusBuilder.Append($"Cycle: {GAUState}");
            _statusBuilder.Append($"{_startString}");

            if (!TryParseGauCommand(argument, out _gauTempCommand))
            {
                GAUState = GAUActionEnum.RESET;
            }

            /*
            if (HasError)
            {
                _statusBuilder.Append($"{_errorBuilder.ToString()}");
                return;
            }*/

            if (HasError) return;

            if (GAUState == GAUActionEnum.OFF && GAUCommand != GAUActionEnum.ON)
            {
                //Echo(InstructionCount());
                return;
            }

            if (GAUCommand != GAUActionEnum.NULL &&
                GAUState != GAUActionEnum.CHARGING &&
                GAUState != GAUActionEnum.ALMOSTCHARGED &&
                GAUState != GAUActionEnum.OPENINGDOOR &&
                GAUState != GAUActionEnum.CLOSINGDOOR
                )
            {
                GAUState = GAUCommand;
                GAUCommand = GAUActionEnum.NULL;
            }
            else if (GAUCommand == GAUActionEnum.FIRE ||
                     GAUCommand == GAUActionEnum.EXHAUST ||
                     GAUCommand == GAUActionEnum.EXHAUSTEFFECT ||
                     GAUCommand == GAUActionEnum.EXHAUSTFIRE)
            {
                GAUCommand = GAUActionEnum.NULL;
            }


            if (GAUState == GAUActionEnum.FIRE || GAUState == GAUActionEnum.EXHAUST ||
                GAUState == GAUActionEnum.EXHAUSTEFFECT || GAUState == GAUActionEnum.EXHAUSTFIRE ||
                GAUState == GAUActionEnum.CHARGING)
            {
                if (AllowsRuntimeModification) _gridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            {
                if (AllowsRuntimeModification) _gridProgram. Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            /*
            if (_warningBuilder.Length > 0)
            {

                Echo($"-----WARNINGS-----{gau.warningString}\n------------------");
            }*/

            switch (GAUState)
            {
                case GAUActionEnum.ON:
                case GAUActionEnum.RESET:
                    GetBlocks();
                    ToggleBlocks(true, RotorBlockList);
                    ToggleBlocks(false, RailgunBlockList);
                    OpenDoors();
                    if (IsCharged)
                    {
                        GAUState = GAUActionEnum.READY;
                    }
                    else
                    {
                        GAUState = GAUActionEnum.CHARGE;
                    }
                    break;

                case GAUActionEnum.OFF:
                    if (IsCharged)
                        Off();
                    break;

                case GAUActionEnum.EXHAUST:
                    ExhaustReset();
                    TrySetRotorOrRotors(TORQUE);
                    if (_fireDelay > _exhaustEffectDelay)
                    {
                        GAUState = GAUActionEnum.EXHAUSTFIRE;
                    }
                    else
                    {
                        GAUState = GAUActionEnum.EXHAUSTEFFECT;
                    }
                    break;

                case GAUActionEnum.EXHAUSTEFFECT:
                    TriggerExhaustEffect();
                    if (_fireDelay > _exhaustEffectDelay)
                    {
                        if (IsDoorOpen)
                        {
                            RailgunShootSalvo();
                        }
                    }
                    else
                    {
                        _exhaustEffectDelay--;
                    }
                    break;

                case GAUActionEnum.EXHAUSTFIRE:
                    if (IsDoorOpen)
                    {
                        RailgunShootSalvo();
                    }
                    if (_fireDelay < _exhaustEffectDelay)
                    {
                        TriggerExhaustEffect();
                    }
                    else
                    {
                        _fireDelay--;
                    }
                    break;

                case GAUActionEnum.FIRE:
                    if (IsDoorOpen && TrySetRotorOrRotors(TORQUE))
                    {
                        RailgunShootSalvo();
                    }
                    break;

                case GAUActionEnum.CLOSINGDOOR:
                    if (!IsDoorOpen)
                    {
                        CloseDoors();
                    }
                    else
                    {
                        GAUState = GAUActionEnum.READY;
                    }
                    break;

                case GAUActionEnum.OPENINGDOOR:
                    if (!IsDoorOpen)
                    {
                        OpenDoors();
                    }
                    else
                    {
                        GAUState = GAUActionEnum.READY;
                    }
                    break;

                case GAUActionEnum.CHARGE:
                    TrySetRotorOrRotors(TORQUENORMAL);
                    CloseDoors();
                    ToggleBlocks(true, RailgunBlockList);
                    ToggleBlocks(false, RotorBlockList);
                    GAUState = GAUActionEnum.CHARGING;
                    break;

                case GAUActionEnum.CHARGING:
                    if (IsAlmostCharged)
                    {
                        railgunReloadCheck = null;
                        OpenDoors();
                        ToggleBlocks(true, RotorBlockList);
                        GAUState = GAUActionEnum.ALMOSTCHARGED;
                    }
                    break;
                case GAUActionEnum.ALMOSTCHARGED:
                    if (IsCharged)
                    {
                        OpenDoors();
                        ToggleBlocks(true, RotorBlockList);
                        GAUState = GAUActionEnum.READY;
                    }
                    break;

                default:
                    if (!_hasCompletedfirstRun && !IsCharged)
                    {
                        GAUState = GAUActionEnum.CHARGE;
                    }
                    else
                    {
                        ToggleBlocks(false, RailgunBlockList);
                    }
                    break;
            }

            if (_areBlocksMissing)
            {
                GAUState = GAUActionEnum.RESET;
            }

            _hasCompletedfirstRun = true;

           // Echo(InstructionCount());
        }
        #endregion Run
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
                GAUState = GAUActionEnum.CHARGE;
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

        public bool IsPointBetweenAngles(List<Plane> rotatedPlanes, Vector3D railgunPosition)
        {
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

        #region Static
        public static void ParseIni(IMyTerminalBlock customDataProvider)
        {
            _iniGeneral.Clear();
            string customData = customDataProvider.CustomData;
            bool parsed = _iniGeneral.TryParse(customData);

            List<string> sections = new List<string>();
            _iniGeneral.GetSections(sections);

            foreach (string sectionName in sections)
            {
                if (sectionName.Contains(INI_SECTION_GENERAL))
                {
                    GAUGroupTag = GAU._iniGeneral.Get(sectionName, INI_KEY_GENERAL_GAU_GROUP_TAG).ToString(GAUGroupTag);
                    continue;
                }
            }

            _iniGeneral.Set(INI_SECTION_GENERAL, INI_KEY_GENERAL_GAU_GROUP_TAG, GAUGroupTag);       

            string output = _iniGeneral.ToString();
            if (!string.Equals(output, customDataProvider.CustomData))
            {
                customDataProvider.CustomData = output;
            }
        }
        #endregion Static

        private void ParseIni()
        {
            _ini.Clear();
            string customData = _customDataProvider.CustomData;
            bool parsed = _ini.TryParse(customData);

            List<string> sections = new List<string>();
            _ini.GetSections(sections);

            foreach (string sectionName in sections)
            {
                if (sectionName.Contains(IniSectionGAU))
                {
                    //_groupName = _ini.Get(sectionName, INI_KEY_GAU_GROUP_NAME).ToString(_groupName);
                    _rpm = (float)_ini.Get(sectionName, INI_KEY_GAU_RPM).ToDouble(_rpm);
                    _rotorName = _ini.Get(sectionName, INI_KEY_GAU_MAIN_ROTOR_NAME).ToString(_rotorName);
                    _exhaustTag = _ini.Get(sectionName, INI_KEY_GAU_EXHAUST_TAG).ToString(_exhaustTag);
                    _stepDelayTicks = _ini.Get(sectionName, INI_KEY_GAU_STEP_DELAY_TICKS).ToInt32(_stepDelayTicks);
                    _targetAngle = (float) _ini.Get(sectionName, INI_KEY_GAU_TARGET_ANGLE).ToDouble(_targetAngle);
                    _referenceBlockName = _ini.Get(sectionName, INI_KEY_GAU_REFERENCE_BLOCK_NAME).ToString(_referenceBlockName);
                    continue;
                }
            }

            //_ini.Set(INI_SECTION_GAU_GENERAL, INI_KEY_GAU_GROUP_NAME, _groupName);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_RPM, _rpm);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_MAIN_ROTOR_NAME, _rotorName);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_EXHAUST_TAG, _exhaustTag);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_STEP_DELAY_TICKS, _stepDelayTicks);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_TARGET_ANGLE, _targetAngle);
            _ini.Set(IniSectionGAU, INI_KEY_GAU_REFERENCE_BLOCK_NAME, _referenceBlockName);

            string output = _ini.ToString();
            if (!string.Equals(output, _customDataProvider.CustomData))
            {
                _customDataProvider.CustomData = output;
            }
        }
        #endregion ini
    }
}
