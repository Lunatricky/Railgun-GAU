using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        //Ini
        Dictionary<string, string> __snapshot = new Dictionary<string, string>();
        bool iniAnyChanged = false;
        int tickCount;

        public static string GAUGroupTag { get; private set; } = "GAU";
        public static string GAUCustomDataProviderTag { get; private set; } = "GAU Data Provider";

        public StringBuilder Info
        {
            get
            {
                StringBuilder info = new StringBuilder();
                info.Append(_statusBuilder.ToString());
                if (Errors.Length > 0)
                {
                    info.Append($"\n-----ERRORS-----\n{_errorBuilder}\n------------------\n");
                }
                if (Warnings.Length > 0)
                {
                    info.Append($"\n-----WARNINGS-----\n{_warningBuilder}\n------------------\n");
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
        IMyBlockGroup GAUBlockGroup;
        public List<IMySmallMissileLauncherReload> RailgunBlockList { get; set; } = new List<IMySmallMissileLauncherReload>();
        public List<IMyDoor> DoorBlockList { get; set; } = new List<IMyDoor>();
        public List<IMyMotorStator> RotorBlockList { get; set; } = new List<IMyMotorStator>();
        public List<IMyTextSurface> LcdBlockList { get; set; } = new List<IMyTextSurface>();

        public float ShootDelayOffsetAngle
        {
            get
            {
                float anglesPerSecond = 360 * _rpm / 60;
                return -1 * anglesPerSecond * _shootDelay;
            }
        }
        public GAUActionEnum GAUState { get; set; }
        public float RotationAngle { get; set; }

        public bool HasWarning { get; private set; } = false;
        public bool IsCreated { get; private set; }

        public IMyMotorStator GAUCenterBlock { get; private set; }



        private static MyIni s_iniGeneral = new MyIni();
        private static MyGridProgram s_gridProgram;
        private static List<GauGeo> s_createdGAUList = new List<GauGeo>();

        private IMyTerminalBlock _customDataProvider;
        private IMyGridTerminalSystem _gridTerminalSystem;

        private GAUActionEnum _gauTempCommand = GAUActionEnum.NULL;

        // Outputs
        private StringBuilder _errorBuilder = new StringBuilder();
        private StringBuilder _warningBuilder = new StringBuilder();
        private StringBuilder _statusBuilder = new StringBuilder();

        private float _shootTimeout;
        private float _shootDelay;  // Shooting delay in seconds
        private float _targetAngle = 0; // Target angle in degrees

        private string _railGunChargeStateDetailedInfoString = "";
        private bool isLG;
        private bool _hasCompletedfirstRun = false;
        private float _originPlaneAngleOffset;

        private Vector3I _circleCenter = new Vector3I();
        private Vector3I _circleCenter2 = new Vector3I();
        private Vector3I _thridPoint = new Vector3I();

        private IMySmallMissileLauncherReload railgunReloadCheck;

        private List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListIsCharging = new List<IMySmallMissileLauncherReload>();
        private List<IMySmallMissileLauncherReload> tempRailgunListOff = new List<IMySmallMissileLauncherReload>();


        private double _groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair

        private IMyShipController _referenceBlock;
        private MyBlockOrientation _referenceBlockOrientation;
        private Vector3I _referenceBlockGridCoords = Vector3I.Zero;
        private readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        private int _state = 0;          // Which step we're on
        private int _tickCounter = 0;    // Delay counter

        private float _exhaustEffectDelay;
        private float _fireDelay;

        private string _groupName = "GAU";
        private string _id;

        // Ini stuff
        private float _rpm = -30;
        private string _rotorName = "GAU Rotor";
        private string _exhaustTag = "Exhaust";
        private int _stepDelayTicks = 2;
        private float _rotationAngle = 10;
        private float _doorOpenRatio = 0.5f;
        private int _hangarDoorsTicksToPartialyOpen = 180;


        private const float TORQUE = 100000000000f;
        private const float TORQUENORMAL = 33600000;


        public GauGeo(IMyTerminalBlock customDataProvider, IMyGridTerminalSystem gridTerminalSystem, string id = null)
        {
            _customDataProvider = customDataProvider;
            _gridTerminalSystem = gridTerminalSystem;
            _groupName = id;
            _id = id;

            GetBlocksGeneric();
            ParseIni();
            GetBlocksIni();

            IsCreated = true;
        }


        public static List<GauGeo> AcquireGAUs(IMyTerminalBlock customDataProvider, IMyGridTerminalSystem gridTerminalSystem)
        {
            List<GauGeo> gauList = new List<GauGeo>();
            if (customDataProvider == null) return gauList;

            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            gridTerminalSystem.GetBlockGroups(groups, group => group.Name.Contains(GAUGroupTag));
            foreach (IMyBlockGroup group in groups)
            {
                try
                {
                    GauGeo gau;
                    gau = new GauGeo(customDataProvider, gridTerminalSystem, group.Name);
                    if (gau.IsCreated)
                    {
                        gauList.Add(gau);
                        s_createdGAUList.Add(gau);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.ToString());
                    //TODO something something
                }
            }
            return gauList;
        }


        public static void RunWithTag(string argument, List<GauGeo> gauList, string groupNameTag)
        {
            foreach (GauGeo gau in gauList)
            {
                if (gau._groupName.Contains(groupNameTag))
                {
                    gau.Run(argument);
                }
            }
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
                command = GAUActionEnum.RELOAD;
            }
            return false;
        }

        private static void GAURuntimeManager()
        {
            foreach (GauGeo gau in s_createdGAUList)
            {
                if (gau.GAUState == GAUActionEnum.FIRE
                    || gau.GAUState == GAUActionEnum.FIRESTATE
                    || gau.GAUState == GAUActionEnum.EXHAUST
                    || gau.GAUState == GAUActionEnum.EXHAUSTEFFECT
                    || gau.GAUState == GAUActionEnum.EXHAUSTFIRE
                    || gau.GAUState == GAUActionEnum.CHARGING)
                {
                    s_gridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    return;
                }
            }
            s_gridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }
        public static bool TryRegisterGridProgram(MyGridProgram gridProgram)
        {
            if (s_gridProgram == null)
            {
                s_gridProgram = gridProgram;
                return true;
            }
            else
            {
                return false;
            }
        }



        public void Run(string argument)
        {
            Run(null, argument);
        }

        public void Run(IMyProgrammableBlock me, string argument = "")
        {
            _statusBuilder.Clear();
            _statusBuilder.AppendLine($"ID: {_id}");
            _statusBuilder.AppendLine($"Cycle: {GAUState}");

            StringBuilder scriptInfo = InfoString();

            _statusBuilder.AppendLine($"{scriptInfo}");

            tickCount++;
            if (tickCount % 100 == 1)
            {
                iniAnyChanged = ParseIni();
            }

            if (GAUActionEnum.FIRE != _gauTempCommand &&
                GAUActionEnum.EXHAUST != _gauTempCommand &&
                GAUActionEnum.FIRESTATE != _gauTempCommand &&
                GAUActionEnum.EXHAUSTEFFECT != _gauTempCommand &&
                GAUActionEnum.EXHAUSTFIRE != _gauTempCommand &&
                GAUActionEnum.CHARGING != _gauTempCommand &&
                iniAnyChanged)
            {
                GAUState = GAUActionEnum.RELOAD;
                iniAnyChanged = false;
                return;
            }
            if (me != null) me.GetSurface(0).WriteText(scriptInfo.ToString());

            if (argument != null && argument.Length != 0 && argument != "")
            {
                TryParseGauCommand(argument, out _gauTempCommand);
                return;
            }

            GAURuntimeManager(); // Modify Runtime

            if (GAUActionEnum.OFF == GAUState && GAUActionEnum.ON != _gauTempCommand)
            {
                return;
            }

            if (GAUActionEnum.NULL != _gauTempCommand &&
                GAUActionEnum.CHARGING != GAUState &&
                GAUActionEnum.ALMOSTCHARGED != GAUState)
            {
                GAUState = _gauTempCommand;
                _gauTempCommand = GAUActionEnum.NULL;
            }
            else if ((GAUActionEnum.FIRE == _gauTempCommand ||
                     GAUActionEnum.EXHAUST == _gauTempCommand) &&
                     tempRailgunListIsCharging.Count < RailgunBlockList.Count)
            {
                GAUState = _gauTempCommand;
                _gauTempCommand = GAUActionEnum.NULL;
                return;
            }

            switch (GAUState)
            {
                case GAUActionEnum.ON:
                case GAUActionEnum.RELOAD:
                    CycleOnOrReload();
                    break;

                case GAUActionEnum.OFF:
                    CycleOff();
                    break;

                case GAUActionEnum.EXHAUST:
                    CycleExhaust();
                    break;

                case GAUActionEnum.EXHAUSTEFFECT:
                    CycleExhaustEffect();
                    break;

                case GAUActionEnum.EXHAUSTFIRE:
                    CycleExhaustFire();
                    break;

                case GAUActionEnum.FIRE:
                    CycleFire();
                    break;

                case GAUActionEnum.FIRESTATE:
                    CycleFireState();
                    break;

                case GAUActionEnum.CHARGE:
                    CycleCharge();
                    break;

                case GAUActionEnum.CHARGING:
                    CycleCharging();
                    break;
                case GAUActionEnum.ALMOSTCHARGED:
                    CycleAlmostCharged();
                    break;

                default:
                    CycleDefault();
                    break;
            }

            _hasCompletedfirstRun = true;
        }

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
                if (!RailgunLooksFullyCharged(railgun))
                {
                    tempRailgunListOff.Remove(railgun);
                    break;
                }
                else
                {
                    railgun.Enabled = false;
                }
            }
            ToggleBlocks(false, RailgunBlockList);
            ToggleBlocks(false, RotorBlockList);
        }

        private void ToggleBlocks<T>(bool toggle, List<T> blockList)
        {
            blockList.ForEach(rotor => ((IMyFunctionalBlock)rotor).Enabled = toggle);
        }


        private bool IsBlockMissingInList<T>(List<T> blockList)
        {
            foreach (T blockT in blockList)
            {
                IMyTerminalBlock block = (IMyTerminalBlock)blockT;
                if (block == null || block.Closed || block.IsFunctional != true)
                {
                    return true;
                }
            }
            return false;
        }


        private bool IsBlockMissing<T>(T blockT)
        {
            IMyTerminalBlock block = (IMyTerminalBlock)blockT;
            return block == null || block.Closed || block.IsFunctional != true;
        }

    }
}
