using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DavuxLib2.Net;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media.Imaging;
using System.IO;
using System.Net;

namespace Unity
{
    class UnityServer
    {
        public class DevInfo 
        {
            public int FrameCount = 0;
            public BitmapSource LastFrame = null;
            public int LastFrameTaken = 0;
        }

        public static UnityServer Inst = null;

        Dictionary<TSocket, DevInfo> DeviceInfo = new Dictionary<TSocket, DevInfo>();

        public UnityServer()
        {
            Port = 9001;
            Inst = this;

            sock.Connected += (sck) =>
            {
                sck.XmlDataArrived += new TSocket.DataHandler(sck_XmlDataArrived);

            };
            sock.Listen(Port);
        }

        List<CamServerCore.IMode> Modes = new List<CamServerCore.IMode>();

        void sck_XmlDataArrived(TSocket sk, TSocket.DataPacket xml)
        {
            Trace.WriteLine("Got packet " + xml.Type + " from " + sk);
            switch (xml.Type)
            {
                case "Unity.Packets.PTZ":
                    Trace.WriteLine("Got ptz request");
                    var req2 = xml.GetObject<Unity.Packets.PTZ>();
                    var reqdev2 = Broadcaster.Inst.Devices
                        .FirstOrDefault(d => d.UID + "@" +  Dns.GetHostName() == req2.UID);
                    if (reqdev2 != null)
                    {
                        var ptdev = reqdev2 as CamServerCore.IDeviceWithPanTilt;
                        if (ptdev != null)
                        {
                            if (req2.preset > -1)
                            {
                                if (req2.setPreset)
                                {
                                    ptdev.SetPreset(req2.preset);
                                }
                                else
                                {
                                    ptdev.JumpToPreset(req2.preset);
                                }
                            }
                            else
                            {
                                ptdev.Move(req2.Movement, req2.once);
                            }
                        }
                        else
                        {
                            Trace.WriteLine("invalid ptz dev");
                        }
                    }
                    break;
                case "Unity.Packets.ModeChange":
                    var r = xml.GetObject<Unity.Packets.ModeChange>();
                    var rdev = Broadcaster.Inst.Devices
                       .FirstOrDefault(d => d.UID + "@" + Dns.GetHostName() == r.UID);

                    if (rdev != null)
                    {
                        var mdev = rdev as CamServerCore.IDeviceWithModes;
                        if (mdev != null)
                        {
                            var mode = Modes.FirstOrDefault(m => m.Text == r.Mode.Text);
                            if (mode != null)
                            {
                                mdev.ToggleMode(mode);
                            }
                            else
                            {
                                Trace.WriteLine("Invalid mode");
                            }
                        }
                        else
                        {
                            Trace.WriteLine("invalid mode dev (no interface)");
                        }
                    }
                    else
                    {
                        Trace.WriteLine("invalid mode dev");
                    }

                    break;
                case "Unity.Packets.Request":
                    var req = xml.GetObject<Unity.Packets.Request>();
                    CamServerCore.IVideoDevice vdev = null;
                    var reqdev = Broadcaster.Inst.Devices
                        .FirstOrDefault(d => d.UID + "@" +  Dns.GetHostName() == req.UID);
                    if (reqdev != null)
                    {
                        switch (req.Type)
                        {
                            case Packets.Request.RequestType.StartVideo:
                                Trace.WriteLine("Requesting start video");
                                 vdev = reqdev as CamServerCore.IVideoDevice;
                                if (vdev != null)
                                {
                                    DeviceInfo.Add(sk, new DevInfo());

                                    vdev.OnFrame += img =>
                                    {
                                        DeviceInfo[sk].FrameCount++;
                                        DeviceInfo[sk].LastFrame = img;
                                        img.Freeze();

                                        if (DeviceInfo[sk].FrameCount == 1)
                                        {
                                            byte[] image = GetJpeg(img, 100);
                                            sk.Send("jpeg", image);
                                            Trace.WriteLine("Sent initial frame");
                                        }
                                    };
                                    vdev.StartVideo();
                                    Trace.WriteLine("Video started");
                                }
                                else
                                {
                                    var err = new Unity.Packets.Error 
                                    { Message = "Invalid video device: " + req.UID };
                                    sk.Send(err);
                                }

                                break;
                            case Packets.Request.RequestType.Frame:

                                if (DeviceInfo[sk].LastFrameTaken < DeviceInfo[sk].FrameCount)
                                {
                                    DeviceInfo[sk].LastFrameTaken = DeviceInfo[sk].FrameCount;
                                    byte[] image = GetJpeg(DeviceInfo[sk].LastFrame, 75);
                                    sk.Send("jpeg", image);
                                    Trace.WriteLine("Sent frame #" + DeviceInfo[sk].FrameCount);
                                }
                                else
                                {
                                    var fnr = new Packets.FrameNotReady();
                                    sk.Send(fnr);
                                    Trace.WriteLine("Sent noop");
                                }
                                break;
                            case Packets.Request.RequestType.StopVideo:
                                Trace.WriteLine("Requesting stop video");
                                 vdev = reqdev as CamServerCore.IVideoDevice;
                                 if (vdev != null)
                                 {
                                     vdev.StopVideo();
                                     Trace.WriteLine("Video stopped");
                                 }
                                 else
                                 {
                                     var err = new Unity.Packets.Error { Message = "Invalid video device: " + req.UID };
                                     sk.Send(err);
                                 }
                                break;
                            case Packets.Request.RequestType.Capabilities:
                                Trace.WriteLine("Caps request");
                                var cap = new Packets.Capabilities();

                                var ptzdev = reqdev as CamServerCore.IDeviceWithPanTilt;
                                if (ptzdev != null)
                                {
                                    cap.Caps = "ptz";
                                }

                                var mxdev = reqdev as CamServerCore.IDeviceWithModes;
                                if (mxdev != null)
                                {
                                    Trace.WriteLine("Adding modes...");
                                    foreach (var kv in mxdev.GetModes())
                                    {
                                        cap.Modes.Add(new Packets.UMode(kv.Key));
                                        Modes.Add(kv.Key);
                                        cap.ModeValues.Add(kv.Value);
                                    }

                                    mxdev.ModeStateChanged += (mode, value) =>
                                        {
                                            Trace.WriteLine("mode changed, sending notify");
                                            var mc = new Packets.ModeChange();
                                            mc.Mode = new Packets.UMode(mode);
                                            mc.value = value;
                                            sk.Send(mc);
                                        };
                                }

                                Trace.WriteLine("Caps response: " + cap.Caps);
                                sk.Send(cap);
                                break;
                            default:
                                Trace.WriteLine("Invalid unity request: " + req.Type);
                                break;
                        }
                    }
                    else
                    {
                        var err = new Unity.Packets.Error { Message = "Invalid device specified: " + req.UID };
                        sk.Send(err);
                    }
                    break;
                default:
                    Trace.WriteLine("Invalid UnityServer packet: " + xml.Type);
                    break;
            }
        }

        public int Port { get; private set; }

        TSocket sock = new TSocket();


        private byte[] GetJpeg(BitmapSource bitmapSource, int quality)
        {
            MemoryStream ms = new MemoryStream();
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
