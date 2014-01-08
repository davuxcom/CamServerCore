using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Net;
using DavuxLib2.Net;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace Unity
{
    public class DeviceProxy : CamServerCore.IDevice, 
        CamServerCore.IVideoDevice, CamServerCore.IDeviceWithPanTilt,
        CamServerCore.IDeviceWithModes
    {
        public string UID { get; set; }
        public string Name { get; set; }
        [XmlIgnore]
        public string Host { get; set; }
        [XmlIgnore]
        public int Port { get; set; }

        private string Capabilities = "";
        Dictionary<CamServerCore.IMode, bool> Modes = new Dictionary<CamServerCore.IMode, bool>();

        // when created via XML (sent from a client)
        public DeviceProxy() { }
        // when created via a device (client creating)
        public DeviceProxy(CamServerCore.IDevice dev)
        {
            UID = dev.UID + "@" + Dns.GetHostName();
            Name = "Unity: " + dev.Name;
        }

        public override string ToString()
        {
            return Name + ": " + UID + " via " + Host + ":" + Port;
        }

        public event Action<string> OnError;

        #region IVideoDevice Members

        TSocket sock = new TSocket();

        int RefCount = 0;
        public bool StartVideo()
        {
            RefCount++;
            if (RefCount == 1)
            {
                StartVideoInternal();
            }
            return true;
        }

        public bool StopVideo()
        {
            RefCount--;
            if (RefCount == 0)
            {
                StopVideoInternal();
            }
            return true;
        }

        protected void StartVideoInternal()
        {
            ConnectIfNeeded();
            var req = new Packets.Request();
            req.UID = UID;
            req.Type = Packets.Request.RequestType.StartVideo;
            sock.Send(req);
            Trace.WriteLine("Requested feed");
        }

        void ConnectIfNeeded()
        {
            if (sock.IsConnected) return;

            sock.BinaryDataArrived += new TSocket.RawHandler(sock_BinaryDataArrived);
            sock.XmlDataArrived += new TSocket.DataHandler(sock_XmlDataArrived);
            sock.Disconnected += (s) =>
            {
                if (OnError != null) OnError("Disconnected");
            };

        retry:
            Trace.WriteLine("Connecting to " + Host + ":" + Port);
            try
            {
                sock.Connect(Host, Port);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(Name + " Connection Failed: " + ex.Message);
                if (OnError != null) OnError(ex.Message);
                goto retry;
            }
        }

        void sock_XmlDataArrived(TSocket sock, TSocket.DataPacket data)
        {

            switch (data.Type)
            {
                case "Unity.Packets.Error":
                    var err = data.GetObject<Packets.Error>();
                    Trace.WriteLine("Error: " + err.Message);
                    OnError(err.Message);
                    break;
                case "Unity.Packets.FrameNotReady":
                    Trace.WriteLine("up to date, requesting again...");
                    var req = new Packets.Request();
                    req.Type = Packets.Request.RequestType.Frame;
                    req.UID = UID;
                    sock.Send(req);
                    break;
                case "Unity.Packets.Capabilities":
                    
                    var caps = data.GetObject<Packets.Capabilities>();
                    
                    Trace.WriteLine("Got caps: " + caps.Caps);

                    for (int i = 0; i < caps.Modes.Count; i++)
                    {
                        if (caps.Modes[i].Text == "-")
                        {
                            Modes.Add(new CamServerCore.SeparatorMode(), false);
                        }
                        else
                        {
                            Modes.Add(caps.Modes[i], caps.ModeValues[i]);
                        }
                    }
                    Capabilities = caps.Caps;
                    break;
                case "Unity.Packets.ModeChange":
                    var mode = data.GetObject<Packets.ModeChange>();
                    Trace.WriteLine("got mode state change");
                    if (ModeStateChanged != null)
                    {
                        ModeStateChanged(mode.Mode, mode.value);
                    }
                    break;
                default:
                    Trace.WriteLine("Got unhandled xml " + data.Type);
                    break;
            }
        }

        bool sock_BinaryDataArrived(TSocket sock, string type, byte[] data)
        {
            if (type == "jpeg")
            {
                // dispatch jpeg
                OnFrame(getBitmap(data));
                var req = new Packets.Request();
                req.Type = Packets.Request.RequestType.Frame;
                req.UID = UID;
                sock.Send(req);
                return true;
            }
            return false;
        }

        protected void StopVideoInternal()
        {
            var req = new Packets.Request();
            req.Type = Packets.Request.RequestType.StopVideo;
            req.UID = UID;
            sock.Send(req);
        }

        private BitmapSource getBitmap(byte[] jpeg)
        {
            JpegBitmapDecoder decoder = new JpegBitmapDecoder(
                new MemoryStream(jpeg), 
                BitmapCreateOptions.PreservePixelFormat, 
                BitmapCacheOption.Default);
            return decoder.Frames[0];
        }

        public event Action<System.Windows.Media.Imaging.BitmapSource> OnFrame;

        public event Func<System.Windows.Media.Imaging.BitmapSource, System.Windows.Media.Imaging.BitmapSource> FilterFrame;

        #endregion

        #region IDeviceWithPanTilt Members

        public bool PTSupported
        {
            get
            {
                GetCaps();
                return Capabilities.IndexOf("ptz") > -1;
            }
        }

        private void GetCaps()
        {
            ConnectIfNeeded();
            Trace.WriteLine("checking caps..");
            if (Capabilities == "")
            {
                Trace.WriteLine("requesting caps...");
                var req = new Packets.Request();
                req.Type = Packets.Request.RequestType.Capabilities;
                req.UID = UID;
                sock.Send(req);

                int c = 0;
                while (Capabilities == "")
                {
                    Trace.WriteLine("Waiting for caps...");
                    Thread.Sleep(100);
                    c++;
                    if (c > 20)
                    {
                        Capabilities = "unk";
                        return;
                    }
                }
            }
        }

        public bool Move(CamServerCore.DevicePT pt, bool once = false)
        {
            Trace.WriteLine("Requesting ptz...");
            var req = new Packets.PTZ();
            req.Movement = pt;
            req.once = once;
            req.UID = UID;
            sock.Send(req);
            return true;
        }

        public bool SetPreset(int preset)
        {
            Trace.WriteLine("Requesting ptz preset set...");
            var req = new Packets.PTZ();
            req.preset = preset;
            req.setPreset = true;
            req.UID = UID;
            sock.Send(req);
            return true;
        }

        public bool JumpToPreset(int preset)
        {
            Trace.WriteLine("Requesting preset jump...");
            var req = new Packets.PTZ();
            req.preset = preset;
            req.setPreset = false;
            req.UID = UID;
            sock.Send(req);
            return true;
        }

        public int GetMaxPresetNum()
        {
            var m = Regex.Match(Capabilities, @"preset_max=[(.*)]");
            if (m.Success)
            {
                return int.Parse(m.Groups[1].Value);
            }
            Trace.Write("Cap preset_max=[(.*)] not found: " + Capabilities);
            return -1;
        }

        #endregion

        #region IDeviceWithModes Members

        public Dictionary<CamServerCore.IMode, bool> GetModes()
        {
            GetCaps();
            return Modes;
        }

        public bool ToggleMode(CamServerCore.IMode mode)
        {
            Trace.WriteLine("Toggle mode...");
            var req = new Packets.ModeChange();
            req.UID = UID;
            req.Mode = new Packets.UMode(mode);
            sock.Send(req);
            return true;
        }

        public event Action<CamServerCore.IMode, bool> ModeStateChanged;

        #endregion
    }
}
