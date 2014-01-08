using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CamServerCore;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace Unity
{
    public class Collector : IDeviceBus
    {
        public static Collector Inst = null;
        public Collector()
        {
            Inst = this;
        }

        public List<IDevice> Devices = new List<IDevice>();

        public string Name { get { return "Unity"; } }

        public event Action<IDevice, IDeviceBus> Discovered;
        public bool Discover()
        {
            new Thread(() =>
                {
                    while (true)
                    {
                        Search();
                        Thread.Sleep(1000 * 10);    // 10s
                    }
                }).Start();
            return true;
        }

        public void Search()
        {
            Trace.WriteLine("Searching network for Unity clients");

            var search = new Unity.Packets.Search();
            search.FromHost = Dns.GetHostName();

            UnityShared.Inst.UBroadcaster.Send(search);
            // search for network devices
        }

        internal void OnDiscovered(IDevice dev)
        {
            if (Devices.ToArray().FirstOrDefault(d => d.UID == dev.UID) != null) return;
            Devices.Add(dev);
            if (Discovered != null)
            {
                Discovered(dev, this);
            }
        }
    }
}
