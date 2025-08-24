using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    partial class Exhaust : MyGridProgram
    {
        // === Exhaust Sequence Controller ===
        // Compatible with C#6 for Space Engineers Programmable Blocks
        // Turns "Exhaust Cap" blocks on in pairs from closest to furthest, waits, then turns them off.
        // -----------------------------------------------

        private const string referenceBlockName = "Main Cockpit";  // Change this to your reference block name
        private const string exhaustName = "Exhaust Cap";          // Must be part of exhaust block names
        private const double groupTolerance = 0.1;                 // Distance tolerance to consider blocks a pair
        private const int stepDelayTicks = 5;                     // Delay between group activations (~0.5s at 60 ticks/sec)

        private IMyGridTerminalSystem gridTerminalSystem;

        private IMyTerminalBlock reference;
        private readonly List<List<IMyFunctionalBlock>> exhaustLists = new List<List<IMyFunctionalBlock>>();
        private int state = 0;          // Which step we're on
        private int tickCounter = 0;    // Delay counter

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

        public bool ExhaustEffect(string arg)
        {
            if (arg != null && arg.ToLower() == "run")
            {
                state = 0;
                tickCounter = 0;
                return false;
            }
            
            // === TURNING ON ===
            if (state < exhaustLists.Count)
            {
                if (tickCounter >= stepDelayTicks)
                {
                    exhaustLists[state].ForEach(exhaust => exhaust.Enabled = true);
                    state++;
                    tickCounter = 0;
                }
            }

            return state >= exhaustLists.Count;
        }

        public void ExhaustOff()
        {
            // === TURNING OFF ===
            exhaustLists.ForEach(exhaustList => exhaustList.ForEach(exhaust => exhaust.Enabled = false));
        }
    }
}
