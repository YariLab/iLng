using System;
using System.Threading;
using System.Windows;

namespace iLang
{
    internal static class Program
    {
        private const string MutexName = "Local\\iLngOsdSingleton";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                AppHost host = null;
                bool testOsd = Array.Exists(Environment.GetCommandLineArgs(), a =>
                    string.Equals(a, "--test", StringComparison.OrdinalIgnoreCase));

                app.Startup += (_, __) =>
                {
                    System.Windows.Forms.Application.EnableVisualStyles();
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    host = new AppHost();
                    host.Start();
                    if (testOsd)
                    {
                        host.ShowTestOsd();
                    }
                };
                app.Exit += (_, __) =>
                {
                    if (host != null)
                    {
                        host.Dispose();
                    }
                };

                app.Run();
            }
        }
    }
}
