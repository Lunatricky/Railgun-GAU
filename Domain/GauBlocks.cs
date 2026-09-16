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
    partial class GauGeo
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
            GAUBlockGroup?.GetBlocksOfType(LcdBlockList);

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

            SetupSurface(LcdBlockList);

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

        private static void SetupSurface(List<IMyTextSurface> surfaces)
        {
            foreach (IMyTextSurfaceProvider surfaceProvider in surfaces)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    var surface = surfaceProvider.GetSurface(0);

                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    surface.Font = "DEBUG";
                    surface.FontSize = 1.7f;
                    surface.Alignment = TextAlignment.LEFT;
                }
            }
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
