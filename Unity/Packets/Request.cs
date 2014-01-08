using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Packets
{
    public class Request
    {
        public enum RequestType 
        {
            StartVideo, StopVideo, Frame, Capabilities
        };

        public string UID { get; set; }

        public RequestType Type { get; set; }
        
        // username, password...
    }
}
