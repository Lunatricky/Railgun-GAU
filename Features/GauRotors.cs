using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;
using VRageMath;

namespace IngameScript.Domain
{
    partial class Gau
    {
        private void ConfigureGAURotors(IMyMotorStator motorStator, float torque, float targetVelocityRPM)
        {
            motorStator.Torque = torque;
            motorStator.BrakingTorque = torque;
            motorStator.TargetVelocityRPM = targetVelocityRPM;
            motorStator.UpperLimitRad = 0;
            GAUCenterBlock = motorStator;
            if (_circleCenter == new Vector3I())
            {
                _circleCenter = GAUCenterBlock.Position;
            }
        }

        public bool TrySetRotorOrRotors(float torque)
        {
            return TrySetRotorOrRotors(torque, _rpm);
        }

            public bool TrySetRotorOrRotors(float torque, float _rpm)
        {
            bool MainRotorExists = false;

            if (RotorBlockList.Count == 1)
            {
                ConfigureGAURotors(RotorBlockList.First(), torque, _rpm);
                MainRotorExists = true;
            }
            else
            {
                foreach (IMyMotorStator rotor in RotorBlockList)
                {
                    if (rotor.CustomName.ToLower().Contains(_rotorName.ToLower()))
                    {
                        ConfigureGAURotors(rotor, torque, _rpm);
                        MainRotorExists = true;
                    }
                    else
                    {
                        ConfigureGAURotors(rotor, torque, -_rpm);
                    }
                }
            }
            return MainRotorExists;
        }

        private void SetVectorOffsets()
        {
            if (_referenceBlockOrientation == null ||(_circleCenter2 != new Vector3I() && _thridPoint != new Vector3I()))
            {
                return;
            }

            _circleCenter2 = _circleCenter - Base6Directions.GetIntVector(_referenceBlockOrientation.Forward);
            _thridPoint = _circleCenter - Base6Directions.GetIntVector(_referenceBlockOrientation.Up);
        }

        double GetRotorAngle360(IMyMotorStator rotor)
        {
            if (rotor == null || rotor.Closed) return 0.0;

            double degrees = MathHelper.ToDegrees(rotor.Angle);
            return (degrees + 360) % 360;        // Converts to 0–360 range
        }
    }
}
