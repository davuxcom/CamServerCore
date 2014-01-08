using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace NetworkCameras.FI89x8W
{
    public class Controller : INotifyPropertyChanged
    {
        public enum PTZAction
        {
            Up = 0,
            Up_Stop = 1,
            Down = 2,
            Down_Stop = 3,
            Left = 4,
            Left_Stop = 5,
            Right = 6,
            Right_Stop = 7,
            Center = 25,
            Patrol_Vertical = 26,
            Patrol_Vertical_Stop = 27,
            Patrol_Horizon = 28,
            Patrol_Horizon_Stop = 29,
            IO_High = 94,   // IR Off (FI8918W)
            IO_Low = 95,    // IR On (FI8918W)
        }

        public enum DisplayResolution
        {
            QVGA = 8,
            VGA = 32,
        }

        public enum DisplayMode
        {
            HZ_50 = 0,
            HZ_60 = 1,
            Outdoor = 2,
        }

        public enum DisplayAlteration
        {
            Default = 0,
            Flip = 1,
            Mirror = 2,
            FlipAndMirror = 3,
        }

        string UserName = "";
        string Password = "";
        string BaseURL = "";

        bool fNoCommit = false;

        public Controller(string BaseURL, string UserName, string Password)
        {
            this.BaseURL = BaseURL;
            this.UserName = UserName;
            this.Password = Password;

            LoadVariables();
        }

        public void PTZ(PTZAction action, bool once = false)
        {
            DecoderControl((int)action, once);
        }

        /*
         * And here are the URL commands for the PRESET function (FI8918w):
            decoder_control.cgi?command=30 = Set preset 0
            decoder_control.cgi?command=31 = Go preset 0
            decoder_control.cgi?command=32 = Set preset 1
            decoder_control.cgi?command=33 = Go preset 1
            decoder_control.cgi?command=34 = Set preset 2
            decoder_control.cgi?command=35 = Go preset 2
            decoder_control.cgi?command=36 = Set preset 3
            decoder_control.cgi?command=37 = Go preset 3
            And the list goes further until preset 16 */

        int GetPresetCommand(int preset, bool IsGet = true)
        {
            if (preset < 0 || preset > 16) throw new ArgumentException("preset must be between 0 and 16");

            return 30 + (preset * 2) + (IsGet ? 1 : 0);
        }

        public void JumpPtz(int preset)
        {
            DecoderControl(GetPresetCommand(preset));
        }

        public void SetPtzJump(int preset)
        {
            DecoderControl(GetPresetCommand(preset, false));
        }


        public void Reboot()
        {
            Req("reboot.cgi");
        }

        void LoadVariables()
        {
            string vars = Req("get_camera_params.cgi?");
            fNoCommit = true;
            foreach (string s in vars.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match m = Regex.Match(s, ".* (.*)=(.*);");
                if (m.Success)
                {
                    try
                    {
                        string key = m.Groups[1].Value;
                        int value = int.Parse(m.Groups[2].Value);
                        switch (key)
                        {
                            case "resolution":
                                Resolution = (DisplayResolution)value;
                                break;
                            case "brightness":
                                Brightness = value;
                                break;
                            case "contrast":
                                Contrast = value;
                                break;
                            case "mode":
                                ImageMode = (DisplayMode)value;
                                break;
                            case "flip":
                                ImageAlteration = (DisplayAlteration)value;
                                break;
                            default:
                                Trace.WriteLine("Unknown variable: " + s);
                                break;
                        }
                    }
                    catch (FormatException ex)
                    {
                        Trace.WriteLine("Variable not integer: " + s);
                    }
                }
                else
                {
                    Trace.WriteLine("Unrecognized variable: " + s);
                }
            }
            fNoCommit = false;
        }

        static int req_count = 0;

        string Req(string req, Action Success = null, Action Failed = null)
        {
            int this_req = ++req_count;

            if (fNoCommit) return "";

            try
            {
                Trace.WriteLine(string.Format("Req {0}: {1}", this_req, req));                 
                string ret = new WebClient().DownloadString(string.Format("{0}{1}&user={2}&pwd={3}",
                    BaseURL, req, UserName, Password));
                Trace.WriteLine(string.Format("Req {0} => {1}", this_req, ret));
                if (ret == "Ok" && Success != null) Success();
                return ret;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("Req {0} Failed: {1}", this_req, ex.Message));
                if (Failed != null) Failed();
                return "";
            }

        }

        void DecoderControl(int cmd, bool oneStep = false)
        {
            // Control PTZ - no need to refresh variable state
            new Thread(() =>
                {
                    Req("decoder_control.cgi?command=" + cmd + (oneStep ? "&onestep=20" : ""));
                }).Start();// + "&onestep=20";
        }

        void CameraControl(int param, int value)
        {
            new Thread(() =>
                {
                    Req(string.Format("camera_control.cgi?param={0}&value={1}", param, value));
                    LoadVariables();
                }).Start();
        }

        int _Brightness = 0;
        public int Brightness
        {
            get
            {
                return _Brightness;
            }
            set
            {
                _Brightness = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Brightness"));
                if (!fNoCommit) CameraControl(1, _Brightness);
            }
        }

        int _Contrast = 0;
        public int Contrast
        {
            get
            {
                return _Contrast;
            }
            set
            {
                _Contrast = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Contrast"));
                if (!fNoCommit) CameraControl(2, _Contrast);
            }
        }

        DisplayResolution _Resolution = DisplayResolution.QVGA;
        public DisplayResolution Resolution
        {
            get
            {
                return _Resolution;
            }
            set
            {
                _Resolution = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Resolution"));
                if (!fNoCommit) CameraControl(0, (int)_Resolution);
            }
        }

        DisplayMode _ImageMode = DisplayMode.HZ_50;
        public DisplayMode ImageMode
        {
            get
            {
                return _ImageMode;
            }
            set
            {
                _ImageMode = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ImageMode"));
                if (!fNoCommit) CameraControl(3, (int)_ImageMode);
            }
        }

        DisplayAlteration _ImageAlteration = DisplayAlteration.Default;
        public DisplayAlteration ImageAlteration
        {
            get
            {
                return _ImageAlteration;
            }
            set
            {
                _ImageAlteration = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ImageAlteration"));
                if (!fNoCommit) CameraControl(5, (int)_ImageAlteration);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
    }
}
