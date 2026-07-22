using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace iLang
{
    internal sealed class OsdWindow : Window
    {
        private readonly Border _card;
        private readonly TextBlock _code;
        private readonly TextBlock _title;
        private readonly System.Drawing.Rectangle _screenBounds;
        private readonly DispatcherTimer _hideTimer;

        public OsdWindow(System.Drawing.Rectangle screenBounds)
        {
            _screenBounds = screenBounds;

            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _hideTimer.Tick += OnHideTick;

            _code = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 56,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _title = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 240, 240, 240)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(_code);
            stack.Children.Add(_title);

            _card = new Border
            {
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(36, 20, 36, 22),
                MinWidth = 160,
                Background = new SolidColorBrush(Color.FromArgb(230, 18, 20, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Child = stack
            };

            Content = _card;
            Opacity = 0;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Do NOT touch WS_EX_LAYERED — WPF Owns it for AllowsTransparency.
            // Only add click-through / no-activate / toolwindow bits.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(hwnd, Native.GwlExStyle);
            Native.SetWindowLong(
                hwnd,
                Native.GwlExStyle,
                ex | Native.WsExTransparent | Native.WsExToolWindow | Native.WsExNoActivate);
        }

        public void ShowLayout(LayoutInfo info)
        {
            _hideTimer.Stop();
            BeginAnimation(OpacityProperty, null);

            _code.Text = info.Code;
            _title.Text = info.Title;
            ApplyAccent(info.LangId);

            if (!IsVisible)
            {
                Show();
            }

            new WindowInteropHelper(this).EnsureHandle();
            PositionOnScreen();

            Topmost = false;
            Topmost = true;

            Opacity = 1.0;
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(90))
            {
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, __) => { Opacity = 1.0; };
            BeginAnimation(OpacityProperty, fadeIn);

            _hideTimer.Start();
        }

        private void OnHideTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200))
            {
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (_, __) =>
            {
                Opacity = 0;
                Hide();
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ApplyAccent(ushort langId)
        {
            Color fill;
            Color border;
            if (langId == 0x0409)
            {
                fill = Color.FromArgb(235, 24, 42, 68);
                border = Color.FromArgb(160, 120, 180, 255);
            }
            else if (langId == 0x0422)
            {
                fill = Color.FromArgb(235, 48, 40, 14);
                border = Color.FromArgb(180, 255, 214, 70);
            }
            else
            {
                fill = Color.FromArgb(230, 18, 20, 24);
                border = Color.FromArgb(120, 255, 255, 255);
            }

            _card.Background = new SolidColorBrush(fill);
            _card.BorderBrush = new SolidColorBrush(border);
        }

        private void PositionOnScreen()
        {
            UpdateLayout();

            double dipW = ActualWidth > 1 ? ActualWidth : 200;
            double dipH = ActualHeight > 1 ? ActualHeight : 120;

            double scaleX = 1.0;
            double scaleY = 1.0;
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null && source.CompositionTarget != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                scaleX = m.M11;
                scaleY = m.M22;
            }
            else
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    try
                    {
                        uint dpi = Native.GetDpiForWindow(hwnd);
                        if (dpi == 0)
                        {
                            dpi = 96;
                        }
                        scaleX = scaleY = dpi / 96.0;
                    }
                    catch
                    {
                    }
                }
            }

            // Screen.Bounds are physical pixels; WPF Left/Top are DIPs for this window's DPI.
            Left = (_screenBounds.Left + (_screenBounds.Width - dipW * scaleX) / 2.0) / scaleX;
            Top = (_screenBounds.Top + (_screenBounds.Height - dipH * scaleY) / 2.0) / scaleY;
        }
    }
}
