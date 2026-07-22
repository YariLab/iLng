using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;

namespace iLang
{
    internal sealed class OsdService : IDisposable
    {
        private readonly List<OsdWindow> _windows = new List<OsdWindow>();

        public OsdService()
        {
            Rebuild();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        public void Show(LayoutInfo info)
        {
            if (_windows.Count == 0)
            {
                Rebuild();
            }

            foreach (OsdWindow window in _windows)
            {
                window.ShowLayout(info);
            }
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            foreach (OsdWindow window in _windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                }
            }
            _windows.Clear();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(Rebuild));
            }
        }

        private void Rebuild()
        {
            foreach (OsdWindow window in _windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                }
            }
            _windows.Clear();

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                _windows.Add(new OsdWindow(screen.Bounds));
            }
        }
    }
}
