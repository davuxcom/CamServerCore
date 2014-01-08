using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamServerCore;

namespace CamServer3Viewer
{
    /// <summary>
    /// Interaction logic for Viewer.xaml
    /// </summary>
    public partial class ViewWindow : Window
    {
        IDevice dev = null;
        IDeviceWithModes mdev = null;
        IDeviceWithPanTilt ptzdev = null;
        IVideoDevice vdev = null;
        IntPtr Handle = IntPtr.Zero;
        int watchdog = 0;

        public ViewWindow(IDevice dev)
        {
            this.dev = dev;
            InitializeComponent();
            this.Tag = dev.UID;

            Closing += (_, __) =>
                {
                    DavuxLib2.Settings.Set(dev.UID + "_Open", false);
                    watchdog = -1; // disable watchdog background thread
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                };


            TransformGroup group = new TransformGroup();
            ScaleTransform xform = new ScaleTransform();
            group.Children.Add(xform);

            TranslateTransform tt = new TranslateTransform();
            group.Children.Add(tt);

            Video.RenderTransform = group;

            MouseRightButtonDown += (_, __) => tt.X = tt.Y = 0;

            Video.MouseLeftButtonDown += image_MouseLeftButtonDown;
            Video.MouseLeftButtonUp += image_MouseLeftButtonUp;
            Video.MouseRightButtonDown += image_MouseLeftButtonDown;
            Video.MouseRightButtonUp += image_MouseLeftButtonUp;

            Video.MouseMove += image_MouseMove;
            Video.MouseWheel += image_MouseWheel;
            Video.Stretch = Stretch.Uniform;
            
            DavuxLib2.Settings.Set(dev.UID + "_Open", true);
        }

        private System.Windows.Point origin;
        private System.Windows.Point start;

        private void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Video.ReleaseMouseCapture();
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Video.IsMouseCaptured) return;

            var currentPoint = e.GetPosition(border);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Video.ReleaseMouseCapture();
                DragMove();
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                var tt = (TranslateTransform)((TransformGroup)Video.RenderTransform).Children.First(tr => tr is TranslateTransform);
                Vector v = start - e.GetPosition(border);
                tt.X = origin.X - v.X;
                tt.Y = origin.Y - v.Y;
                //Debug.WriteLine(string.Format("{0}x {1}y", tt.X, tt.Y));
            }
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Video.CaptureMouse();
            var tt = (TranslateTransform)((TransformGroup)Video.RenderTransform).Children.First(tr => tr is TranslateTransform);
            start = e.GetPosition(border);
            origin = new System.Windows.Point(tt.X, tt.Y);
        }

        System.Windows.Point zpos = new System.Windows.Point(0.5, 0.5);

        private void image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            TransformGroup transformGroup = (TransformGroup)Video.RenderTransform;
            ScaleTransform transform = (ScaleTransform)transformGroup.Children[0];

            double zoom = e.Delta > 0 ? .2 : -.2;
            if (zoom > 0)
            {
                // dunno why, but it's a Point expressing a % 0-1.
                var p = e.GetPosition(border);
                p.X = p.X / Video.ActualWidth;
                p.Y = p.Y / Video.ActualHeight;

                Video.RenderTransformOrigin = p;
            }
            // Disallow zooming out
            if (transform.ScaleX < 1.2 && zoom < 0) { return; }

            transform.ScaleX += zoom;
            transform.ScaleY += zoom;
        }



        bool isBlockingClose = false;
        bool lockedBar = false;

        int frame_count;
        int tick_count;

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;

            btnChangeMode.ContextMenu.Closed += (_, __) =>
            {
                isBlockingClose = false;
            };

            dev.OnError += msg =>
                {
                    Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                    {
                        alerting = true;
                        txtAlert.Text = "Error: " + msg;
                        gAlert.Visibility = Visibility.Visible;
                    }));
                };

            var authdev = dev as IDeviceWithAuthentication;
            if (authdev != null)
            {
                authdev.OnCredentialsRequired += user =>
                    {
                        DavuxLib2.Controls.CredentialsDialog cd = new DavuxLib2.Controls.CredentialsDialog(dev.Name);
                        cd.Caption = "Connect to " + dev.Name;
                        cd.Message = dev.Name + " requires a password.";
                        cd.Name = user;
                        cd.Password = "";
                        Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                        {
                            if (IsEnabled && cd.Show((System.Windows.Forms.IWin32Window)new Win(Handle)) == System.Windows.Forms.DialogResult.Cancel)
                            {
                                IsEnabled = false;
                                Close();
                            }
                        }));
                        return new KeyValuePair<string, string>(cd.Name, cd.Password);
                    };
            }

            // Note: make sure to call this before IVideo.OnStart
            mdev = dev as IDeviceWithModes;
            if (mdev != null)
            {
                PopulateModes();
                btnChangeMode.Visibility = Visibility.Visible;
            }

            ptzdev = dev as IDeviceWithPanTilt;

            if (ptzdev != null && ptzdev.PTSupported)
            {
                PTZ.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                PTZ.Visibility = System.Windows.Visibility.Hidden;
            }

            var smdev = dev as IDeviceWithSliderModes;
            if (smdev != null)
            {
                var sliders = smdev.GetSliderModes();

                foreach (var slider in sliders)
                {
                    var sp = new DockPanel();
                    sp.LastChildFill = true;

                    TextBlock tb = new TextBlock();
                    tb.Padding = new Thickness(4);
                    tb.Text = slider.Key.Text;
                    DockPanel.SetDock(tb, Dock.Left);
                    sp.Children.Add(tb);

                    Slider sl = new Slider();
                    sl.Tag = slider.Key;
                    sl.Value = slider.Value;
                    sl.PreviewMouseUp += (send, _) =>
                    {
                        var s = (send as Slider).Tag as ISlider;
                        (dev as IDeviceWithSliderModes).SetSliderMode(s, (int)(send as Slider).Value);
                    };
                    (dev as IDeviceWithSliderModes).SliderValueChanged += (isli, value) =>
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)(() =>
                            {
                                foreach (var ch in sp.Children)
                                {
                                    // also TextBlock controls
                                    var sdr = ch as Slider;
                                    if (sdr != null)
                                    {
                                        var isdr = sdr.Tag as ISlider;
                                        if (isdr == isli)
                                        {
                                            sdr.Value = value;
                                        }
                                    }
                                }
                            }));
                    };
                    sl.Maximum = slider.Key.Max;
                    sl.Minimum = slider.Key.Min;
                    sp.Children.Add(sl);

                    DockPanel p = new DockPanel();
                    p.LastChildFill = true;
                    DockPanel.SetDock(p, Dock.Bottom);
                    p.Children.Add(sp);
                    quality.Children.Insert(0, p);
                }
            }


            vdev = dev as IVideoDevice;
            if (vdev != null)
            {
                vdev.OnFrame += frame =>
                    {
                        frame.Freeze();
                        Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                        {
                            try
                            {
                                Video.Source = frame;
                                watchdog = 0;
                                pgrLag.Value = 0;

                                frame_count++;
                                if (tick_count == 0) tick_count = Environment.TickCount;
                                if (Environment.TickCount - tick_count > 1000)
                                {
                                    // calculate fps
                                    //Trace.WriteLine(dev.Name + " " + Math.Round((double)frame_count/(double)10,3) + "fps");
                                    FPS.Text = frame_count + " fps";
                                    alerting = false;
                                    gAlert.Visibility = System.Windows.Visibility.Collapsed;
                                    
                                    tick_count = Environment.TickCount;
                                    frame_count = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine("frame err " + ex);
                            }
                        }));
                    };
                new Thread(() =>
                vdev.StartVideo()).Start();

                StartWatchDog();

                System.Timers.Timer t = new System.Timers.Timer(100);
                t.Elapsed += (sx, ex) =>
                {
                    if (!lockedBar)
                    {
                        lockedBar = true;
                        Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                        {
                            try
                            {
                                if (pgrLag.Value + 1 < pgrLag.Maximum)
                                    pgrLag.Value += 1;
                            }
                            catch (Exception ex2)
                            {
                                Trace.WriteLine("pgerr " + ex2);
                            }
                            lockedBar = false;
                        }));
                    }
                };
                t.Start();
            }

            var decd = dev as IDeviceWithConfigurationDialog;
            if (decd != null)
            {
                btnConfigure.Visibility = Visibility.Visible;
                btnConfigure.Tag = decd;
            }

            /*

                qdec = decoder as IQualityControllable;
                if (qdec != null)
                {
                    quality.Visibility = Visibility.Visible;
                    qualitySlider.Value = (double)qdec.Quality;
                    scaleSlider.Value = (double)qdec.Scale;
                }
            */
            Title = dev.Name;
            setButtonText();
        }

        void PopulateModes()
        {
            mdev.ModeStateChanged += (mode, checkState) =>
            {
                Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                {
                    foreach (var item in btnChangeMode.ContextMenu.Items)
                    {
                        var mItem = item as MenuItem;
                        if (mItem != null)
                        {
                            var iMode = mItem.Tag as IMode;
                            if (iMode.Text == mode.Text)
                            {
                                mItem.IsChecked = checkState;
                            }
                        }
                    }
                }));
            };

            btnChangeMode.ContextMenu.Items.Clear();
                
            foreach (var dm in mdev.GetModes())
            {
                if (dm.Key as CamServerCore.SeparatorMode != null)
                {
                    btnChangeMode.ContextMenu.Items.Add(new Separator());
                }
                else
                {
                    MenuItem mi = new MenuItem();
                    mi.Header = dm.Key.Text;
                    mi.Tag = dm.Key;
                    mi.IsChecked = dm.Value;
                    mi.Click += (ex_sender, ex_e) =>
                    {
                        mdev.ToggleMode((ex_sender as MenuItem).Tag as IMode);
                    };
                    btnChangeMode.ContextMenu.Items.Add(mi);
                }
            }
        }

        void dec_Error(string error)
        {
            Trace.WriteLine("decoder error " + error);
            try
            {
                Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                {
                    alerting = true;
                    txtAlert.Text = "Error: " + error;
                    gAlert.Visibility = Visibility.Visible;
                }));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error showing error: " + ex.Message + " Err: " + error);
            }
        }

        bool alerting = false;
        private void StartWatchDog()
        {
            
            new Thread((ThreadStart)delegate()
            {
                try
                {
                    while (watchdog != -1)
                    {
                        Thread.Sleep(1000);
                        watchdog++;

                        if (watchdog > 5 && !alerting)
                        {
                            Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                            {
                                txtAlert.Text = "Warning:  " + dev.Name + " is not responding.";
                                gAlert.Visibility = Visibility.Visible;
                                alerting = true;
                            }));
                        }

                        if (watchdog < 5 && alerting)
                        {
                            Video.Dispatcher.Invoke(DispatcherPriority.Input, (ThreadStart)(() =>
                            {
                                gAlert.Visibility = Visibility.Collapsed;
                                alerting = false;
                            }));
                        }

                        if (watchdog > 5 && watchdog % 15 == 0)
                        {
                            Trace.WriteLine("Kickstart " + watchdog);
                            // attempt to kickstart every 10s when in error state.
                            // neither of these errors matter!
                            try { vdev.StopVideo(); }
                            catch { }
                            try { vdev.StartVideo(); }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("watchdog error " + ex);
                }
                Trace.WriteLine("Watchdog ending, decoders removed");
            }).Start();
            
        }

        private void setButtonText()
        {
            /*
            if (dev.Class != "Network Device")
            {
                if (dev.Settings.Keys.Contains("UserName")
                    && !string.IsNullOrEmpty(dev.Settings["UserName"]))
                {
                    btnLock.Text = "Secure Access - Change Password";
                }
                else
                {
                    btnLock.Text = "Public Access - Set a Password";
                }
            }
            else
            {
                btnLockButton.Visibility = Visibility.Collapsed;
            }
             */
        }

        private void btnLock_Click(object sender, RoutedEventArgs e)
        {
            /*
            DavuxLib.Controls.CredentialsDialog cd = new DavuxLib.Controls.CredentialsDialog("Change Password");
            cd.Caption = "Change Password: " + dev.Name;
            cd.Name = dev.Settings.Keys.Contains("UserName") ? dev.Settings["UserName"] : "";
            cd.Password = dev.Settings.Keys.Contains("Password") ? dev.Settings["Password"] : "";

            if (cd.Show(new Win(Handle)) == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

            if (dev.Settings.Keys.Contains("UserName"))
            {
                dev.Settings["UserName"] = cd.Name;
                dev.Settings["Password"] = cd.Password;
            }
            else
            {
                dev.Settings.Add("UserName", cd.Name);
                dev.Settings.Add("Password", cd.Password);
            }
            */
            setButtonText();
        }

        private void btnAlert_Click(object sender, RoutedEventArgs e)
        {
            if (gAlert.Visibility == Visibility.Collapsed)
            {
                gAlert.Visibility = Visibility.Visible;
            }
            else
            {
                gAlert.Visibility = Visibility.Collapsed;
            }
        }

        private void btnRecordButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (recdev.IsRecording)
            {
                recdev.StopRecording();
            }
            else
            {
                recdev.StartRecording();
            }
            */
        }

        private void btnConfigure_Click(object sender, RoutedEventArgs e)
        {
            var d = btnConfigure.Tag as IDeviceWithConfigurationDialog;
            if (d != null)
            {
                d.ShowConfiguration(new WindowInteropHelper(this).Handle);
            }
        }

        private void Viewbox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                e.ClickCount >= 2)
            {
                if (WindowStyle == WindowStyle.None)
                {
                    // leave fullscreen
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    Topmost = false;
                    WindowState = WindowState.Normal;
                }
                else
                {
                    // enter fullscreen
                    WindowState = WindowState.Normal;
                    WindowStyle = WindowStyle.None;
                    Topmost = true;
                    WindowState = WindowState.Maximized;
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // qdec.Quality = (int)qualitySlider.Value;
        }

        private void btnChangeResolution_Click(object sender, RoutedEventArgs e)
        {
            //ResolutionChanger rc = new ResolutionChanger(sizedec.FrameSize);
            //rc.ShowDialog();
            // sizedec.FrameSize = rc.Size;
            // XBAP  contextMenu.PlacementTarget = sender as UIElement; 
            //btnChangeResolution.ContextMenu.PlacementTarget = sender as UIElement;
            btnChangeResolution.ContextMenu.PlacementTarget = btnChangeResolution;
            btnChangeResolution.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ContextMenuService.SetPlacement(btnChangeResolution, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            //btnChangeResolution.ContextMenu.IsOpen = true;

            btnChangeResolution.ContextMenu.IsOpen = true;
        }

        private void mnuLagMeter_Click(object sender, RoutedEventArgs e)
        {
            pgrLag.Visibility = Visibility.Collapsed;
        }

        private void btnChangeMode_Click(object sender, RoutedEventArgs e)
        {
            isBlockingClose = true;
            Button b = sender as Button;

            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ContextMenuService.SetPlacement(b, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            b.ContextMenu.IsOpen = true;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                ((Storyboard)FindResource("AMouseEnter")).Begin(this);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("mouse enter error " + ex);
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (isBlockingClose) return;
                ((Storyboard)FindResource("AMouseLeave")).Begin(this);

            }
            catch (Exception ex)
            {
                Trace.WriteLine("mouse move " + ex);
            }
        }

        private void scaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // qdec.Scale = (int)scaleSlider.Value;
        }

        private void Window_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isBlockingClose) ((Storyboard)FindResource("AMouseLeave")).Begin(this);
        }



        private void btnUp_MouseDown(object sender, MouseButtonEventArgs e)
        {
          //  controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Up);
            ptzdev.Move(DevicePT.Up);
        }

        private void btnUp_MouseUp(object sender, MouseButtonEventArgs e)
        {
          //  controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Up_Stop);
            ptzdev.Move(DevicePT.Up_Stop);
        }

        private void btnLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
           // controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Left);
            ptzdev.Move(DevicePT.Left);
        }

        private void btnLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {
          //  controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Left_Stop);
            ptzdev.Move(DevicePT.Left_Stop);
        }

        private void btnStop_MouseDown(object sender, MouseButtonEventArgs e)
        {
           // controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Center);
            ptzdev.Move(DevicePT.Center);
        }

        private void btnStop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 
        }

        private void btnRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
           // controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Right);
            ptzdev.Move(DevicePT.Right);
        }

        private void btnRight_MouseUp(object sender, MouseButtonEventArgs e)
        {
           // controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Right_Stop);
            ptzdev.Move(DevicePT.Right_Stop);
        }

        private void btnDown_MouseDown(object sender, MouseButtonEventArgs e)
        {
          //  controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Down);
            ptzdev.Move(DevicePT.Down);
        }

        private void btnDown_MouseUp(object sender, MouseButtonEventArgs e)
        {
          //  controller.PTZ(IPCamCGI.IPCameraController.PTZAction.Down_Stop);
            ptzdev.Move(DevicePT.Down_Stop);
        }

        bool up, down, left, right = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ptzdev != null)
            {
                bool once = e.KeyboardDevice.Modifiers == ModifierKeys.Control;

                if (e.Key == Key.Left && !left)
                {
                    ptzdev.Move(DevicePT.Left, once);
                    left = true;
                }
                else if (e.Key == Key.Right && !right)
                {
                    ptzdev.Move(DevicePT.Right, once);
                    right = true;
                }
                else if (e.Key == Key.Up && !up)
                {
                    ptzdev.Move(DevicePT.Up, once);
                    up = true;
                }
                else if (e.Key == Key.Down && !down)
                {
                    ptzdev.Move(DevicePT.Down, once);
                    down = true;
                }
                else
                {
                    HandlePtzPresets(e);
                }
            }
        }

        private void HandlePtzPresets(KeyEventArgs e)
        {
            int DigitBase = 34;
            int NumBase = 74;

            for (int i = 0; i < Math.Min(ptzdev.GetMaxPresetNum(), 9); i++)
            {
                if (e.Key == (Key)(DigitBase + i) || e.Key == (Key)(NumBase + i))
                {
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        ptzdev.SetPreset(i);
                    }
                    else
                    {
                        ptzdev.JumpToPreset(i);
                    }
                    Trace.WriteLine(i + " Preset Activated");
                    break;
                }
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (ptzdev != null)
            {
                if (e.Key == Key.Left)
                {
                    ptzdev.Move(DevicePT.Left_Stop);
                    left = false;
                }
                else if (e.Key == Key.Right)
                {
                    ptzdev.Move(DevicePT.Right_Stop);
                    right = false;
                }
                else if (e.Key == Key.Up)
                {
                    ptzdev.Move(DevicePT.Up_Stop);
                    up = false;
                }
                else if (e.Key == Key.Down)
                {
                    ptzdev.Move(DevicePT.Down_Stop);
                    down = false;
                }
            }
        }

    }

    public class Win : System.Windows.Forms.IWin32Window
    {

        public Win(IntPtr Handle)
        {
            this.Handle = Handle;
        }
        public IntPtr Handle { get; private set; }

    }
}
