using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AForge.Video;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Net;
using System.Diagnostics;
using CamServerCore;
using DavuxLib2.Platform;
using System.Threading;
using System.Net.Sockets;
using System.IO;

namespace NetworkCameras.JpegUdp
{
    class JpegUdpDevice : BaseVideoDevice, IDisposable
    {
        int Port = 0;
        public JpegUdpDevice(string DisplayName, string Port)
        {
            this.Port = int.Parse(Port);
            UID = "MJpeg-" + Port;
            Name = string.Format("{0} ({1})", DisplayName, Port);
        }

        Thread worker = null;

        protected override void StartVideoInternal()
        {
            worker = new Thread(() =>
            {
                var u = new UdpClient(new IPEndPoint(IPAddress.Any, Port));

                while (worker == Thread.CurrentThread)
                {
                    IPEndPoint ip = null;
                    var bytes = u.Receive(ref ip);
                    Console.WriteLine(bytes.Length);

                    JpegBitmapDecoder decoder = null;
                    BitmapSource bitmapSource = null;
                    using (var stream = new MemoryStream(bytes))
                    {
                        decoder = new JpegBitmapDecoder(stream,
                                                        BitmapCreateOptions.PreservePixelFormat,
                                                        BitmapCacheOption.OnLoad);
                    }
                    bitmapSource = decoder.Frames[0];
                    bitmapSource.Freeze();


                    TransformedBitmap myRotatedBitmapSource = new TransformedBitmap();
                    myRotatedBitmapSource.BeginInit();
                    myRotatedBitmapSource.Source = bitmapSource;
                    myRotatedBitmapSource.Transform = new RotateTransform(90);
                    myRotatedBitmapSource.EndInit();

                    this.FrameArrived(myRotatedBitmapSource);
                }
            });
            worker.Start();
        }

        protected override void StopVideoInternal()
        {
            worker = null;
        }

        public void Dispose()
        {
        }
    }
}



