using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
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
            Vector3D center1 = GetWorldPosition(_circleCenter);
            Vector3D center2 = GetWorldPosition(_circleCenter2);
            Vector3D point = GetWorldPosition(_thridPoint);

            // Create the axis of rotation
            Vector3D rotationAxis = Vector3D.Normalize(center2 - center1);

            // Rotate the third point around the axis by X degrees to find the rotated plane
            Vector3D rotatedPoint = RotationHelper.RotateVector(point - center1, rotationAxis, 180 +  _rotationAngle + _targetAngle + _originPlaneAngleOffset) + center1;
            Vector3D rotatedPoint2 = RotationHelper.RotateVector(point - center1, rotationAxis, 180 -_rotationAngle + _targetAngle + _originPlaneAngleOffset) + center1;

            return new List<Plane>
            {
                new Plane(center1, center2, rotatedPoint),
                new Plane(center1, center2, rotatedPoint2)
            };
        }

        public Vector3D GetWorldPosition(Vector3I localPosition)
        {
            // Convert the grid coordinates to a local position in 3D space
            Vector3D localCoords = (Vector3D)localPosition * GAUCenterBlock.CubeGrid.GridSize;

            // Transform the local position to world coordinates using the grid's WorldMatrix
            Vector3D worldCoords = Vector3D.Transform(localCoords, GAUCenterBlock.CubeGrid.WorldMatrix);

            return worldCoords;
        }

        public static Vector3I TryParseVector3I(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return new Vector3I();

            var parts = s.Split(',');
            if (parts.Length != 3)
                return new Vector3I();

            int x, y, z;

            if (!int.TryParse(parts[0], out x)) return new Vector3I();
            if (!int.TryParse(parts[1], out y)) return new Vector3I();
            if (!int.TryParse(parts[2], out z)) return new Vector3I();

            return new Vector3I(x, y, z);
        }

        public static String Vector3ItoString(Vector3I vector3I)
        { 
            return vector3I.X + ", " + vector3I.Y + ", " + vector3I.Z;
        }
    }
}
