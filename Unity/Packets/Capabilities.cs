using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Packets
{
    public class Capabilities
    {
        public string Caps = "";
        public List<UMode> Modes = new List<UMode>();
        public List<bool> ModeValues = new List<bool>();
    }

    public class UMode : CamServerCore.IMode
    {
        public string Text { get; set; }
        public UMode(CamServerCore.IMode mode)
        {
            Text = mode.Text;
        }
        public UMode() { }
    }
}
