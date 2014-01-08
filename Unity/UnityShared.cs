using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DavuxLib2.Net;
using System.Net;
using System.Diagnostics;
using System.Threading;

namespace Unity
{
    class UnityShared
    {
        public USocket UBinder { get; private set; }
        public USocket UBroadcaster { get; private set; }

        private static object Lock = new object();
        private static volatile UnityShared _Inst = null;
        public static UnityShared Inst
        {
            get
            {
                if (_Inst == null)
                {
                    lock (Lock)
                    {
                        return _Inst ?? (_Inst = new UnityShared());
                    }
                }
                return _Inst;
            }
        }

        public UnityShared()
        {
            new UnityServer();
            UBinder = new USocket(9000);
            UBinder.DataArrived += (sock, data) =>
                {
                    // data rcv from broadcast (request from other clients)
                    switch(data.Type)
                    {
                        case "Unity.Packets.Search":
                            Trace.WriteLine("Got Unity search request");
                            var search = data.Decode<Unity.Packets.Search>();

                            var sr = new Packets.SearchResponse();
                            sr.Devices.AddRange(Broadcaster.Inst.ProxyDevices);
                            sr.Port = UnityServer.Inst.Port;
                            Trace.WriteLine("Sent response with " + sr.Devices.Count + " devices");
                            sock.Send(sr);
                            break;
                        default:
                            Trace.WriteLine("Unknown Bind packet: " + data.Type);
                            break;
                    }
                };
            UBinder.Bind();

            UBroadcaster = new USocket(new IPEndPoint(IPAddress.Broadcast, 9000));
            UBroadcaster.DataArrived += (sock, data) =>
                {
                    // data rcv from response to a broadcast packet
                    // (response from other clients to self broadcast)

                    switch(data.Type)
                    {
                        case "Unity.Packets.SearchResponse":
                            var search = data.Decode<Unity.Packets.SearchResponse>();
                            Trace.WriteLine("Got response from " + sock.IPAddress + " with " + search.Devices.Count + " devices");
                            for (int i = 0; i < search.Devices.Count; i++)
                            {
                                
                                var dev = search.Devices[i];
                                dev.Port = search.Port;
                                dev.Host = sock.IPAddress.ToString();
                                Collector.Inst.OnDiscovered(dev);
                            }
                            break;
                        default:
                            Trace.WriteLine("Unknown Bcast packet: " + data.Type);
                            break;
                    }
                };
        }
    }
}
