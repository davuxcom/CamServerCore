using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AForge.Video;
using AForge.Video.DirectShow;

namespace DirectShowBus
{
    public class Bus : CamServerCore.IDeviceBus
    {
        public event Action<CamServerCore.IDevice, CamServerCore.IDeviceBus> Discovered;
        
        public string Name { get { return "DirectShow"; } }

        public bool Discover()
        {
            foreach (FilterInfo fi in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                Discovered(new Device(fi.Name, fi.MonikerString), this);
            }
            return true;
        }
    }
}
