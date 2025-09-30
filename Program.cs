using IngameScript.Utils;
using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private List<GAU> _gauList = new List<GAU>();

        // CommandLine Commands
        public const string CL_COMMAND_ON = "ON";
        public const string CL_COMMAND_OFF = "OFF";
        public const string CL_COMMAND_FIRE = "FIRE";
        public const string CL_COMMAND_EXHAUST = "EXHAUST";

        // CommandLine Switches
        public const string CL_SWITCH_GAU_TAG = "TAG";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            GAU.ParseIni(Me); // Parse general settings
            _gauList = GAU.AcquireGAUs(Me, GridTerminalSystem, this); // Each gau will create its own custom data section
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ReadInput(argument);
            foreach (GAU gau in _gauList)
            {
                gau.Run();
                Echo(gau.Info.ToString());
            }
            Echo(GetRuntimeInfo());
        }

        public void ReadInput(string input)
        {
            MyCommandLine commandLine = new MyCommandLine();          
            bool hasValidCommand = false;
            bool hasTagRestriction = false;

            if (commandLine.TryParse(input.ToUpper()))
            {
                string targetGAUTag = null;
                if (commandLine.Switches.Count != 0)
                {
                    if (commandLine.Switch(CL_SWITCH_GAU_TAG))
                    {
                        hasTagRestriction = true;
                        try
                        {
                            targetGAUTag = commandLine.Switch(CL_SWITCH_GAU_TAG, 0).ToString();
                            hasTagRestriction = true;
                        }
                        catch
                        {
                            Echo("Failed to parse target gau tag switch from command");
                            hasTagRestriction = false;
                        }
                    }
                }

                if (commandLine.ArgumentCount == 0)
                {
                    Echo("No valid argument provided");
                    return;
                }

                string command = commandLine.Argument(0);
                hasValidCommand = true;
                switch (command)
                {
                    case CL_COMMAND_ON:
                    case CL_COMMAND_OFF:
                    case CL_COMMAND_FIRE:
                    case CL_COMMAND_EXHAUST:
                        break;
                    default:
                        hasValidCommand = false;
                        break;
                }

                if (hasValidCommand)
                {
                    if (hasTagRestriction)
                    {
                        GAU.RunWithTag(command, _gauList, targetGAUTag);
                    }
                    else
                    {
                        foreach (GAU gau in _gauList)
                        {
                            gau.Run(command);
                        }
                    }
                }
            }       
        }

        public void Save()
        {

        }     
        private String GetRuntimeInfo()
        {
            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }   
    }
}