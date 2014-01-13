using CamServerCore;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NetworkCameras.JpegUdp
{
    class JpegUdpDevice : BaseVideoDevice, IDisposable
    {
        int Port = 0;
        Thread worker = null;
        UdpClient udpClient = null;
        public JpegUdpDevice(string DisplayName, string Port)
        {
            this.Port = int.Parse(Port);
            UID = "MJpeg-Udp-" + Port;
            Name = string.Format("{0} (Port {1})", DisplayName, Port);
        }
        protected override void StartVideoInternal()
        {
            worker = new Thread(() => {
                try
                {
                    udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
                    while (worker == Thread.CurrentThread)
                    {
                        IPEndPoint ip = null;
                        var bytes = udpClient.Receive(ref ip);
                        // Console.WriteLine(bytes.Length);
                        using (var stream = new MemoryStream(bytes))
                        {
                            var decoder = new JpegBitmapDecoder(stream,
                                                            BitmapCreateOptions.PreservePixelFormat,
                                                            BitmapCacheOption.OnLoad);
                            var bitmapSource = decoder.Frames[0];
                            bitmapSource.Freeze();

                            var rotatedSource = new TransformedBitmap();
                            rotatedSource.BeginInit();
                            rotatedSource.Source = bitmapSource;
                            rotatedSource.Transform = new RotateTransform(90);
                            rotatedSource.EndInit();

                            this.FrameArrived(rotatedSource);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Worker crash: " + ex.Message);
                    StopVideoInternal();
                    StartVideoInternal();
                    return;
                }

                StopVideoInternal();
            });
            worker.Start();
        }
        protected override void StopVideoInternal()
        {
            worker = null;
            if (udpClient != null)
            {
                try
                {
                    udpClient.Close();
                }
                catch (SocketException) { }
            }
        }
        public void Dispose()
        {
        }
    }
}



