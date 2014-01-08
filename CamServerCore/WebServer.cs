using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Media.Imaging;
using DavuxLib2;
using DavuxLib2.Extensions;
using DavuxLib2.HTTP;

namespace CamServerCore
{
    class WebServer : ILoader
    {
        class DeviceWrapper
        {
            public string Hash { get; private set; }
            public bool IsActive { get; private set; }
            public string Name { get { return dev.Name; } }

            public string LastError { get; private set; }
            public BitmapSource LastFrame { get; private set; }
            public int Timeout { get; set; }

            IDevice dev = null;
            public IVideoDevice vdev = null;
            public IDeviceWithModes mdev = null;
            public IDeviceWithSliderModes sdev = null;
            public IDeviceWithPanTilt ptdev = null;

            public DeviceWrapper(IDevice dev)
            {
                this.dev = dev;
                Hash = dev.UID.GetMD5Hash();

                vdev = dev as IVideoDevice;
                if (vdev != null)
                {
                    vdev.OnFrame += frame => 
                        {
                            frame.Freeze();
                            LastFrame = frame;
                        };
                    dev.OnError += error => LastError = error;
                }
                mdev = dev as IDeviceWithModes;
                sdev = dev as IDeviceWithSliderModes;
                ptdev = dev as IDeviceWithPanTilt;
            }

            public void Activate()
            {
                if (IsActive) return;
                IsActive = true;
      
                Trace.WriteLine("Web Activate: " + dev.Name);
                vdev.StartVideo();
                Timeout = 0;
            }

            public void DeActivate()
            {
                if (!IsActive) return;
                IsActive = false;
                Trace.WriteLine("Web Deactivate: " + dev.Name);
                vdev.StopVideo();
                Timeout = 0;
            }
        }

        List<DeviceWrapper> Devices = new List<DeviceWrapper>();
        Server server = null;

        public WebServer()
        {
            Core.Inst.DeviceAdded += (dev, bus) => Devices.Add(new DeviceWrapper(dev));

            server = new Server(Settings.Get("HTTPPort", 8008));
            server.OnRequest += new Server.RequestHandler(server_OnRequest);
            try
            {
                server.Start();
            }
            catch (SocketException ex)
            {
                Trace.Write("[Webserver: " + ex.Message + "]");
            }

            // Watchdog
            new Thread(() => {
                while (true)
                {
                    try
                    {
                        foreach (WebServer.DeviceWrapper dev in Devices)
                        {
                            if (dev.IsActive) dev.Timeout++;

                            if (dev.Timeout > Settings.Get("HTTPTimeout", 6))
                                dev.DeActivate();
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("HTTP/Error deactivating camera: " + ex);
                    }
                    Thread.Sleep(1000 * 10);
                }
            }).Start();
        }

        void server_OnRequest(Request req, Server server)
        {
            try
            {
                req.Response.MimeType = "text/html";
                req.Response.Code = Server.StatusCodes.OK;

                if (req.Headers.URL.StartsWith("/video/") && req.Headers.URL.Length > 7)
                {
                    string device = req.Headers.URL.Substring(7);
                    foreach (DeviceWrapper dv in Devices)
                    {
                        if (dv.Hash == device)
                        {
                            if (req.Headers.AuthenticatedUser == Settings.Get("WebServer_UserName", "dave") &&
                                req.Headers.AuthenticatedPassword == Settings.Get("WebServer_Password", "dave"))
                            {
                                dv.Timeout = 0;
                                if (!dv.IsActive) dv.Activate();

                                int quality = 100;
                                if (req.Headers.QueryString.ContainsKey("q"))
                                {
                                    quality = int.Parse(req.Headers.QueryString["q"]);
                                }

                                if (dv.LastFrame != null)
                                {
                                    req.Response.Body = GetJpeg(dv.LastFrame, quality);
                                    req.Response.MimeType = "image/jpeg";
                                }
                            }
                            else
                            {
                                req.Response.Code = Server.StatusCodes.UNAUTHORIZED;
                                req.Response.AddHeader("WWW-Authenticate", "Basic realm=\"CamServer3\"");
                                req.Response.BodyString = "Authentication is required for this resource. (Device-Level Auth Failure)";
                            }
                            return;
                        }
                    }
                }

                if (Settings.Get("WebServer_UserName", "") != "" &&
                    Settings.Get("WebServer_Password", "") != "")
                {
                    if (req.Headers.AuthenticatedUser != Settings.Get("WebServer_UserName", "") ||
                        req.Headers.AuthenticatedPassword != Settings.Get("WebServer_Password", ""))
                    {
                        req.Response.Code = Server.StatusCodes.UNAUTHORIZED;
                        req.Response.AddHeader("WWW-Authenticate", "Basic realm=\"CamServer3\"");
                        req.Response.BodyString = "Authentication is required for this resource. (Server-Level Auth Failure)";
                        return;
                    }
                }

                if (req.Headers.URL.StartsWith("/ctl/") && req.Headers.URL.Length > 5)
                {
                    string device = req.Headers.URL.Substring(7);
                    foreach (DeviceWrapper dv in Devices)
                    {
                        if (dv.Hash == device)
                        {
                            // PTZ, I/O, Sliders, Modes
                            if (req.Headers.QueryString.ContainsKey("ptz"))
                            {
                                DevicePT pt = (DevicePT) int.Parse(req.Headers.QueryString["ptz"]);
                                if (dv.ptdev != null)
                                {
                                    dv.ptdev.Move(pt);
                                }
                            }
                            return;
                        }
                    }
                }

                if (req.Headers.URL == "/list")
                {
                    string list = "";
                    foreach (DeviceWrapper dv in Devices)
                    {
                        list += dv.Name + "\r\n" + dv.Hash + "\r\n";
                    }
                    req.Response.BodyString = list;
                    return;
                }

                string resource = req.Headers.URL.Replace('/', System.IO.Path.DirectorySeparatorChar);
                resource = resource.Replace('\\', System.IO.Path.DirectorySeparatorChar);
                resource = resource.Replace("..", "");
                resource = resource.Replace(":", "");

                if (resource.Trim() == System.IO.Path.DirectorySeparatorChar.ToString())
                {
                    resource = "default.htm";
                }

                if (File.Exists("html" + System.IO.Path.DirectorySeparatorChar + resource))
                {
                    req.Response.Body = File.ReadAllBytes("html" + System.IO.Path.DirectorySeparatorChar + resource);
                    req.Response.MimeType = MimeType.DetermineFromFile("html" + System.IO.Path.DirectorySeparatorChar + resource);
                    return;
                }

                req.Response.Code = Server.StatusCodes.NOT_FOUND;
                req.Response.BodyString = "404: Resource Not Found";
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Fatal HTTP Error: " + ex);
            }
        }

        private byte[] GetJpeg(BitmapSource bitmapSource, int quality)
        {
            MemoryStream ms = new MemoryStream();
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(ms);
            return ms.ToArray();
        }

        public void Load()
        {
            // NOTE:  We don't load up anything here since ILoader() was already called, and our ctor
            // did all the legwork.
           // new WebServer();
        }

    }
}
