using CamServerCore;
using DavuxLib2.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NetworkCameras.Mjpeg
{
    class MjpegBasicDevice : BaseVideoDevice, IDisposable
    {
        MjpegProcessor.MjpegDecoder stream = new MjpegProcessor.MjpegDecoder();
        string Url;
        public MjpegBasicDevice(string DisplayName, string Url)
        {
            UID = Url;
            Name = string.Format("{0} ({1})", DisplayName, Url);
            this.Url = Url;
        }

        public void Dispose()
        {
        }

        protected override void StartVideoInternal()
        {
            if (stream != null) stream.StopStream();
            stream = new MjpegProcessor.MjpegDecoder();

            stream.OnError += err =>
            {
                ErrorArrived(err.Message);
                Trace.WriteLine("[KICK] Restarting video...");
                StopVideoInternal();
                StartVideoInternal();
            };

            stream.ParseStream(new Uri(Url));
            stream.FrameReady += (_, e) =>
            {
                using (var ptr = new GDIPtr(e.Bitmap.GetHbitmap()))
                {
                    FrameArrived(System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr.Ptr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
                }
            };
        }

        protected override void StopVideoInternal()
        {
            stream.StopStream();
        }
    }
}
