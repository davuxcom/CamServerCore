using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DavuxLib2.Extensions;
using DavuxLib2;

namespace CamServerCore
{
    public class Config
    {
        public static Dictionary<string, string> GetSettingsForDevice(string UID)
        {
            return Settings.Get(UID, new Dictionary<string, string>());
        }

        public static void SaveSettingsForDevice(string UID, Dictionary<string, string> Settings)
        {
            DavuxLib2.Settings.Set(UID, Settings);
            DavuxLib2.Settings.Save();
        }
    }
}
