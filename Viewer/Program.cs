using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CamServer3Viewer
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            //Viewer.App app = new Viewer.App();

            DavuxLib2.App.Init("CamServer3");
            if (DavuxLib2.App.IsAppAlreadyRunning() || !DavuxLib2.App.CheckEULA(DavuxLib2.LicensingMode.Free))
            {
                Environment.Exit(0);
            }

            DavuxLib2.App.SubmitCrashReports();

            new CamServerCore.Core();
            new NotificationAreaIcon();

            CamServerCore.Core.Inst.Init();
        }
    }
}
