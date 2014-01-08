using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CamServerCore
{
    public interface IDeviceBus
    {
        bool Discover();
        event Action<IDevice, IDeviceBus> Discovered;
        string Name { get; }
    }

    public interface ILoader
    {
        void Load();
    }

    public interface IDeviceBusAltDiscovery
    {
        bool Create(Dictionary<string, string> Properties);
        Dictionary<string, string> Properties { get; }

        // VarName, VarType
        // Known VarTypes:
        //      - string
        //      - { ListItem1, ListeItem2, ... }
    }

    public interface IDevice
    {
        string UID { get; }
        string Name { get; }

        event Action<string> OnError;
    }

    public interface IVideoDevice
    {
        bool StartVideo();
        bool StopVideo();

        event Action<System.Windows.Media.Imaging.BitmapSource> OnFrame;
        event Func<System.Windows.Media.Imaging.BitmapSource, System.Windows.Media.Imaging.BitmapSource> FilterFrame;
    }

    public interface IDeviceWithAuthentication
    {
        event Func<string, KeyValuePair<string,string>> OnCredentialsRequired;
    }

    public interface IDeviceWithPanTilt
    {
        bool PTSupported { get; }
        bool Move(DevicePT pt, bool once = false);

        // consider:  IDeviceWtihPanTiltPresets
        bool SetPreset(int preset);
        bool JumpToPreset(int preset);
        int GetMaxPresetNum();
    }

    public enum DevicePT
    {
        Up = 1,
        Up_Stop = 2,
        Down = 3,
        Down_Stop = 4,
        Right = 5,
        Right_Stop = 6,
        Left = 7,
        Left_Stop = 8,

        Center = 9,
    }

    public interface IDeviceWithModes
    {
        Dictionary<IMode, bool> GetModes();
        bool ToggleMode(IMode mode);

        event Action<IMode, bool> ModeStateChanged;
    }

    public interface IDeviceWithSliderModes
    {
        Dictionary<ISlider, int> GetSliderModes();
        bool SetSliderMode(ISlider slider, int value);

        event Action<ISlider, int> SliderValueChanged;
    }

    public interface IDeviceWithConfigurationDialog
    {
        bool ShowConfiguration(IntPtr hWnd);
    }

    public interface ISlider : IMode
    {
        int Max { get; }
        int Min { get; }
    }

    public interface IMode
    {
        string Text { get; }
    }

    public class SeparatorMode : IMode
    {
        // impl for ContextMenu seperator.  - is not significant
        public string Text { get { return "-"; } }
    }
}
