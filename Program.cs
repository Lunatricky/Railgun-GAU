using IngameScript.Utils;
using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private List<GAU> _gauList = new List<GAU>();          

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            GAU.ParseIni(Me); // Parse general settings
            _gauList = GAU.AcquireGAUs(Me, GridTerminalSystem, this); // Each gau will create its own custom data section
        }

        public void Main(string argument, UpdateType updateSource)
        {
            foreach (GAU gau in _gauList)
            {
                gau.Run(argument);
            }

            Echo(GetRuntimeInfo());
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