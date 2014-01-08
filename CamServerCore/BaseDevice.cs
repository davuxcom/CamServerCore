using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CamServerCore
{
    public class BaseDevice : IDevice
    {
        public string UID { get; protected set; }
        public string Name { get; protected set; }

        public event Action<string> OnError;

        protected void ErrorArrived(string error)
        {
            OnError(error);
        }
    }

    public abstract class BaseVideoDevice : BaseDevice, IVideoDevice
    {
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

        protected abstract void StartVideoInternal();
        protected abstract void StopVideoInternal();

        protected void FrameArrived(System.Windows.Media.Imaging.BitmapSource frame)
        {
            OnFrame(FilterFrame(frame));
        }

        public event Action<System.Windows.Media.Imaging.BitmapSource> OnFrame;
        public event Func<System.Windows.Media.Imaging.BitmapSource, System.Windows.Media.Imaging.BitmapSource> FilterFrame;
    }
}
