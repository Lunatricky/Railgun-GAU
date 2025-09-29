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
        private GAU gau;

        private IMyCubeGrid CubeGrid;

        private IMyGridTerminalSystem GridTerminalSystem;

        private bool hasRecompiled = true;



        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            gau = new GAU();
            gau.GetBlocks(GridTerminalSystem, CubeGrid);
        }

        public void Main(string argument)
        {
            Echo($"Cycle: {gau.GAUCommand}");
            Echo($"{gau.startString}");

            if (argument != null && argument.Length != 0 && argument != "")
            {
                try
                {
                    gau.GAUTempCommand = (GAUCommandEnum)Enum.Parse(typeof(GAUCommandEnum), argument, true);
                }
                catch
                {
                    gau.GAUTempCommand = GAUCommandEnum.ON;
                }
            }

            if (gau.errorString != "")
            {
                Echo($"{gau.errorString}");
                Echo(InstructionCount());
                return;
            }

            if (gau.GAUCommand == GAUCommandEnum.OFF && gau.GAUTempCommand != GAUCommandEnum.ON)
            {
                Echo(InstructionCount());
                return;
            }

            if (gau.GAUTempCommand != GAUCommandEnum.NULL &&
                gau.GAUCommand != GAUCommandEnum.CHARGING && 
                gau.GAUCommand != GAUCommandEnum.ALMOSTCHARGED &&
                gau.GAUCommand != GAUCommandEnum.OPENINGDOOR &&
                gau.GAUCommand != GAUCommandEnum.CLOSINGDOOR
                )
            {
                gau.GAUCommand = gau.GAUTempCommand;
                gau.GAUTempCommand = GAUCommandEnum.NULL;
            } else if (gau.GAUTempCommand == GAUCommandEnum.FIRE ||
                gau.GAUTempCommand == GAUCommandEnum.EXHAUST ||
                gau.GAUTempCommand == GAUCommandEnum.EXHAUSTEFFECT ||
                gau.GAUTempCommand == GAUCommandEnum.EXHAUSTFIRE)
            {
                gau.GAUTempCommand = GAUCommandEnum.NULL;
            }


            if (gau.GAUCommand == GAUCommandEnum.FIRE || gau.GAUCommand == GAUCommandEnum.EXHAUST ||
                gau.GAUCommand == GAUCommandEnum.EXHAUSTEFFECT || gau.GAUCommand == GAUCommandEnum.EXHAUSTFIRE || 
                gau.GAUCommand == GAUCommandEnum.CHARGING)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            if (gau.warningString.Length > 0)
            {
                Echo($"-----WARNINGS-----{gau.warningString}\n------------------");
            }

            switch (gau.GAUCommand)
            {
                case GAUCommandEnum.ON:
                case GAUCommandEnum.RESET:
                    gau.GetBlocks(GridTerminalSystem, CubeGrid);
                    ToggleBlocks(true, gau.RotorBlockList);
                    ToggleBlocks(false, gau.RailgunBlockList);
                    OpenDoors();
                    if (IsCharged())
                    {
                        gau.GAUCommand = GAUCommandEnum.READY;
                    } else
                    {
                        gau.GAUCommand = GAUCommandEnum.CHARGE;
                    }
                    break;

                case GAUCommandEnum.OFF:
                    if (IsCharged())
                        Off();
                    break;

                case GAUCommandEnum.EXHAUST:
                    gau.ExhaustReset();
                    gau.SetRotorOrRotors(gau.TORQUE);
                    if (gau.fireDelay > gau.exhaustEffectDelay)
                    {
                        gau.GAUCommand = GAUCommandEnum.EXHAUSTFIRE;
                    } else
                    {
                        gau.GAUCommand = GAUCommandEnum.EXHAUSTEFFECT;
                    }
                        break;

                case GAUCommandEnum.EXHAUSTEFFECT:
                    ExhaustEffect();
                    if (gau.fireDelay > gau.exhaustEffectDelay)
                    {
                        if (IsDoorOpen())
                        {
                            RailgunShootSalvo();
                        }
                    } else
                    {
                        gau.exhaustEffectDelay--;
                    }
                    break;

                case GAUCommandEnum.EXHAUSTFIRE:
                    if (IsDoorOpen())
                    {
                        RailgunShootSalvo();
                    }
                    if (gau.fireDelay < gau.exhaustEffectDelay)
                    {
                        ExhaustEffect();
                    } else
                    {
                        gau.fireDelay--;
                    }
                    break;

                case GAUCommandEnum.FIRE:
                    if (IsDoorOpen() && gau.SetRotorOrRotors(gau.TORQUE))
                    {
                        RailgunShootSalvo();
                    }
                    break;

                case GAUCommandEnum.CLOSINGDOOR:
                    if (!IsDoorOpen())
                    {
                        CloseDoors();
                    }
                    else
                    {
                        gau.GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                case GAUCommandEnum.OPENINGDOOR:
                    if (!IsDoorOpen())
                    {
                        OpenDoors();
                    }
                    else
                    {
                        gau.GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                case GAUCommandEnum.CHARGE:
                    gau.SetRotorOrRotors(gau.TORQUENORMAL);
                    CloseDoors();
                    ToggleBlocks(true, gau.RailgunBlockList);
                    ToggleBlocks(false, gau.RotorBlockList);
                    gau.GAUCommand = GAUCommandEnum.CHARGING;
                    break;

                case GAUCommandEnum.CHARGING:
                    if (IsAlmostCharged())
                    {
                        gau.railgunReloadCheck = null;
                        OpenDoors();
                        ToggleBlocks(true, gau.RotorBlockList);
                        gau.GAUCommand = GAUCommandEnum.ALMOSTCHARGED;
                    }
                    break;
                case GAUCommandEnum.ALMOSTCHARGED:
                    if (IsCharged())
                    {
                        OpenDoors();
                        ToggleBlocks(true, gau.RotorBlockList);
                        gau.GAUCommand = GAUCommandEnum.READY;
                    }
                    break;

                default:
                    if (hasRecompiled && !IsCharged())
                    {
                        gau.GAUCommand = GAUCommandEnum.CHARGE;
                        hasRecompiled = false;
                    } else
                    {
                        ToggleBlocks(false, gau.RailgunBlockList);
                    }
                    break;
            }

            if (gau.areBlocksMissing)
            {
                gau.GAUCommand = GAUCommandEnum.RESET;
            }

            Echo(InstructionCount());
        }

        private String InstructionCount()
        {
            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }

        private void Off()
        {
            CloseDoors();

            if (gau.tempRailgunListOff.Count == 0)
            {
                gau.tempRailgunListOff = new List<IMySmallMissileLauncherReload>(gau.RailgunBlockList);
            }

            gau.tempRailgunListOff.ForEach(railgun => railgun.Enabled = true);

            foreach (IMySmallMissileLauncherReload railgun in gau.tempRailgunListOff)
            {
                if (IsBlockMissing(railgun))
                {
                    continue;
                }

                string detailString = railgun.DetailedInfo;

                if (!detailString.Contains(RailgunChargeStateEnum.CHARGED))
                {
                    gau.tempRailgunListOff.Remove(railgun);
                    break;
                }
                else
                {
                    railgun.Enabled = false;
                }
            }

            if (gau.tempRailgunListOff.Count == 0)
            {
                ToggleBlocks(false, gau.RailgunBlockList);
                ToggleBlocks(false, gau.RotorBlockList);
            }
        }

        private void RailgunShootSalvo()
        {

            if (gau.tempRailgunListShootSalvo.Count == 0)
            {
                gau.tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>(gau.RailgunBlockList);
            }

            List<Plane> rotatedPlanes = getRotatedPlanes();

            foreach (var railgun in gau.tempRailgunListShootSalvo.ToList())
            {
                if (IsPointBetweenAngles(rotatedPlanes, railgun.GetPosition()))
                {
                    ShootRailgun(railgun);
                    if (gau.railgunReloadCheck == null)
                    {
                        gau.railgunReloadCheck = railgun;
                    }
                }

                if (!railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED))
                {
                    gau.tempRailgunListShootSalvo.Remove(railgun);
                }
            }

            if (gau.tempRailgunListShootSalvo.Count == 0)
            {
                gau.GAUCommand = GAUCommandEnum.CHARGE;
                ExhaustOff();
            }
        }

        private bool IsCharged()
        {
            bool isCharged = false;
            int chargedCounter = 0;

            if (gau.tempRailgunListIsCharged.Count == 0)
            {
                gau.tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>(gau.RailgunBlockList);
            }

            foreach (IMySmallMissileLauncherReload railgun in gau.tempRailgunListIsCharged.ToList())
            {
                int checkCounter = CheckCounter(chargedCounter, railgun, RailgunChargeStateEnum.CHARGED);
                if (checkCounter != chargedCounter)
                {
                    chargedCounter = checkCounter;
                    railgun.Enabled = false;
                    gau.tempRailgunListIsCharged.Remove(railgun);
                }
            }

            if (gau.tempRailgunListIsCharged.Count == 0)
            {
                isCharged = true;
            }

            return isCharged;
        }

        private bool IsAlmostCharged()
        {
            bool result = false;

            if (!IsBlockMissing(gau.railgunReloadCheck))
            {
                return gau.railgunReloadCheck.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
            } else
            {
                foreach (IMySmallMissileLauncherReload railgun in gau.RailgunBlockList)
                {
                    result = railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED);
                }
            }
            return result;
        }

        private static int CheckCounter(int chargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
        {
            string detailString = railgun.DetailedInfo;

            if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
            {
                chargeCounter++;
            }

            return chargeCounter;
        }

        private void ToggleBlocks<T>(bool toggle, List<T> blockList)
        {
            blockList.ForEach(rotor => ((IMyFunctionalBlock)rotor).Enabled = toggle);
        }

        private bool IsDoorOpen()
        {
            foreach (IMyDoor door in gau.DoorBlockList)
            {
                if (IsBlockMissing(door) && door.Status != DoorStatus.Open)
                {
                    gau.GAUCommand = GAUCommandEnum.OPENINGDOOR;
                    return false;
                }
            }
            return true;
        }

        private void OpenDoors()
        {
            foreach (IMyDoor door in gau.DoorBlockList)
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
            foreach (IMyDoor door in gau.DoorBlockList)
            {
                if (IsBlockMissing(door))
                {
                    continue;
                }
                door.CloseDoor();
            }
        }

        private bool IsBlockMissing<T>(T blockT)
        {
            IMyTerminalBlock block = (IMyTerminalBlock)blockT;
            return block == null || block.Closed || block.IsFunctional != true;
        }

        private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
        {
            railgun.Enabled = true;
            railgun.ShootOnce();
        }

        public Vector3D GetWorldPosition(Vector3I localPosition)
        {
            // Convert the grid coordinates to a local position in 3D space
            Vector3D localCoords = (Vector3D)localPosition * CubeGrid.GridSize;

            // Transform the local position to world coordinates using the grid's WorldMatrix
            Vector3D worldCoords = Vector3D.Transform(localCoords, CubeGrid.WorldMatrix);

            return worldCoords;
        }

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
            Vector3D center1 = GetWorldPosition(gau.circleCenter);
            Vector3D center2 = GetWorldPosition(gau.circleCenter2);
            Vector3D point = GetWorldPosition(gau.thridPoint);

            // Create the axis of rotation
            Vector3D rotationAxis = Vector3D.Normalize(center2 - center1);

            // Rotate the third point around the axis by X degrees to find the rotated plane
            Vector3D rotatedPoint = RotationHelper.RotateVector(point - center1, rotationAxis, gau.rotationAngle + gau.targetAngle + gau.originPlaneAngleOffset) + center1;
            Vector3D rotatedPoint2 = RotationHelper.RotateVector(point - center1, rotationAxis, -gau.rotationAngle + gau.targetAngle + gau.originPlaneAngleOffset) + center1;

            return new List<Plane>
            {
                new Plane(center1, center2, rotatedPoint),
                new Plane(center1, center2, rotatedPoint2)
            };
        }

        public bool IsPointBetweenAngles(List<Plane> rotatedPlanes, Vector3D railgunPosition)
        {
            // Check if the fourth point is between the two planes
            float distanceToRotated = rotatedPlanes[0].SignedDistance(railgunPosition);
            float distanceToRotated2 = rotatedPlanes[1].SignedDistance(railgunPosition);

            return distanceToRotated2 > 0 && distanceToRotated < 0;
        }

        public void ExhaustEffect()
        {
            // === TURNING ON ===
            if (gau.state < gau.exhaustLists.Count)
            {
                if (gau.tickCounter >= gau.stepDelayTicks)
                {
                    gau.exhaustLists[gau.state].ForEach(exhaust => exhaust.Enabled = true);
                    gau.state++;
                    gau.tickCounter = 0;
                }
                else
                {
                    gau.tickCounter++;
                }
            }
        }

        public void ExhaustOff()
        {
            // === TURNING OFF ===
            gau.exhaustLists.ForEach(exhaustList => exhaustList.ForEach(exhaust => exhaust.Enabled = false));
        }
    }
}