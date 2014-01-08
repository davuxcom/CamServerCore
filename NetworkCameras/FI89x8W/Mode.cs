using System;

namespace NetworkCameras.FI89x8W
{
    class Mode : CamServerCore.IMode
    {
        Action<Controller> Activation;
        public void Activate(Controller c)
        {
            Activation.Invoke(c);
        }
        public string Text { get; private set; }

        public Mode(string Text, Action<Controller> Activation)
        {
            this.Activation = Activation;
            this.Text = Text;
        }
    }

    class SliderMode : CamServerCore.ISlider
    {
        public int Max { get; private set; }
        public int Min { get; private set; }
        public string Text { get; private set; }

        Action<Controller, int> Activation;

        public SliderMode(string Text, int Min, int Max, Action<Controller, int> Activation)
        {
            this.Activation = Activation;
            this.Min = Min;
            this.Max = Max;
            this.Text = Text;
        }

        public void Activate(Controller c, int value)
        {
            Activation.Invoke(c, value);
        }
    }
}
