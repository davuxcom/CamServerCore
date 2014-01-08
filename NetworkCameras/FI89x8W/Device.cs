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

namespace NetworkCameras.FI89x8W
{
    class Device : BaseVideoDevice, IDeviceWithAuthentication, IDeviceWithModes, 
        IDeviceWithPanTilt, IDeviceWithSliderModes, IDisposable
    {
        public enum Version
        {
            Unknown, FI8908W, FI8918W,
        }

        Dictionary<string, string> Settings = null;
        Dictionary<IMode, bool> Modes = new Dictionary<IMode, bool>();
        Dictionary<ISlider, int> SliderModes = new Dictionary<ISlider, int>();

        Controller currentController = null;
        Version camVersion = Version.Unknown;
        MjpegProcessor.MjpegDecoder stream = new MjpegProcessor.MjpegDecoder();

        public event Func<string, KeyValuePair<string, string>> OnCredentialsRequired;

        public Device(string DisplayName, string camVersion, string IPAddress)
        {
            UID = IPAddress;
            Name = string.Format("{0} ({1}: {2})", DisplayName, camVersion, IPAddress);
            this.camVersion = (Version)Enum.Parse(typeof(Version), camVersion);

            Settings = Config.GetSettingsForDevice(UID);

            Modes.Add(new Mode("QVGA (320x240) @ 30fps", 
                c => c.Resolution = Controller.DisplayResolution.QVGA), false);
            Modes.Add(new Mode("VGA (640x480) @ 15fps",
                c => c.Resolution = Controller.DisplayResolution.VGA), false);

            Modes.Add(new CamServerCore.SeparatorMode(), false);

            if (this.camVersion == Version.FI8918W)
            {
                Modes.Add(new Mode("IR LEDs On",
                    c => c.PTZ(Controller.PTZAction.IO_Low)), false);
                Modes.Add(new Mode("IR LEDs Off",
                    c => c.PTZ(Controller.PTZAction.IO_High)), false);
            }
            else
            {
                Modes.Add(new Mode("I/O Relay -> Low",
                    c => c.PTZ(Controller.PTZAction.IO_Low)), false);
                Modes.Add(new Mode("I/O Relay -> High",
                    c => c.PTZ(Controller.PTZAction.IO_High)), false);
            }

            Modes.Add(new CamServerCore.SeparatorMode(), false);

            Modes.Add(new Mode("Patrol Horizon",
                c => c.PTZ(Controller.PTZAction.Patrol_Horizon)), false);
            Modes.Add(new Mode("[Stop] Patrol Horizon",
                c => c.PTZ(Controller.PTZAction.Patrol_Horizon_Stop)), false);

            Modes.Add(new CamServerCore.SeparatorMode(), false);

            Modes.Add(new Mode("50Hz",
                c => c.ImageMode = Controller.DisplayMode.HZ_50), false);
            Modes.Add(new Mode("60Hz",
                c => c.ImageMode = Controller.DisplayMode.HZ_60), false);
            Modes.Add(new Mode("Outdoor",
                c => c.ImageMode = Controller.DisplayMode.Outdoor), false);

            Modes.Add(new CamServerCore.SeparatorMode(), false);

            Modes.Add(new Mode("Default",
                c => c.ImageAlteration = Controller.DisplayAlteration.Default), false);
            Modes.Add(new Mode("Flip",
                c => c.ImageAlteration = Controller.DisplayAlteration.Flip), false);
            Modes.Add(new Mode("Mirror",
                c => c.ImageAlteration = Controller.DisplayAlteration.Mirror), false);
            Modes.Add(new Mode("Flip and Mirror",
                c => c.ImageAlteration = Controller.DisplayAlteration.FlipAndMirror), false);

            SliderModes.Add(new SliderMode("Contrast", 0, 6, (c, v) => c.Contrast = v), 0);
            SliderModes.Add(new SliderMode("Brightness", 0, 255, (c, v) => c.Brightness = v), 0);

            ModeStateChanged += (mode, state) => Modes[mode] = state;
            SliderValueChanged += (mode, value) => SliderModes[mode] = value;
        }

        private void UpdateValuesFromController()
        {
            SliderValueChanged(SliderModes.Keys.First(s => s.Text == "Brightness"), 
                currentController.Brightness);
            SliderValueChanged(SliderModes.Keys.First(s => s.Text == "Contrast"), 
                currentController.Contrast);

            ModeStateChanged(Modes.Keys.First(s => s.Text == "Default"),
                currentController.ImageAlteration == Controller.DisplayAlteration.Default);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "Flip"),
                currentController.ImageAlteration == Controller.DisplayAlteration.Flip);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "Mirror"),
                currentController.ImageAlteration == Controller.DisplayAlteration.Mirror);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "Flip and Mirror"),
                currentController.ImageAlteration == Controller.DisplayAlteration.FlipAndMirror);

            ModeStateChanged(Modes.Keys.First(s => s.Text == "QVGA (320x240) @ 30fps"),
                currentController.Resolution == Controller.DisplayResolution.QVGA);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "VGA (640x480) @ 15fps"),
                currentController.Resolution == Controller.DisplayResolution.VGA);

            ModeStateChanged(Modes.Keys.First(s => s.Text == "50Hz"),
                currentController.ImageMode == Controller.DisplayMode.HZ_50);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "60Hz"),
                currentController.ImageMode == Controller.DisplayMode.HZ_60);
            ModeStateChanged(Modes.Keys.First(s => s.Text == "Outdoor"),
                currentController.ImageMode == Controller.DisplayMode.Outdoor);

            // NOTE:  We don't know how to query the I/O pin state to IR on/off.
        }

        public Dictionary<IMode, bool> GetModes()
        {
            return Modes;
        }

        public bool ToggleMode(CamServerCore.IMode mode)
        {
            Mode newMode = (Mode)Modes.Keys.First(m => m.Text == mode.Text);
            newMode.Activate(currentController);

            UpdateValuesFromController();
            return true;
        }

        public event Action<CamServerCore.IMode, bool> ModeStateChanged = delegate { };
        public event Action<CamServerCore.ISlider, int> SliderValueChanged = delegate { };

        public Dictionary<CamServerCore.ISlider, int> GetSliderModes()
        {
            return SliderModes;
        }

        public bool SetSliderMode(CamServerCore.ISlider slider, int value)
        {
            SliderMode newMode = (SliderMode)SliderModes.Keys.First(m => m.Text == slider.Text);
            newMode.Activate(currentController, value);
            return true;
        }

        public bool PTSupported
        {
            get { return true; }
        }

        public bool SetPreset(int preset)
        {
            currentController.SetPtzJump(preset);
            return true;
        }

        public bool JumpToPreset(int preset)
        {
            currentController.JumpPtz(preset);
            return true;
        }

        public int GetMaxPresetNum()
        {
            return 16;  
        }

        public bool Move(CamServerCore.DevicePT pt, bool once = false)
        {
            Dictionary<CamServerCore.DevicePT, Controller.PTZAction> Mapping = null;

            if (camVersion == Version.FI8918W)
            {
                // new cameras have right/left swapped (according to CGI SDK 2.1)
                Mapping = new Dictionary<CamServerCore.DevicePT, Controller.PTZAction>
                {
                    { CamServerCore.DevicePT.Center, Controller.PTZAction.Center },
                    { CamServerCore.DevicePT.Left, Controller.PTZAction.Right },
                    { CamServerCore.DevicePT.Left_Stop, Controller.PTZAction.Right_Stop },
                    { CamServerCore.DevicePT.Right, Controller.PTZAction.Left },
                    { CamServerCore.DevicePT.Right_Stop, Controller.PTZAction.Left_Stop },
                    { CamServerCore.DevicePT.Up, Controller.PTZAction.Up },
                    { CamServerCore.DevicePT.Up_Stop, Controller.PTZAction.Up_Stop },
                    { CamServerCore.DevicePT.Down, Controller.PTZAction.Down },
                    { CamServerCore.DevicePT.Down_Stop, Controller.PTZAction.Down_Stop },
                };
            }
            else //FI8908W
            {
                Mapping = new Dictionary<CamServerCore.DevicePT, Controller.PTZAction>
                {
                    { CamServerCore.DevicePT.Center, Controller.PTZAction.Center },
                    { CamServerCore.DevicePT.Left, Controller.PTZAction.Left },
                    { CamServerCore.DevicePT.Left_Stop, Controller.PTZAction.Left_Stop },
                    { CamServerCore.DevicePT.Right, Controller.PTZAction.Right },
                    { CamServerCore.DevicePT.Right_Stop, Controller.PTZAction.Right_Stop },
                    { CamServerCore.DevicePT.Up, Controller.PTZAction.Up },
                    { CamServerCore.DevicePT.Up_Stop, Controller.PTZAction.Up_Stop },
                    { CamServerCore.DevicePT.Down, Controller.PTZAction.Down },
                    { CamServerCore.DevicePT.Down_Stop, Controller.PTZAction.Down_Stop },
                };
            }

            currentController.PTZ(Mapping[pt], once);
            return true;
        }

        public void Dispose()
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            CamServerCore.Config.SaveSettingsForDevice(this.UID, Settings);
        }

        protected override void StartVideoInternal()
        {
            if (stream != null) stream.StopStream();
            stream = new MjpegProcessor.MjpegDecoder();

            if (!Settings.Keys.Contains("User"))
            {
                var kv = OnCredentialsRequired(Environment.UserName);
                Settings["User"] = kv.Key;
                Settings["Pass"] = kv.Value;

                // sick of losing settings
                SaveSettings();
            }

            stream.OnError += err =>
                {
                    ErrorArrived(err.Message);
                    Trace.WriteLine("[KICK] Restarting video...");
                    //StopVideoInternal();
                   // StartVideoInternal();
                };

            stream.ParseStream(new Uri(string.Format(
                "http://{0}/videostream.cgi?user={1}&pwd={2}", UID, Settings["User"], Settings["Pass"])));
            stream.FrameReady += (_, e) =>
                {
                    using (var ptr = new GDIPtr(e.Bitmap.GetHbitmap()))
                    {
                        FrameArrived(System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        ptr.Ptr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
                    }
                };

            try
            {
                currentController = new Controller("http://" + UID + "/", Settings["User"], Settings["Pass"]);

                UpdateValuesFromController();
            }
            catch (WebException ex)
            {
                Trace.WriteLine("Error: " + ex.Message);

                if ((ex.Response as HttpWebResponse).StatusCode == HttpStatusCode.Unauthorized)
                {
                    var kv = OnCredentialsRequired(Settings["User"]);
                    Settings["User"] = kv.Key;
                    Settings["Pass"] = kv.Value;
                    StartVideoInternal();
                }
                else
                {
                    ErrorArrived(ex.Message);
                }
            }
        }

        protected override void StopVideoInternal()
        {
            stream.StopStream();
        }
    }
}

