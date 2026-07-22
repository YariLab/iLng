using System;
using System.Drawing;
using System.Windows;
using Microsoft.Win32;

namespace iLang
{
    internal sealed class AppHost : IDisposable
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "iLng";
        private const string LegacyRunValueName = "iLang";

        private readonly AppSettings _settings;
        private readonly LayoutMonitor _monitor;
        private readonly OsdService _osd;
        private readonly SoundNotifier _sound;
        private readonly System.Windows.Forms.NotifyIcon _tray;
        private readonly System.Windows.Forms.ContextMenuStrip _menu;
        private readonly System.Windows.Forms.ToolStripMenuItem _volume100;
        private readonly System.Windows.Forms.ToolStripMenuItem _volume50;
        private readonly System.Windows.Forms.ToolStripMenuItem _volumeMute;
        private readonly System.Windows.Forms.ToolStripMenuItem _osdItem;
        private readonly System.Windows.Forms.ToolStripMenuItem _autostartItem;
        private bool _updatingUi;

        public AppHost()
        {
            _settings = AppSettings.Load();
            _osd = new OsdService();
            _sound = new SoundNotifier { VolumePercent = _settings.VolumePercent };
            _monitor = new LayoutMonitor();
            _monitor.LayoutChanged += OnLayoutChanged;

            _menu = new System.Windows.Forms.ContextMenuStrip();
            _menu.Items.Add(UiStrings.InfoMenu, null, (_, __) => Ui(AboutWindow.ShowSingleton));
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            _volume100 = CreateVolumeItem(UiStrings.Volume100, 100);
            _volume50 = CreateVolumeItem(UiStrings.Volume50, 50);
            _volumeMute = CreateVolumeItem(UiStrings.VolumeMute, 0);
            _menu.Items.Add(_volume100);
            _menu.Items.Add(_volume50);
            _menu.Items.Add(_volumeMute);
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            _osdItem = new System.Windows.Forms.ToolStripMenuItem();
            _osdItem.CheckOnClick = true;
            _osdItem.Checked = _settings.OsdEnabled;
            _osdItem.CheckedChanged += (_, __) =>
            {
                if (_updatingUi)
                {
                    return;
                }

                Ui(() =>
                {
                    _settings.OsdEnabled = _osdItem.Checked;
                    UpdateOsdItemText();
                    _settings.Save();
                });
            };
            UpdateOsdItemText();
            _menu.Items.Add(_osdItem);
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            _autostartItem = new System.Windows.Forms.ToolStripMenuItem(UiStrings.Autostart);
            _autostartItem.CheckOnClick = true;
            _autostartItem.Checked = IsAutostartEnabled();
            _autostartItem.CheckedChanged += (_, __) =>
            {
                if (_updatingUi)
                {
                    return;
                }

                Ui(() => SetAutostart(_autostartItem.Checked));
            };
            _menu.Items.Add(_autostartItem);
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _menu.Items.Add(UiStrings.Exit, null, (_, __) => Ui(() => Application.Current.Shutdown()));

            SyncVolumeChecks();

            _tray = new System.Windows.Forms.NotifyIcon
            {
                Text = UiStrings.AppTitle,
                Icon = CreateTrayIcon(),
                Visible = true,
                ContextMenuStrip = _menu
            };
            _tray.DoubleClick += (_, __) => Ui(PreviewCurrent);
        }

        public void Start()
        {
            _monitor.Start();
        }

        public void ShowTestOsd()
        {
            var info = new LayoutInfo(0x0422, "UA", "Ukrainian");
            if (_settings.OsdEnabled)
            {
                _osd.Show(info);
            }
            _sound.Play(info);
        }

        public void Dispose()
        {
            _monitor.Stop();
            _monitor.LayoutChanged -= OnLayoutChanged;
            _settings.Save();
            _osd.Dispose();
            _sound.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
        }

        private System.Windows.Forms.ToolStripMenuItem CreateVolumeItem(string text, int volume)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(text);
            item.Click += (_, __) => Ui(() => SetVolume(volume));
            return item;
        }

        private void SetVolume(int volume)
        {
            _settings.VolumePercent = AppSettings.NormalizeVolume(volume);
            _sound.VolumePercent = _settings.VolumePercent;
            SyncVolumeChecks();
            _settings.Save();
        }

        private void SyncVolumeChecks()
        {
            _updatingUi = true;
            try
            {
                int v = _settings.VolumePercent;
                _volume100.Checked = v == 100;
                _volume50.Checked = v == 50;
                _volumeMute.Checked = v == 0;
            }
            finally
            {
                _updatingUi = false;
            }
        }

        private void UpdateOsdItemText()
        {
            _osdItem.Text = _osdItem.Checked ? UiStrings.OsdOn : UiStrings.OsdOff;
        }

        private static void Ui(Action action)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.BeginInvoke(action);
            }
        }

        private void OnLayoutChanged(LayoutInfo info)
        {
            if (_settings.OsdEnabled)
            {
                _osd.Show(info);
            }

            _sound.Play(info);
        }

        private void PreviewCurrent()
        {
            LayoutInfo current = _monitor.PeekCurrent();
            if (_settings.OsdEnabled)
            {
                _osd.Show(current);
            }
            _sound.Play(current);
        }

        private static bool IsAutostartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null)
                {
                    return false;
                }

                return key.GetValue(RunValueName) != null || key.GetValue(LegacyRunValueName) != null;
            }
        }

        private static void SetAutostart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null)
                {
                    return;
                }

                // Clean legacy name when toggling.
                key.DeleteValue(LegacyRunValueName, false);

                if (enabled)
                {
                    string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(RunValueName, "\"" + exe + "\"");
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static Icon CreateTrayIcon()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Icon associated = Icon.ExtractAssociatedIcon(exe);
                if (associated != null)
                {
                    return associated;
                }
            }
            catch
            {
            }

            // Fallback if exe has no embedded icon.
            return SystemIcons.Application;
        }
    }
}
