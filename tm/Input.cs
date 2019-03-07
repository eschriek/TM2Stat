using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DirectX.DirectInput;

namespace tm
{
    class Input
    {
        public static string[] FindJoysticks()
        {
            string[] systemJoysticks = null;

            try
            {
                // Find all the GameControl devices that are attached.
                DeviceList gameControllerList = Manager.GetDevices(DeviceClass.GameControl, EnumDevicesFlags.AttachedOnly);

                // check that we have at least one device.
                if (gameControllerList.Count > 0)
                {
                    systemJoysticks = new string[gameControllerList.Count];
                    int i = 0;
                    // loop through the devices.
                    foreach (DeviceInstance deviceInstance in gameControllerList)
                    {
                        // create a device from this controller so we can retrieve info.
                        using (var joystickDevice = new Device(deviceInstance.InstanceGuid))
                        {
                            systemJoysticks[i] = joystickDevice.DeviceInformation.InstanceName;
                        }

                        i++;
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }

            return systemJoysticks;
        }
    }
}
