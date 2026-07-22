using System;
using System.Globalization;
using Microsoft.Win32;

namespace iLang
{
    internal sealed class AppSettings
    {
        private const string RegPath = @"Software\iLng";

        public bool OsdEnabled { get; set; }
        public int VolumePercent { get; set; }

        public AppSettings()
        {
            OsdEnabled = true;
            VolumePercent = 100;
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath, false))
                {
                    if (key == null)
                    {
                        return settings;
                    }

                    object osd = key.GetValue("OsdEnabled");
                    if (osd != null)
                    {
                        settings.OsdEnabled = Convert.ToInt32(osd) != 0;
                    }

                    object vol = key.GetValue("VolumePercent");
                    if (vol != null)
                    {
                        settings.VolumePercent = NormalizeVolume(Convert.ToInt32(vol));
                    }
                }
            }
            catch
            {
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.SetValue("OsdEnabled", OsdEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("VolumePercent", NormalizeVolume(VolumePercent), RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        public static int NormalizeVolume(int value)
        {
            if (value <= 0)
            {
                return 0;
            }
            if (value <= 50)
            {
                return 50;
            }
            return 100;
        }
    }

    internal static class UiStrings
    {
        public static bool IsUkrainian
        {
            get
            {
                try
                {
                    CultureInfo ui = CultureInfo.CurrentUICulture;
                    if (ui != null && ui.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Fallback: OS display language
                    CultureInfo installed = CultureInfo.InstalledUICulture;
                    return installed != null &&
                           installed.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string AppTitle
        {
            get { return IsUkrainian ? "iLng — індикатор мови" : "iLng — language indicator"; }
        }

        public static string InfoMenu
        {
            get { return IsUkrainian ? "iLng інфо" : "iLng info"; }
        }

        public static string Author
        {
            get { return IsUkrainian ? "Ярослав Богачук" : "Yaroslav Bohachuk"; }
        }

        public static string DeveloperLabel
        {
            get { return IsUkrainian ? "Розробник" : "Developer"; }
        }

        public static string GitHubLabel
        {
            get { return "GitHub"; }
        }

        public static string GitHubUrl
        {
            get { return "https://github.com/YariLab/iLng"; }
        }

        public static string Close
        {
            get { return IsUkrainian ? "Закрити" : "Close"; }
        }

        public static string Volume100
        {
            get { return IsUkrainian ? "Гучність 100%" : "Volume 100%"; }
        }

        public static string Volume50
        {
            get { return IsUkrainian ? "Гучність 50%" : "Volume 50%"; }
        }

        public static string VolumeMute
        {
            get { return IsUkrainian ? "Без звуку" : "Mute"; }
        }

        public static string OsdOn
        {
            get { return "OSD"; }
        }

        public static string OsdOff
        {
            get { return IsUkrainian ? "Без OSD" : "No OSD"; }
        }

        public static string Autostart
        {
            get { return IsUkrainian ? "Автозапуск з Windows" : "Start with Windows"; }
        }

        public static string Exit
        {
            get { return IsUkrainian ? "Вихід" : "Exit"; }
        }
    }
}
