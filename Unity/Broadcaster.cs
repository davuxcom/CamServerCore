using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CamServerCore;
using System.Diagnostics;
using DavuxLib2.Net;
using System.Net;

namespace Unity
{
    public class Broadcaster : ILoader
    {
        public static Broadcaster Inst = null;

        public List<IDevice> Devices = new List<IDevice>();
        public List<DeviceProxy> ProxyDevices = new List<DeviceProxy>();

        public void Load(){ }

        public Broadcaster()
        {
            Inst = this;
            Core.Inst.DeviceAdded += (dev, bus) =>
                {
                    if (dev.Name.StartsWith("Unity:")) return;
                    Devices.Add(dev);
                    ProxyDevices.Add(new DeviceProxy(dev));
                };
        }
    }
}
