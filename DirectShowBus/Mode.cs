using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AForge.Video.DirectShow;

namespace DirectShowBus
{
    class Mode : CamServerCore.IMode
    {
        internal VideoCapabilities Capabilities = null;

        public Mode(VideoCapabilities Capabilities)
        {
            this.Capabilities = Capabilities;
        }

        public string Text
        {
            get
            {
                return String.Format("{0}x{1} @ {2}fps", 
                    Capabilities.FrameSize.Width, Capabilities.FrameSize.Height,
                    Capabilities.MaxFrameRate);
            }
        }
    }
}
