using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    partial class Gau
    {
        private string IniSectionGAU
        {
            get
            {
                return $"{INI_SECTION_GAU_GENERAL} - {_id}";
            }
        }



        // Keys
        private const string INI_KEY_GENERAL_GAU_GROUP_TAG = "GAU Group Tag";
        private const string INI_KEY_GENERAL_COCKPIT_TAG = "Cockpit Tag";

        // Sections
        private const string INI_SECTION_GENERAL = "GAU Script General Settings";

        // Keys
        private const string INI_KEY_GAU_RPM = "RPM";
        private const string INI_KEY_GAU_MAIN_ROTOR_NAME = "Rotor Name";
        private const string INI_KEY_GAU_EXHAUST_TAG = "Exhaust Tag";
        private const string INI_KEY_GAU_STEP_DELAY_TICKS = "Step Delay Ticks";
        private const string INI_KEY_GAU_TARGET_ANGLE = "Target Angle";
        private const string INI_KEY_GAU_ROTATION_ANGLE = "Angle Offset";
        private const string INI_KEY_GAU_DOOR_OPEN_RATIO = "Door Open Ratio";
        private const string INI_KEY_GAU_LCD_SPRITE = "LCD Sprite";
        private const string INI_KEY_GAU_LCD_TAG = "LCD Tag";
        private const string REFERENCE_BLOCK_GRID_COORDS = "Reference Grid Coords";

        // Sections
        private const string INI_SECTION_GAU_GENERAL = "GAU - Settings";


        public static bool ParseIni(IMyTerminalBlock customDataProvider)
        {
            s_iniGeneral.Clear();
            string customData = customDataProvider.CustomData;
            bool parsed = s_iniGeneral.TryParse(customData);

            string section = INI_SECTION_GENERAL;

            if (!s_iniGeneral.ContainsSection(section))
            {
                s_iniGeneral.AddSection(section);
            }


            GAUGroupTag = s_iniGeneral.Get(section, INI_KEY_GENERAL_GAU_GROUP_TAG).ToString(GAUGroupTag);
            string cockpitTag = s_iniGeneral.Get(section, INI_KEY_GENERAL_COCKPIT_TAG).ToString(CockpitTag);
            bool cockpitTagChanged = cockpitTag != CockpitTag;
            CockpitTag = cockpitTag;

            s_iniGeneral.Set(section, INI_KEY_GENERAL_GAU_GROUP_TAG, GAUGroupTag);
            s_iniGeneral.Set(section, INI_KEY_GENERAL_COCKPIT_TAG, CockpitTag);


            string output = s_iniGeneral.ToString();
            if (!string.Equals(output, customDataProvider.CustomData))
            {
                customDataProvider.CustomData = output;
            }
            return cockpitTagChanged;
        }

        private bool ParseIni()
        {
            bool iniAnyChanged = false;
            s_iniGeneral.Clear();
            string customData = _customDataProvider.CustomData;
            bool parsed = s_iniGeneral.TryParse(customData);

            string sectionName = IniSectionGAU;

            if (!s_iniGeneral.ContainsSection(sectionName))
            {
                s_iniGeneral.AddSection(sectionName);
            }

            String referenceBlockGridCoords;

            _rpm = (float)s_iniGeneral.Get(sectionName, INI_KEY_GAU_RPM).ToDouble(_rpm);
            _rotorName = s_iniGeneral.Get(sectionName, INI_KEY_GAU_MAIN_ROTOR_NAME).ToString(_rotorName);
            _exhaustTag = s_iniGeneral.Get(sectionName, INI_KEY_GAU_EXHAUST_TAG).ToString(_exhaustTag);
            _stepDelayTicks = s_iniGeneral.Get(sectionName, INI_KEY_GAU_STEP_DELAY_TICKS).ToInt32(_stepDelayTicks);
            _targetAngle = (float)s_iniGeneral.Get(sectionName, INI_KEY_GAU_TARGET_ANGLE).ToDouble(_targetAngle);
            _rotationAngle = (float)s_iniGeneral.Get(sectionName, INI_KEY_GAU_ROTATION_ANGLE).ToDouble(_rotationAngle);
            _doorOpenRatio = (float)s_iniGeneral.Get(sectionName, INI_KEY_GAU_DOOR_OPEN_RATIO).ToDouble(_doorOpenRatio);
            _lcdSprite = s_iniGeneral.Get(sectionName, INI_KEY_GAU_LCD_SPRITE).ToBoolean(_lcdSprite);
            _lcdTag = s_iniGeneral.Get(sectionName, INI_KEY_GAU_LCD_TAG).ToString(_lcdTag);
            referenceBlockGridCoords = s_iniGeneral.Get(sectionName, REFERENCE_BLOCK_GRID_COORDS).ToString(Vector3ItoString(_referenceBlockGridCoords));

            // Get Reference block grid coords from CD
            _referenceBlockGridCoords = TryParseVector3I(referenceBlockGridCoords);
            
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_RPM, _rpm);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_MAIN_ROTOR_NAME, _rotorName);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_EXHAUST_TAG, _exhaustTag);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_STEP_DELAY_TICKS, _stepDelayTicks);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_TARGET_ANGLE, _targetAngle);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_ROTATION_ANGLE, _rotationAngle);
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_DOOR_OPEN_RATIO, _doorOpenRatio);
            bool lcdChanged = ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_LCD_SPRITE, _lcdSprite);
            lcdChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, INI_KEY_GAU_LCD_TAG, _lcdTag);
            iniAnyChanged |= lcdChanged;
            iniAnyChanged |= ReadAndDetectChange(s_iniGeneral, IniSectionGAU, REFERENCE_BLOCK_GRID_COORDS, referenceBlockGridCoords);

            string output = s_iniGeneral.ToString();
            _customDataProvider.CustomData = output;
            if (!string.Equals(output, _customDataProvider.CustomData))
            {
                _customDataProvider.CustomData = output;
            }
            if (lcdChanged)
                RefreshLcds();
            return iniAnyChanged;
        }

        bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());

            string old;
            string newValString = newVal.ToString();
            __snapshot.TryGetValue(key, out old);
            if (old != newValString)
            {
                __snapshot[key] = newValString;
                return true;
            }
            return false;
        }
    }
}
