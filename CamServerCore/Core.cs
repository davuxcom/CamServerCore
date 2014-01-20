using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DavuxLib2;
using System.Diagnostics;

namespace CamServerCore
{
    public class Core
    {
        public static Core Inst = null;
        public Core()
        {
            // todo: this is a bad/wrong singleton pattern, but thread risk is very low.
            if (Inst != null) throw new InvalidOperationException("Only one core");
            Inst = this;
        }

        public event Action<IDevice, IDeviceBus> DeviceAdded = delegate { };

        ObservableCollectionEx<IDeviceBus> Buses = new ObservableCollectionEx<IDeviceBus>();
        ObservableCollectionEx<IDevice> Devices = new ObservableCollectionEx<IDevice>();
        ObservableCollectionEx<ILoader> Loaders = new ObservableCollectionEx<ILoader>();

        public void Shutdown()
        {
            TryDispose(Devices);
            TryDispose(Buses);
            TryDispose(Loaders);

            Settings.Save();
        }

        void TryDispose(IEnumerable<object> list)
        {
            foreach (var d in list)
            {
                var id = d as IDisposable;
                if (id != null) id.Dispose();
            }
        }

        public void Init()
        {
            Settings.Get("WebServer_UserName", "dave");
            Settings.Get("WebServer_Password", "dave");

            // Load up on-demand plugins, like WebServer
            Loaders.AddRange(Plugin.Load<ILoader>().ToArray());
            foreach (var loader in Loaders) loader.Load();

            // Load up device discovery buses.  (DirectShow, IPCam-Foscam, Unity)
            Buses.AddRange(Plugin.Load<IDeviceBus>().ToArray());
            foreach (var b in Buses)
            {
                Trace.WriteLine("Searching bus: " + b.Name);
                b.Discovered += (dev, bus) =>
                    {
                        Trace.WriteLine(string.Format("Core Found: [{0}:{1}]", bus, dev));
                        var vdev = dev as IVideoDevice;
                        if (vdev != null)
                        {
                            // TODO: add timestamp, etc.
                            vdev.FilterFrame += (f) => { return f; };
                        }

                        Devices.Add(dev);
                        DeviceAdded(dev, bus);
                    };
                b.Discover();

                IDeviceBusAltDiscovery altBus = b as IDeviceBusAltDiscovery;
                if (altBus != null)
                {
                    if (b.Name == "Foscam")
                    {
                        altBus.Create(new Dictionary<string, string> { 
                            { "Host", "davux2.mooo.com:89" },
                            { "Display Name", "Dave-Seattle" },
                            { "Camera Version", "FI8918W" },
                        });
                        altBus.Create(new Dictionary<string, string> { 
                            { "Host", "davux.mooo.com:89" },
                            { "Display Name", "Dave-Home" },
                            { "Camera Version", "FI8918W" },
                        });
                    }
                    else if (b.Name == "Mjpeg")
                    {
                        altBus.Create(new Dictionary<string, string> { 
                            { "Url", "http://192.168.0.133:8080/videofeed" },
                            { "Display Name", "Wall Slate" },
                        });

                    }
                    else if (b.Name == "JpegUdp")
                    {
                        altBus.Create(new Dictionary<string, string> { 
                            { "Rotate", "90" },
                            { "Port", "11000" },
                            { "Display Name", "UDP Broadcast Video" },
                        });
                    }
                }
            }
        }
    }
}
