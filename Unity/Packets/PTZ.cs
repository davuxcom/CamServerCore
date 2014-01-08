using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Packets
{
    public class PTZ
    {
        public int preset = -1;
        public CamServerCore.DevicePT Movement = CamServerCore.DevicePT.Center;
        public bool once = false;
        public string UID = "";

        public bool setPreset { get; set; }
    }
}
