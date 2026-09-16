using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        private void CycleOnOrReload()
        {
            GetBlocksGeneric();
            ParseIni();
            GetBlocksIni();
            ToggleBlocks(true, RotorBlockList);
            ToggleBlocks(false, RailgunBlockList);

            if (IsCharged)
            {
                GAUState = GAUActionEnum.READY;
            }
            else
            {
                GAUState = GAUActionEnum.CHARGE;
            }
        }

        private void CycleOff()
        {
            if (IsCharged)
                Off();
        }

        private void CycleExhaust()
        {
            tempRailgunListShootSalvo.Clear();
            foreach (IMySmallMissileLauncherReload railgun in RailgunBlockList)
            {
                if (CheckRailgunChargeState(railgun, RailgunChargeStateEnum.CHARGED))
                    tempRailgunListShootSalvo.Add(railgun);
            }

            _shootTimeout = 0;
            TrySetRotorOrRotors(TORQUE, _rpm);
            if (!IsDoorAlmostOpen)
            {
                OpenDoors();
                return;
            }
            ExhaustReset();
            if (_fireDelay > _exhaustEffectDelay)
            {
                GAUState = GAUActionEnum.EXHAUSTFIRE;
            }
            else
            {
                GAUState = GAUActionEnum.EXHAUSTEFFECT;
            }
        }

        private void CycleExhaustEffect()
        {
            if (IsShootTimeOut())
            {
                GAUState = GAUActionEnum.CHARGE;
                return;
            }

            _shootTimeout++;

            if (!IsDoorOpen)
            {
                OpenDoors();
            }
            TriggerExhaustEffect();
            if (_fireDelay > _exhaustEffectDelay)
            {
                RailgunShootSalvo();
            }
            else
            {
                _exhaustEffectDelay--;
            }
        }

        private void CycleExhaustFire()
        {
            if (IsShootTimeOut())
            {
                GAUState = GAUActionEnum.CHARGE;
                return;
            }

            _shootTimeout++;

            if (!IsDoorOpen)
            {
                OpenDoors();
            }
            RailgunShootSalvo();
            if (_fireDelay < _exhaustEffectDelay)
            {
                TriggerExhaustEffect();
            }
            else
            {
                _fireDelay--;
            }
        }

        private void CycleFire()
        {
            _shootTimeout = 0;
            TrySetRotorOrRotors(TORQUE, _rpm);
            if (DoorBlockList == null || DoorBlockList.Count == 0)
            {
                GAUState = GAUActionEnum.FIRESTATE;
                return;
            }
            if (isLG && DoorBlockList.First() is IMyAirtightSlideDoor || DoorBlockList.Count == 0) { }
            else if (isLG && DoorBlockList.First() is IMyAirtightHangarDoor)
            {
                if (_shootDelay >= _hangarDoorsTicksToPartialyOpen)
                {
                    GAUState = GAUActionEnum.FIRESTATE;
                }
                _hangarDoorsTicksToPartialyOpen--;
            }
            else if (!IsDoorAlmostOpen)
            {
                OpenDoors();
                return;
            }
            GAUState = GAUActionEnum.FIRESTATE;
        }

        private void CycleFireState()
        {
            if (IsShootTimeOut())
            {
                GAUState = GAUActionEnum.CHARGE;
                return;
            }

            _shootTimeout++;

            _hangarDoorsTicksToPartialyOpen = 180;
            if (!IsDoorOpen)
            {
                OpenDoors();
            }
            RailgunShootSalvo();
        }

        private void CycleCharge()
        {
            TrySetRotorOrRotors(TORQUENORMAL, -_rpm);
            CloseDoors();
            ToggleBlocks(true, RailgunBlockList);
            GAUState = GAUActionEnum.CHARGING;
            ExhaustOff();
        }

        private void CycleCharging()
        {
            if (IsAlmostCharged)
            {
                railgunReloadCheck = null;
                GAUState = GAUActionEnum.ALMOSTCHARGED;
            }
        }

        private void CycleAlmostCharged()
        {
            if (IsCharged)
            {
                GAUState = GAUActionEnum.READY;
            }
        }

        private void CycleDefault()
        {
            CloseDoors();
            ExhaustOff();
            if (_hasCompletedfirstRun && !IsCharged)
            {
                GAUState = GAUActionEnum.CHARGE;
            }
            else
            {
                ToggleBlocks(false, RailgunBlockList);
            }
        }

        private bool IsShootTimeOut()
        {
            return _shootTimeout > 8 * 60 * 60 / Math.Abs(_rpm);
        }
    }
}
