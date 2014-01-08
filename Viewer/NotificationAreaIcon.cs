using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using CamServerCore;

namespace CamServer3Viewer
{
    class NotificationAreaIcon
    {
        ContextMenuStrip strip = new ContextMenuStrip();
        ContextMenu menu = new ContextMenu();
        NotifyIcon ni = new NotifyIcon();

        public NotificationAreaIcon()
        {
            CamServerCore.Core.Inst.DeviceAdded += (dev, bus) =>
                {
                    Trace.WriteLine("NI got notify for new device: " + dev);
                    Discovered(dev);
                };
            var t = new Thread(() =>
            {
                ToolStripMenuItem mnuLoading = new ToolStripMenuItem();
                mnuLoading.Enabled = false;
                mnuLoading.Text = "My Devices";
                mnuLoading.Font = new Font(mnuLoading.Font, FontStyle.Bold);
                strip.Items.Add(mnuLoading);

                strip.Items.Add(new ToolStripSeparator());
                strip.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem m = new ToolStripMenuItem();
                //m.Text = "&Connect To...";
                //m.Click += new EventHandler(Connect_Click);
                //strip.Items.Add(m);

                m = new ToolStripMenuItem();
                m.Text = "&About CamServer3";
                m.Click += new EventHandler(About_Click);
                strip.Items.Add(m);

                strip.Items.Add(new ToolStripSeparator());

                m = new ToolStripMenuItem();
                // m.Image = (Image)Viewer.Properties.Resources.logoff.ToBitmap();
                m.Text = "&Exit";
                m.Click += new EventHandler(m_Click);
                strip.Items.Add(m);


                //ni.ContextMenu = menu;


                ni.ContextMenuStrip = strip;


                if (Debugger.IsAttached)
                {
                    ni.Icon = CamServer3Viewer.Properties.Resources.web_camera_debug;
                }
                else
                {
                    ni.Icon = CamServer3Viewer.Properties.Resources.web_camera;
                }
                ni.Visible = true;
                ni.Text = "CamServer3";
                /*
                if (Settings.Get("FirstRunTray", true))
                {
                    Settings.Set("FirstRunTray", false);
                    ni.ShowBalloonTip(1000 * 60, "CamServer2 is now Active", "CamServer2 will start in the notification area", ToolTipIcon.Info);
                }
                */
                Application.Run();

                Debugger.Break();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        void m_Click(object sender, EventArgs e)
        {
            
            ni.Visible = false;
            // Core.SaveState();
            Core.Inst.Shutdown();
            Environment.Exit(0);
            
        }

        void Connect_Click(object sender, EventArgs e)
        {
            /*
            Thread thread = new Thread(() =>
            {
                ConnectTo ct = new ConnectTo();
                ct.Show();
                System.Windows.Threading.Dispatcher.Run();
                Trace.WriteLine("UI Thread Exiting");
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            */
        }

        void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show("CamServer2 by Dave Amenta (Exp/Alpha)", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void Discovered(CamServerCore.IDevice dev)
        {
            Action add = () =>
                {
                    Trace.WriteLine("Adding " + dev + " to menu");
                    ToolStripMenuItem item = new ToolStripMenuItem();

                    item.Text = dev.Name;
                    item.Tag = dev;
                    item.Click += new EventHandler(item_Click);
                    /*
                    dev.StateChanged += (tdev, st) =>
                    {
                        Trace.WriteLine("State changed for " + tdev + " to " + st);

                        foreach (ToolStripItem ts in strip.Items)
                        {
                            if (ts.Tag as IDevice == tdev)
                            {
                                SetState(ts as ToolStripMenuItem, dev.State);
                                break;
                            }
                        }
                    };
                    */

                    //SetState(item, dev.State);


                    strip.Items.Insert(2, item);
                    /*
                    mnuLoading.Visible = false;

                    MenuItem mi = new MenuItem();
                    mi.Tag = dev;
                    mi.Text = dev.Name;
                    mi.Click += new EventHandler(mi_Click);
                    menu.MenuItems.Add(0, mi);
                    */
                    return;
                    if (DavuxLib2.Settings.Get(dev.UID + "_Open", false))
                    {
                        OpenWindow(dev);
                    }
                };

            Trace.Write("notify add: " + dev.UID);
            if (strip.InvokeRequired)
            {
                strip.BeginInvoke(add);
            }
            else
            {
                add();
            }
        }
        /*
        private void SetState(ToolStripMenuItem item, InterfaceDelegates.DeviceState State)
        {
            if (State == InterfaceDelegates.DeviceState.Disconnected)
            {
                item.Image = (Image)CamServer2.Properties.Resources.Disconnected.ToBitmap();
            }
            else if (State == InterfaceDelegates.DeviceState.Unknown)
            {
                item.Image = (Image)CamServer2.Properties.Resources.question.ToBitmap();
            }
            else if (State == InterfaceDelegates.DeviceState.Online)
            {
                item.Image = (Image)CamServer2.Properties.Resources.ok.ToBitmap();
            }
        }
        */
        void item_Click(object sender, EventArgs e)
        {
            OpenWindow(((ToolStripMenuItem)sender).Tag as IDevice);
        }

        void OpenWindow(IDevice dev)
        {
            Thread thread = new Thread(() =>
            {
                ViewWindow v = new ViewWindow(dev);
                v.Show();
                System.Windows.Threading.Dispatcher.Run();
                Trace.WriteLine("UI Thread Exiting");

            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Dispose()
        {
            ni.Visible = false;
        }
    }
}
