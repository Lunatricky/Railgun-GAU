using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript.Domain
{
    partial class Gau
    {

        public void GetBlocksIni()
        {
            //TODO don't pass grid terminal system, pass groups to GAU class instead
            GAUBlockGroup = _gridTerminalSystem.GetBlockGroupWithName(_groupName);
            GAUBlockGroup?.GetBlocksOfType(RotorBlockList);

            if (AreBlocksMissingFromGroupErrorMessage(RotorBlockList, "Rotor"))
            {
                return;
            }

            if (!(RotorBlockList.Count == 1 || RotorBlockList.Count == 2))
            {
                _errorBuilder.Append("\n" + $"Scrip only works with 1 or 2 rotors no more no less");
                return;
            }

            if (!TrySetRotorOrRotors(TORQUENORMAL, -_rpm))
            {
                _errorBuilder.Append("\n" + $"No rotor named {_rotorName} found in group");
                return;
            }
        }

        public void GetBlocksGeneric()
        {
            //TODO don't pass grid terminal system, pass groups to GAU class instead
            GAUBlockGroup = _gridTerminalSystem.GetBlockGroupWithName(_groupName);
            GAUBlockGroup?.GetBlocksOfType(RailgunBlockList);
            GAUBlockGroup?.GetBlocksOfType(DoorBlockList);
            GAUBlockGroup?.GetBlocksOfType(RotorBlockList);

            if (AreBlocksMissingFromGroupErrorMessage(RailgunBlockList, "Railgun") || AreBlocksMissingFromGroupErrorMessage(RotorBlockList, "Rotor")) return;

            AreBlocksMissingFromGroupWarningMessage(DoorBlockList, "Door");

            if (!(RotorBlockList.Count == 1 || RotorBlockList.Count == 2))
            {
                _errorBuilder.Append("\n" + $"Scrip only works with 1 or 2 rotors no more no less");
                return;
            }

            if (!TrySetRotorOrRotors(TORQUENORMAL, -_rpm))
            {
                _errorBuilder.Append("\n" + $"No rotor named {_rotorName} found in group");
                return;
            }

            RefreshLcds();

            List<IMyShipController> myShipControllers = new List<IMyShipController>();
            GAUBlockGroup.GetBlocksOfType(myShipControllers);

            if (AreBlocksMissingFromGroupErrorMessage(myShipControllers, "ShipControllers"))
            {
                return;
            }

            //TODO save _referenceBlockOrientation and _referenceBlockGridCoords in  custom data
            _referenceBlock = myShipControllers.First();
            _referenceBlockOrientation = _referenceBlock.Orientation; 
            _referenceBlockGridCoords = _referenceBlock.Position;

            Initialize();
            SetVectorOffsets();
            GridSizeSettings();
            ExhaustReset();
        }

        public void RefreshLcds()
        {
            LcdBlockList.Clear();

            if (!string.IsNullOrEmpty(_lcdTag))
            {
                List<IMyTextPanel> tagged = new List<IMyTextPanel>();
                _gridTerminalSystem.GetBlocksOfType(tagged, LcdTagMatch);
                AddLcdSurfaces(tagged);
            }

            if (LcdBlockList.Count == 0 && GAUBlockGroup != null)
            {
                List<IMyTextPanel> grouped = new List<IMyTextPanel>();
                GAUBlockGroup.GetBlocksOfType(grouped);
                AddLcdSurfaces(grouped);
            }

            SetupSurface(LcdBlockList);
        }

        bool LcdTagMatch(IMyTextPanel panel)
        {
            if (panel == null)
                return false;
            if (_customDataProvider != null && !panel.IsSameConstructAs(_customDataProvider))
                return false;
            string name = panel.CustomName ?? "";
            return name.Contains(_lcdTag);
        }

        void AddLcdSurfaces(List<IMyTextPanel> panels)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                IMyTextPanel panel = panels[i];
                if (panel == null)
                    continue;
                LcdBlockList.Add(panel);
            }
        }

        void SetupSurface(List<IMyTextSurface> surfaces)
        {
            for (int i = 0; i < surfaces.Count; i++)
            {
                IMyTextSurface surface = surfaces[i];
                if (surface == null)
                    continue;

                if (_lcdSprite)
                {
                    surface.AddImageToSelection("Online");
                    surface.RemoveImageFromSelection("Online");
                    surface.ContentType = ContentType.SCRIPT;
                    surface.Script = "";
                }
                else
                {
                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    surface.Font = "DEBUG";
                    surface.FontSize = 1.7f;
                    surface.Alignment = TextAlignment.LEFT;
                }
            }
        }

        public static void TickCockpits(IMyTerminalBlock me, IMyGridTerminalSystem gts, List<Gau> gaus)
        {
            s_cockpitSyncTick++;
            if (s_cockpitSyncTick == 1 || s_cockpitSyncTick % 100 == 1)
            {
                bool tagChanged = false;
                if (me != null)
                    tagChanged = ParseIni(me);
                SyncCockpits(me, gts, tagChanged || s_cockpitSyncTick == 1);
            }

            PaintCockpits(gaus);
        }

        public static void SyncCockpits(IMyTerminalBlock me, IMyGridTerminalSystem gts, bool forceReload)
        {
            s_pb = me;
            if (gts == null)
                return;

            s_cockpitScratch.Clear();
            if (!string.IsNullOrEmpty(CockpitTag))
                gts.GetBlocksOfType(s_cockpitScratch, CockpitTagMatch);

            s_cockpitSeen.Clear();
            bool slotsChanged = forceReload;
            for (int i = 0; i < s_cockpitScratch.Count; i++)
            {
                IMyCockpit cockpit = s_cockpitScratch[i];
                if (cockpit == null)
                    continue;

                s_cockpitSeen.Add(cockpit.EntityId);
                GauCockpitSpriteIni cfg = GetCockpitIni(cockpit.EntityId);
                if (cfg.Sync(cockpit))
                    slotsChanged = true;
            }

            List<long> stale = new List<long>();
            foreach (KeyValuePair<long, GauCockpitSpriteIni> pair in s_cockpitInis)
            {
                if (s_cockpitSeen.IndexOf(pair.Key) < 0)
                    stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                s_cockpitInis.Remove(stale[i]);
                slotsChanged = true;
            }

            if (!slotsChanged)
                return;

            s_cockpitSurfaces.Clear();
            for (int i = 0; i < s_cockpitScratch.Count; i++)
            {
                IMyCockpit cockpit = s_cockpitScratch[i];
                if (cockpit == null)
                    continue;

                GauCockpitSpriteIni cfg = GetCockpitIni(cockpit.EntityId);
                int index;
                if (!GauCockpitSpriteIni.TryParseSlot(cfg.Lcd, cockpit.SurfaceCount, out index))
                    continue;

                IMyTextSurface surface = cockpit.GetSurface(index);
                if (surface == null)
                    continue;

                SetupCockpitSurface(surface);
                s_cockpitSurfaces.Add(surface);
            }
        }

        static bool CockpitTagMatch(IMyCockpit cockpit)
        {
            if (cockpit == null)
                return false;
            if (s_pb != null && !cockpit.IsSameConstructAs(s_pb))
                return false;
            string name = cockpit.CustomName ?? "";
            return name.Contains(CockpitTag);
        }

        static GauCockpitSpriteIni GetCockpitIni(long id)
        {
            GauCockpitSpriteIni cfg;
            if (!s_cockpitInis.TryGetValue(id, out cfg) || cfg == null)
            {
                cfg = new GauCockpitSpriteIni();
                s_cockpitInis[id] = cfg;
            }
            return cfg;
        }

        static void SetupCockpitSurface(IMyTextSurface surface)
        {
            surface.AddImageToSelection("Online");
            surface.RemoveImageFromSelection("Online");
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
        }

        private void GridSizeSettings()
        {
            isLG = RailgunBlockList.First().CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large);

            _railGunChargeStateDetailedInfoString = (isLG ? RailgunChargeStateEnumLG.CHARGED : RailgunChargeStateEnumSG.CHARGED);

            _shootDelay = (isLG ? InGameValues.LG : InGameValues.SG);

            if (_rotationAngle == 0)
            {
                _rotationAngle = (isLG ? InGameValues.rotationAngleLG : InGameValues.rotationAngleSG);
            }

            _originPlaneAngleOffset = ShootDelayOffsetAngle;
            _fireDelay = _shootDelay * 60;
        }

        public void Initialize()
        {
            Vector3D referenceBlockWorldCoords = GetWorldPosition(_referenceBlockGridCoords);
            // Collect all exhaust caps
            List<IMyFunctionalBlock> allExhausts = new List<IMyFunctionalBlock>();
            GAUBlockGroup?.GetBlocksOfType<IMyFunctionalBlock>(allExhausts, b => b.CustomName.Contains(_exhaustTag));

            // Build a list of exhausts + distances
            List<IMyFunctionalBlock> sortedExhausts = new List<IMyFunctionalBlock>(allExhausts);
            sortedExhausts.Sort(delegate (IMyFunctionalBlock a, IMyFunctionalBlock b)
            {
                double da = Vector3D.Distance(referenceBlockWorldCoords, a.GetPosition());
                double db = Vector3D.Distance(referenceBlockWorldCoords, b.GetPosition());
                return da.CompareTo(db);
            });

            // Group exhausts by approximate distance
            exhaustLists.Clear();
            foreach (IMyFunctionalBlock sortedExhaust in sortedExhausts)
            {
                double dist = Vector3D.Distance(referenceBlockWorldCoords, sortedExhaust.GetPosition());
                bool placed = false;

                foreach (List<IMyFunctionalBlock> exhaustList in exhaustLists)
                {
                    double groupDist = Vector3D.Distance(referenceBlockWorldCoords, exhaustList[0].GetPosition());
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
                return false;
            }
        }
    }
}
