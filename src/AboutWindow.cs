using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace iLang
{
    internal sealed class AboutWindow : Window
    {
        private static AboutWindow _open;

        public AboutWindow()
        {
            Title = UiStrings.InfoMenu;
            Width = 440;
            Height = 240;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
            Background = new SolidColorBrush(Color.FromRgb(248, 249, 251));
            FontFamily = new FontFamily("Segoe UI");
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            BitmapSource logo = LoadLogo(256);
            if (logo != null)
            {
                Icon = logo;
            }

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            var root = new Grid { Margin = new Thickness(20) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var iconImage = new Image
            {
                Width = 96,
                Height = 96,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SnapsToDevicePixels = true,
                Source = logo ?? LoadLogo(128)
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);

            Grid.SetRow(iconImage, 0);
            Grid.SetColumn(iconImage, 0);
            root.Children.Add(iconImage);

            var textStack = new StackPanel
            {
                Margin = new Thickness(12, 4, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            textStack.Children.Add(new TextBlock
            {
                Text = UiStrings.AppTitle,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 10)
            });

            textStack.Children.Add(new TextBlock
            {
                Text = UiStrings.DeveloperLabel + ": " + UiStrings.Author,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 55, 64)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var githubBlock = new TextBlock
            {
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            githubBlock.Inlines.Add(new Run(UiStrings.GitHubLabel + ": "));
            var link = new Hyperlink(new Run(UiStrings.GitHubUrl))
            {
                NavigateUri = new Uri(UiStrings.GitHubUrl),
                Foreground = new SolidColorBrush(Color.FromRgb(30, 90, 170))
            };
            link.RequestNavigate += (_, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
                e.Handled = true;
            };
            githubBlock.Inlines.Add(link);
            textStack.Children.Add(githubBlock);

            Grid.SetRow(textStack, 0);
            Grid.SetColumn(textStack, 1);
            root.Children.Add(textStack);

            var closeButton = new Button
            {
                Content = UiStrings.Close,
                Width = 100,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                IsDefault = true,
                IsCancel = true
            };
            closeButton.Click += (_, __) => Close();
            Grid.SetRow(closeButton, 1);
            Grid.SetColumn(closeButton, 0);
            Grid.SetColumnSpan(closeButton, 2);
            root.Children.Add(closeButton);

            Content = root;
            Closed += (_, __) =>
            {
                if (ReferenceEquals(_open, this))
                {
                    _open = null;
                }
            };
        }

        private static BitmapSource LoadLogo(int preferredSize)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string[] resourceNames =
                {
                    "iLng.Logo256.png"
                };

                foreach (string name in resourceNames)
                {
                    using (Stream stream = asm.GetManifestResourceStream(name))
                    {
                        if (stream == null)
                        {
                            continue;
                        }

                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = stream;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bi.DecodePixelWidth = Math.Max(preferredSize, 256);
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static void ShowSingleton()
        {
            if (_open != null)
            {
                if (_open.WindowState == WindowState.Minimized)
                {
                    _open.WindowState = WindowState.Normal;
                }
                _open.Activate();
                _open.Focus();
                return;
            }

            _open = new AboutWindow();
            _open.Show();
            _open.Activate();
        }
    }
}
