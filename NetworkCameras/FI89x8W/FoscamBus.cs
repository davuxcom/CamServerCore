using System;
using System.Collections.Generic;

namespace NetworkCameras.FI89x8W
{
    public class FoscamBus : CamServerCore.IDeviceBus, CamServerCore.IDeviceBusAltDiscovery
    {
        public bool Discover()
        {
            return false;
        }

        public event Action<CamServerCore.IDevice, CamServerCore.IDeviceBus> Discovered;

        public string Name
        {
            get { return "Foscam"; }
        }

        public bool Create(Dictionary<string, string> Properties)
        {
            if (Properties.ContainsKey("Host") && Properties.ContainsKey("Display Name")
                && Properties.ContainsKey("Camera Version"))
            {
                Discovered(new Device(Properties["Display Name"], 
                    Properties["Camera Version"], Properties["Host"]), this);
            }
            else
            {
                throw new ArgumentException("All properties are required to create a new camera");
            }
            return true;
        }

        public Dictionary<string, string> Properties
        {
            get
            {
                Dictionary<string, string> dict = new Dictionary<string, string>();
                dict.Add("Host", "string");
                dict.Add("Display Name", "string");
                dict.Add("Camera Version", "{FI8908W, FI8918W}");
                return dict;
            }
        }
    }
}
