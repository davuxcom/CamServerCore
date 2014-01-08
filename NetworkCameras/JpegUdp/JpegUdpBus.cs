using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkCameras.JpegUdp
{
    public class JpegUdpBus : CamServerCore.IDeviceBus, CamServerCore.IDeviceBusAltDiscovery
    {
        public bool Discover()
        {
            return false;
        }

        public event Action<CamServerCore.IDevice, CamServerCore.IDeviceBus> Discovered;

        public string Name
        {
            get { return "JpegUdp"; }
        }

        public bool Create(Dictionary<string, string> Properties)
        {
            if (Properties.ContainsKey("Port") && Properties.ContainsKey("Display Name"))
            {
                Discovered(new JpegUdpDevice(Properties["Display Name"], 
                    Properties["Port"]), this);
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
                dict.Add("Port", "integer");
                dict.Add("Display Name", "string");
                return dict;
            }
        }
    }

}