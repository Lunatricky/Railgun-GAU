using Sandbox.ModAPI.Ingame;

namespace IngameScript.Domain
{
    partial class GauGeo
    {
        private bool IsDoorOpen
        {
            get
            {
                bool isDoorOpen = true;
                foreach (IMyDoor door in DoorBlockList)
                {
                    if (door.Enabled == true)
                    {
                        isDoorOpen = false;
                    }
                }
                return isDoorOpen;
            }
        }

        private bool IsDoorAlmostOpen
        {
            get
            {
                bool IsDoorAlmostOpen = false;
                foreach (IMyDoor door in DoorBlockList)
                {
                    float almostOpenRatio = isLG ? _doorOpenRatio - 0.3f : _doorOpenRatio - 0.1f;
                    if (almostOpenRatio < door.OpenRatio)
                    {
                        IsDoorAlmostOpen = true;
                    }
                }
                return IsDoorAlmostOpen;
            }
        }

        private void OpenDoors()
        {
            foreach (IMyDoor door in DoorBlockList)
            {
                door.OpenDoor();

                if (_doorOpenRatio < door.OpenRatio)
                {
                    door.Enabled = false;
                }
            }
        }

        private void CloseDoors()
        {
            foreach (IMyDoor door in DoorBlockList)
            {
                door.Enabled = true;
                door.CloseDoor();
            }
        }
    }
}
