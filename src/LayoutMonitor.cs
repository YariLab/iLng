using System;
using System.Windows.Threading;

namespace iLang
{
    internal sealed class LayoutInfo
    {
        public LayoutInfo(ushort langId, string code, string title)
        {
            LangId = langId;
            Code = code;
            Title = title;
        }

        public ushort LangId { get; private set; }
        public string Code { get; private set; }
        public string Title { get; private set; }
    }

    internal sealed class LayoutMonitor
    {
        private readonly DispatcherTimer _timer;
        private ushort _lastLangId;
        private bool _ready;

        public event Action<LayoutInfo> LayoutChanged;

        public LayoutMonitor()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            _lastLangId = GetCurrentLangId();
            _ready = true;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public LayoutInfo PeekCurrent()
        {
            return Describe(GetCurrentLangId());
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_ready)
            {
                return;
            }

            ushort langId = GetCurrentLangId();
            if (langId == _lastLangId)
            {
                return;
            }

            _lastLangId = langId;
            LayoutInfo info = Describe(langId);
            Action<LayoutInfo> handler = LayoutChanged;
            if (handler != null)
            {
                handler(info);
            }
        }

        private static ushort GetCurrentLangId()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            uint threadId = 0;
            if (hwnd != IntPtr.Zero)
            {
                threadId = Native.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
            }

            IntPtr hkl = Native.GetKeyboardLayout(threadId);
            if (hkl == IntPtr.Zero)
            {
                hkl = Native.GetKeyboardLayout(0);
            }

            return (ushort)((long)hkl & 0xFFFF);
        }

        private static LayoutInfo Describe(ushort langId)
        {
            switch (langId)
            {
                case 0x0409:
                    return new LayoutInfo(langId, "EN", "English");
                case 0x0422:
                    return new LayoutInfo(langId, "UA", "Ukrainian");
                default:
                    try
                    {
                        var culture = new System.Globalization.CultureInfo(langId);
                        string code = culture.TwoLetterISOLanguageName.ToUpperInvariant();
                        return new LayoutInfo(langId, code, culture.EnglishName);
                    }
                    catch
                    {
                        return new LayoutInfo(langId, langId.ToString("X4"), "Layout");
                    }
            }
        }
    }
}
