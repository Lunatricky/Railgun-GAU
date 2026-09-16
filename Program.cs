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
        private List<GauGeo> _gauList = new List<GauGeo>();

        // CommandLine Commands
        public const string CL_COMMAND_ON = "ON";
        public const string CL_COMMAND_OFF = "OFF";
        public const string CL_COMMAND_FIRE = "FIRE";
        public const string CL_COMMAND_EXHAUST = "EXHAUST";
        public const string CL_COMMAND_CHARGE = "CHARGE";
        public const string CL_COMMAND_RELOAD = "RELOAD";

        private IMyProgrammableBlock me;

        public string arg = "";

        double maxRuntimeMs = 0;
        int tickCounter = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            me = Me;
            GauGeo.ParseIni(me); // Parse general settings
            GauGeo.TryRegisterGridProgram(this); // enable runtime modification
            _gauList = GauGeo.AcquireGAUs(me, GridTerminalSystem); // Each gau will create its own custom data section
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                foreach (GauGeo gau in _gauList)
                {
                    gau.Run(me);
                    Echo(gau.Info.ToString());
                    foreach(IMyTextSurface surface in gau.LcdBlockList)
                    {
                        surface.WriteText(gau.Info);
                    }
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
                GauGeo.RunWithTag(command, _gauList, groupName);
            }
            else
            {
                foreach (GauGeo gau in _gauList)
                {
                    gau.Run(command);
                }
            }
            Echo(GetRuntimeInfo());
        }

        private String GetRuntimeInfo()
        {
            tickCounter++;

            if (tickCounter % 20 == 1)
            {
                maxRuntimeMs = 0;
            }

            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");

            double newRuntimeMs = Math.Round(Runtime.LastRunTimeMs, 5);
            maxRuntimeMs = Math.Max(newRuntimeMs, maxRuntimeMs);

            m_echoBuilder.Append($"Max Runtime: {maxRuntimeMs} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }   
    }
}