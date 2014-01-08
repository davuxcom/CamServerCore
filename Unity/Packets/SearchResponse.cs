using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Packets
{
    public class SearchResponse
    {
        public List<DeviceProxy> Devices = new List<DeviceProxy>();
        public int Port = 0;
    }
}
