using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ElementalTracker
{
    public partial class App : Application
    {
        private static Mutex _mutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd); // checks if minimized

        private const int SW_RESTORE = 9;

        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                Exception inner = e.Exception;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                MessageBox.Show(
                    "Error: " + inner.Message +
                    "\n\nType: " + inner.GetType().Name +
                    "\n\n" + inner.StackTrace,
                    AppInfo.ShortName + " - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "ElementalTracker_SingleInstance_Mutex";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is running — find it and bring it to front
                BringExistingInstanceToFront();
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        private void BringExistingInstanceToFront()
        {
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    if (IsIconic(process.MainWindowHandle))
                        ShowWindow(process.MainWindowHandle, SW_RESTORE);

                    SetForegroundWindow(process.MainWindowHandle);
                    return;
                }
            }
        }
    }
}