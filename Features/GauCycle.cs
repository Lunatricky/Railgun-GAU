using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;

namespace IngameScript.Domain
{
    partial class Gau
    {
        private void CycleOnOrReload()
        {
            GetBlocksGeneric();
            ParseIni();
            GetBlocksIni();
            ToggleBlocks(true, RotorBlockList);

            if (IsCharged)
            {
                GAUState = GauActionEnum.READY;
            }
            else
            {
                GAUState = GauActionEnum.CHARGE;
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
                GAUState = GauActionEnum.EXHAUSTFIRE;
            }
            else
            {
                GAUState = GauActionEnum.EXHAUSTEFFECT;
            }
        }

        private void CycleExhaustEffect()
        {
            if (IsShootTimeOut())
            {
                FinishSalvoCycle();
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
                FinishSalvoCycle();
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
                GAUState = GauActionEnum.FIRESTATE;
                return;
            }
            if (isLG && DoorBlockList.First() is IMyAirtightSlideDoor || DoorBlockList.Count == 0) { }
            else if (isLG && DoorBlockList.First() is IMyAirtightHangarDoor)
            {
                if (_shootDelay >= _hangarDoorsTicksToPartialyOpen)
                {
                    GAUState = GauActionEnum.FIRESTATE;
                }
                _hangarDoorsTicksToPartialyOpen--;
            }
            else if (!IsDoorAlmostOpen)
            {
                OpenDoors();
                return;
            }
            GAUState = GauActionEnum.FIRESTATE;
        }

        private void CycleFireState()
        {
            if (IsShootTimeOut())
            {
                FinishSalvoCycle();
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
            if (IsCharged)
            {
                ExhaustOff();
                GAUState = GauActionEnum.READY;
                return;
            }

            TrySetRotorOrRotors(TORQUENORMAL, -_rpm);
            CloseDoors();
            ToggleBlocks(true, RailgunBlockList);
            GAUState = GauActionEnum.CHARGING;
            ExhaustOff();
        }

        private void CycleCharging()
        {
            if (IsAlmostCharged)
            {
                railgunReloadCheck = null;
                GAUState = GauActionEnum.ALMOSTCHARGED;
            }
        }

        private void CycleAlmostCharged()
        {
            if (IsCharged)
            {
                GAUState = GauActionEnum.READY;
            }
        }

        private void CycleDefault()
        {
            CloseDoors();
            ExhaustOff();
            if (_hasCompletedfirstRun && !IsCharged)
            {
                GAUState = GauActionEnum.CHARGE;
            }
            else
            {
                ToggleBlocks(false, RailgunBlockList);
            }
        }

        private void FinishSalvoCycle()
        {
            ExhaustOff();
            GAUState = IsCharged ? GauActionEnum.READY : GauActionEnum.CHARGE;
        }

        private bool IsShootTimeOut()
        {
            return _shootTimeout > 8 * 60 * 60 / Math.Abs(_rpm);
        }
    }
}
