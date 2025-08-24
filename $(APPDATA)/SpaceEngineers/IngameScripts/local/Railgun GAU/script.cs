/*
 * R e a d m e
 * -----------
 * 
 * In this file you can include any instructions or other comments you want to have injected onto the 
 * top of your final script. You can safely delete this file if you do not want any such comments.
 */


private const string GAU_GROUP_NAME = "GAU";
private const string MAIN_ROTOR_NAME = "GAU Rotor";

private const float TORQUE = 100000000000f;
private const float RPM = -30f;

private const float shootTimeLG = 2.02f;
private const float shootTimeSG = 0.52f;

// Configurable variables
private float shootDelay;  // Shooting delay in seconds
private float targetAngle = 0; // Target angle in degrees

private string errorString = "";
private string warningString = "";
private string startString = "";

private GAUCommandEnum GAUCommand;
private GAUCommandEnum GAUTempCommand;
private float rotationAngle;
private float rotationAngleSG = 5.5f;
private float rotationAngleLG = 3.5f;
private bool areBlocksMissing = false;
private float originPlaneAngleOffset = 0;

private IMyMotorStator GAUCenterBlock;

private IMyCubeGrid CubeGrid;

private Vector3I circleCenter = new Vector3I();
private Vector3I circleCenter2 = new Vector3I();
private Vector3I thridPoint = new Vector3I();

private List<IMySmallMissileLauncherReload> railgunBlockList = new List<IMySmallMissileLauncherReload>();
private List<IMyDoor> doorBlockList = new List<IMyDoor>();
private List<IMyMotorStator> rotorBlockList = new List<IMyMotorStator>();

private List<IMySmallMissileLauncherReload> tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>();
private List<IMySmallMissileLauncherReload> tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>();

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    GetBlocks();
}

public void Main(string argument)
{
    Echo($"Cicle: {GAUCommand}");
    Echo($"{startString}");

    if (argument != null && argument.Length != 0 && argument != "")
    {
        try
        {
            GAUTempCommand = (GAUCommandEnum)Enum.Parse(typeof(GAUCommandEnum), argument);
        } catch {
            GAUTempCommand = GAUCommandEnum.ON;
        }
    }

    if (errorString != "")
    {
        Echo($"{errorString}");
        Echo(InstructionCount());
        return;
    }

    if (GAUCommand == GAUCommandEnum.OFF && GAUTempCommand != GAUCommandEnum.ON)
    {
        Echo(InstructionCount());
        return;
    }

    if (GAUCommand != GAUCommandEnum.CHARGING)
    {
        GAUCommand = GAUTempCommand;
    }

    if (GAUCommand == GAUCommandEnum.FIRE)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    }
    else
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }

    if (warningString.Length > 0)
    {
        Echo($"-----WARNINGS-----{warningString}\n------------------");
    }

    switch (GAUCommand)
    {
        case GAUCommandEnum.ON:
        case GAUCommandEnum.RESET:
            GetBlocks();
            ToggleBlocks(true, rotorBlockList);
            ToggleBlocks(false, railgunBlockList);
            OpenDoors();
            GAUCommand = GAUCommandEnum.READY;
            break;

        case GAUCommandEnum.OFF:
            if (IsCharged())
                Off();
            break;

        case GAUCommandEnum.FIRE:
            if (IsDoorOpen())
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
                GAUCommand = GAUCommandEnum.READY;
            }
            break;

        case GAUCommandEnum.OPENINGDOOR:
            if (!IsDoorOpen())
            {
                OpenDoors();
            }
            else
            {
                GAUCommand = GAUCommandEnum.READY;
            }
            break;

        case GAUCommandEnum.CHARGE:
            CloseDoors();
            ToggleBlocks(true, railgunBlockList);
            ToggleBlocks(false, rotorBlockList);
            GAUCommand = GAUCommandEnum.CHARGING;
            GAUTempCommand = GAUCommandEnum.CHARGING;
            break;

        case GAUCommandEnum.CHARGING:
            if (IsAlmostCharged())
            {
                OpenDoors();
                ToggleBlocks(true, rotorBlockList);
            }
            if (IsCharged())
            {
                OpenDoors();
                ToggleBlocks(true, rotorBlockList);
                GAUCommand = GAUCommandEnum.READY;
                GAUTempCommand = GAUCommandEnum.READY;
            }
            break;

        default:
            if (!IsCharged())
            {
                GAUCommand = GAUCommandEnum.CHARGE;
            }
            break;
    }

    if (areBlocksMissing)
    {
        GAUCommand = GAUCommandEnum.RESET;
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
    int chargingCounter = 0;
    CloseDoors();

    railgunBlockList.ForEach(railgun => railgun.Enabled = true);

    foreach (IMySmallMissileLauncherReload railgun in railgunBlockList)
    {
        if (IsBlockMissing(railgun))
        {
            continue;
        }

        string detailString = railgun.DetailedInfo;

        if (!detailString.Contains(RailgunChargeStateEnum.CHARGED))
        {
            chargingCounter++;
            break;
        }
        else
        {
            railgun.Enabled = false;
        }
    }

    if (chargingCounter == 0)
    {
        ToggleBlocks(false, railgunBlockList);
        ToggleBlocks(false, rotorBlockList);
    }
}

private void RailgunShootSalvo()
{

    if (tempRailgunListShootSalvo.Count == 0)
    {
        tempRailgunListShootSalvo = new List<IMySmallMissileLauncherReload>(railgunBlockList);
    }

    List<Plane> rotatedPlanes = getRotatedPlanes();

    foreach (var railgun in tempRailgunListShootSalvo.ToList())
    {
        if (IsPointBetweenAngles(rotatedPlanes, railgun.GetPosition()))
        {
            ShootRailgun(railgun);
        }

        if (!railgun.DetailedInfo.Contains(RailgunChargeStateEnum.CHARGED))
        {
            tempRailgunListShootSalvo.Remove(railgun);
        }
    }

    if (tempRailgunListShootSalvo.Count == 0)
    {
        GAUCommand = GAUCommandEnum.CHARGE;
        GAUTempCommand = GAUCommandEnum.CHARGE;
    }
}

private bool IsCharged()
{
    return IsCharged(1f);
}

private bool IsCharged(float percentage)
{
    bool isCharged = false;
    int chargedCounter = 0;

    if (tempRailgunListIsCharged.Count == 0)
    {
        tempRailgunListIsCharged = new List<IMySmallMissileLauncherReload>(railgunBlockList);
    }

    foreach (IMySmallMissileLauncherReload railgun in tempRailgunListIsCharged.ToList())
    {
        int checkCounter = CheckCounter(chargedCounter, railgun, RailgunChargeStateEnum.CHARGED);
        if (checkCounter != chargedCounter)
        {
            chargedCounter = checkCounter;
            railgun.Enabled = false;
            tempRailgunListIsCharged.Remove(railgun);
        }
    }

    if (chargedCounter >= percentage * tempRailgunListIsCharged.Count)
    {
        isCharged = true;
    }

    return isCharged;
}

private bool IsCharging()
{
    bool isCharging = false;
    int chargingCounter = 0;
    foreach (IMySmallMissileLauncherReload railgun in railgunBlockList)
    {
        chargingCounter = CheckCounter(chargingCounter, railgun, RailgunChargeStateEnum.CHARGED);
    }

    if (chargingCounter == 0)
    {
        isCharging = true;
        GAUCommand = GAUCommandEnum.CHARGE;
    }

    return isCharging;
}

private bool IsAlmostCharged()
{
    foreach (IMySmallMissileLauncherReload railgun in railgunBlockList)
    {
        if (!IsBlockMissing(railgun) && railgun.Enabled == false)
        {
            return true;
        }
    }
    return false;
}

private static int CheckCounter(int ChargeCounter, IMySmallMissileLauncherReload railgun, string railgunChargeState)
{
    string detailString = railgun.DetailedInfo;

    if (railgun.IsFunctional && detailString.Contains(railgunChargeState))
    {
        ChargeCounter++;
    }

    return ChargeCounter;
}

private void ToggleBlocks<T>(bool toggle, List<T> blockList)
{
    blockList.ForEach(rotor => ((IMyFunctionalBlock)rotor).Enabled = toggle);
}

private bool IsDoorOpen()
{
    foreach (IMyAirtightHangarDoor door in doorBlockList)
    {
        if (IsBlockMissing(door) && door.Status != DoorStatus.Open)
        {
            GAUCommand = GAUCommandEnum.OPENINGDOOR;
            return false;
        }
    }
    return true;
}

void GetBlocks()
{
    errorString = "";
    warningString = "";
    startString = "";
    IMyBlockGroup GAUBlockGroup = GridTerminalSystem.GetBlockGroupWithName(GAU_GROUP_NAME);

    GAUBlockGroup?.GetBlocksOfType(railgunBlockList);
    GAUBlockGroup?.GetBlocksOfType(rotorBlockList);
    GAUBlockGroup?.GetBlocksOfType(doorBlockList);

    if (AreBlocksMissingFromGroupErrorMessage(railgunBlockList, "Railgun") || AreBlocksMissingFromGroupErrorMessage(rotorBlockList, "Rotor"))
    {
        return;
    }

    AreBlocksMissingFromGroupWarningMessage(doorBlockList, "Door");

    if (!(rotorBlockList.Count == 1 || rotorBlockList.Count == 2))
    {
        errorString = errorString + "\n" + $"Scrip only works with 1 or 2 rotors no more no less";
        areBlocksMissing = true;
        return;
    }

    bool MainRotorExists = false;

    if (rotorBlockList.Count == 1)
    {
        ConfigureGAURotors(rotorBlockList.First(), TORQUE, RPM);
        MainRotorExists = true;
    }
    else
    {
        foreach (IMyMotorStator rotor in rotorBlockList)
        {
            if (rotor.CustomName.Contains(MAIN_ROTOR_NAME))
            {
                ConfigureGAURotors(rotor, TORQUE, RPM);
                MainRotorExists = true;
            }
            else
            {
                ConfigureGAURotors(rotor, -1 * TORQUE, RPM);
            }
        }
    }

    if (!MainRotorExists)
    {
        errorString = errorString + "\n" + $"No rotor named {MAIN_ROTOR_NAME} found in group";
        areBlocksMissing = true;
        return;
    }

    CubeGrid = GAUCenterBlock.CubeGrid;
    VectorOffsets();

    if (railgunBlockList.First().CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large))
    {
        RailgunChargeStateEnum.CHARGED = RailgunChargeStateEnumLG.CHARGED;
        RailgunChargeStateEnum.ALMOST = RailgunChargeStateEnumLG.ALMOST;
        shootDelay = shootTimeLG;
        rotationAngle = rotationAngleLG;
    }
    else
    {
        RailgunChargeStateEnum.CHARGED = RailgunChargeStateEnumSG.CHARGED;
        RailgunChargeStateEnum.ALMOST = RailgunChargeStateEnumSG.ALMOST;
        shootDelay = shootTimeSG;
        rotationAngle = rotationAngleSG;
    }

    originPlaneAngleOffset = ShootDelayOffsetAngle();
}

private void ConfigureGAURotors(IMyMotorStator motorStator, float torque, float targetVelocityRPM)
{
    motorStator.Torque = torque;
    motorStator.BrakingTorque = torque;
    motorStator.TargetVelocityRPM = targetVelocityRPM;
    GAUCenterBlock = motorStator;
    circleCenter = GAUCenterBlock.Position;
}

private void VectorOffsets()
{
    Vector3I behind = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Backward);
    Vector3I below = circleCenter + Base6Directions.GetIntVector(Base6Directions.Direction.Down);

    circleCenter2 = behind;
    thridPoint = below;
}

private void OpenDoors()
{
    foreach (IMyDoor door in doorBlockList)
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
    foreach (IMyDoor door in doorBlockList)
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
    return block == null || block.Closed == true || !block.IsFunctional == true;
}

private static void ShootRailgun(IMySmallMissileLauncherReload railgun)
{
    railgun.Enabled = true;
    railgun.ShootOnce();
}

private bool AreBlocksMissingFromGroupErrorMessage<T>(List<T> list, string blockType)
{
    if (list?.Count == 0)
    {
        errorString = errorString + "\n" + $"No {blockType} block found in group";
        return true;
    }
    else
    {
        startString = startString + "\n" + $"{blockType} count: {list.Count}";
        return false;
    }
}

private bool AreBlocksMissingFromGroupWarningMessage<T>(List<T> list, string blockType)
{
    if (list?.Count == 0)
    {
        warningString = warningString + "\n" + $"No {blockType} block found in group";
        return true;
    }
    else
    {
        startString = startString + "\n" + $"{blockType} count: {list.Count}";
        return false;
    }
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
    Vector3D center1 = GetWorldPosition(circleCenter);
    Vector3D center2 = GetWorldPosition(circleCenter2);
    Vector3D point = GetWorldPosition(thridPoint);

    // Create the axis of rotation
    Vector3D rotationAxis = Vector3D.Normalize(center2 - center1);

    // Rotate the third point around the axis by X degrees to find the rotated plane
    Vector3D rotatedPoint = RotationHelper.RotateVector(point - center1, rotationAxis, rotationAngle + targetAngle + originPlaneAngleOffset) + center1;
    Vector3D rotatedPoint2 = RotationHelper.RotateVector(point - center1, rotationAxis, -rotationAngle + targetAngle + originPlaneAngleOffset) + center1;

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

public float ShootDelayOffsetAngle()
{
    float anglesPerSecond = 360 * RPM / 60;
    return -1 * anglesPerSecond * shootDelay;
}

}
enum GAUCommandEnum
{
    ON,
    OFF,
    STANDBY,
    READY,
    FIRE,
    CANCEL,
    RESET,
    CHARGE,
    CHARGING,
    OPENINGDOOR,
    CLOSINGDOOR
}

partial class RailgunChargeStateEnum
{
    public static string ALMOST = "";
    public static string CHARGED = "";
}

partial class RailgunChargeStateEnumLG
{
    public const string ALMOST = "Stored power: 4";
    public const string CHARGED = "Stored power: 500";
}

partial class RailgunChargeStateEnumSG
{
    public const string ALMOST = "Stored power: 1";
    public const string CHARGED = "Stored power: 16";