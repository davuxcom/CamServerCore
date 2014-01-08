using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Video.DirectShow;
using CamServerCore;
using DavuxLib2.Platform;

namespace DirectShowBus
{
    class Device : BaseVideoDevice, IDisposable,
        IDeviceWithModes, IDeviceWithConfigurationDialog, IDeviceWithPanTilt
    {
        VideoCaptureDevice dev = null;
        Dictionary<IMode, bool> Modes = new Dictionary<IMode, bool>();
        Dictionary<string, string> Settings = null;

        public Device(string Name, string MonikerName)
        {
            this.Name = Name;
            this.UID = MonikerName;
            Settings = Config.GetSettingsForDevice(UID);
        }

        public override string ToString()
        {
            return Name + " " + UID;
        }

        public event Action<IMode, bool> ModeStateChanged = delegate { };
        public Dictionary<IMode, bool> GetModes()
        {
            InitVideoDevice();
            return Modes;
        }

        public bool ToggleMode(IMode mode)
        {
            Mode newMode = (Mode)Modes.Keys.First(m => m.Text == mode.Text);
            dev.DesiredFrameRate = newMode.Capabilities.MaxFrameRate;
            dev.DesiredFrameSize = newMode.Capabilities.FrameSize;

            if (dev.IsRunning)
            {
                new Thread(() =>
                {
                    dev.SignalToStop();
                    dev.WaitForStop();
                    dev.Start();
                }).Start();
            }

            foreach (var m in Modes.Keys)
            {
                // notify that all modes are off, except the new one
                ModeStateChanged(m, m == newMode);
            }
            Settings["SelectedMode"] = mode.Text;
            return true;
        }

        protected override void StartVideoInternal()
        {
            InitVideoDevice();
            dev.Start();
        }

        protected override void StopVideoInternal()
        {
            new Thread(() => dev.SignalToStop()).Start();
        }

        private void InitVideoDevice()
        {
            if (dev == null)
            {
                dev = new VideoCaptureDevice(UID); // moniker
                if (dev.VideoCapabilities != null && dev.VideoCapabilities.Length > 0)
                {
                    foreach (VideoCapabilities c in dev.VideoCapabilities)
                    {
                        Modes.Add(new Mode(c), false);
                    }

                    Mode SelectedMode = null;

                    if (Settings.Keys.Contains("SelectedMode"))
                    {
                        SelectedMode = (Mode)Modes.Keys.First(m => m.Text == Settings["SelectedMode"]);
                    }

                    if (SelectedMode == null)
                    {
                        Trace.WriteLine("Setting Default Mode: " + (Mode)Modes.ElementAt(Modes.Count - 1).Key);
                        SelectedMode = (Mode)Modes.ElementAt(Modes.Count - 1).Key;
                    }

                    new Thread(() =>
                        {
                            Thread.Sleep(200);
                            ModeStateChanged(SelectedMode, true);
                        }).Start();

                    dev.DesiredFrameRate = SelectedMode.Capabilities.MaxFrameRate;
                    dev.DesiredFrameSize = SelectedMode.Capabilities.FrameSize;
                }
                else
                {
                    Trace.WriteLine("Setting fallback mode");
                    dev.DesiredFrameRate = 30;
                    dev.DesiredFrameSize = new System.Drawing.Size(800, 600);
                }

                dev.NewFrame += (_, eventArgs) =>
                    {
                        using (var ptr = new GDIPtr(eventArgs.Frame.GetHbitmap()))
                        {
                            BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            ptr.Ptr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                            FrameArrived(source);
                        }
                    };
                dev.PlayingFinished += (_, __) => { };
                dev.VideoSourceError += (_, ex) => { ErrorArrived(ex.Description); };
            }
        }

        public bool ShowConfiguration(IntPtr hWnd)
        {
            dev.DisplayPropertyPage(hWnd);
            return true;
        }

        public void Dispose()
        {
            Config.SaveSettingsForDevice(this.UID, Settings);
        }

 
        int ptz_step = 5;

        public bool Move(DevicePT pt, bool once = false)
        {
            if (Name.IndexOf("QuickCam") > -1)
            {
                switch (pt)
                {
                    case DevicePT.Down:
                        PtzProcess("0 -" + ptz_step);
                        break;
                    case DevicePT.Up:
                        PtzProcess("0 " + ptz_step);
                        break;
                    case DevicePT.Right:
                        PtzProcess("" + ptz_step + " 0");
                        break;
                    case DevicePT.Left:
                        PtzProcess("-" + ptz_step + " 0");
                        break;
                    default:
                        break;
                }
            }
            return true;
        }

        void PtzProcess(string arg)
        {
            System.Diagnostics.ProcessStartInfo info = new ProcessStartInfo();
            info.Arguments = arg;
            info.FileName = "ptz.exe";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            System.Diagnostics.Process.Start(info);
        }

        public bool PTSupported
        {
            // TODO ...
            get { return Name.IndexOf("QuickCam") > -1; }
        }

        public bool SetPreset(int preset)
        {
            return false;
        }

        public bool JumpToPreset(int preset)
        {
            return false;
        }

        public int GetMaxPresetNum()
        {
            return 0;
        }
    }
}
