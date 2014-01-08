using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Packets
{
    public class Error
    {
        public string Message { get; set; }
        public string Description { get; set; }
        public Error(Exception ex)
        {
            Message = ex.Message;
            Description = ex.ToString();
        }
        public Error() { }
    }
}
