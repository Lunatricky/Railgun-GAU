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
        public const string CL_COMMAND_CHARGE = "CHARGE";
        public const string CL_COMMAND_RELOAD = "RELOAD";

        public string arg = "";
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            GAU.ParseIni(Me); // Parse general settings
            GAU.TryRegisterGridProgram(this); // enable runtime modification
            _gauList = GAU.AcquireGAUs(Me, GridTerminalSystem); // Each gau will create its own custom data section
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                foreach (GAU gau in _gauList)
                {
                    gau.Run();
                    Echo(gau.Info.ToString());
                }
                Echo(GetRuntimeInfo());
                return;
            }

            ReadInput(argument);
        }

        public void ReadInput(string input)
        {
            bool hasValidCommand = true;

            string groupName = "";
            string command;

            int sep = input.IndexOf(':');


            if (sep < 0) // no separator
            {
                command = input.Trim();
            }
            else
            {
                command = input.Substring(0, sep).Trim();
                groupName = input.Substring(sep + 1).Trim();
            }

            switch (command.ToUpper())
            {
                case CL_COMMAND_ON:
                case CL_COMMAND_OFF:
                case CL_COMMAND_FIRE:
                case CL_COMMAND_EXHAUST:
                case CL_COMMAND_CHARGE:
                case CL_COMMAND_RELOAD:
                    break;
                default:
                    hasValidCommand = false;
                    break;
            }

            if (!hasValidCommand)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                GAU.RunWithTag(command, _gauList, groupName);
            }
            else
            {
                foreach (GAU gau in _gauList)
                {
                    gau.Run(command);
                }
            }
            Echo(GetRuntimeInfo());
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