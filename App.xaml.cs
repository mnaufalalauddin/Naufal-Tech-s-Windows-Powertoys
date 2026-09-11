using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        private const string SingleInstanceMutexName =
            @"Local\NaufalWindowsPowertoys_V78_SingleInstance";

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out _ownsSingleInstanceMutex);
            try
            {
                InitializeComponent();
            }
            catch (Exception exception)
            {
                WriteCrashLog(exception, "App.InitializeComponent");
                throw;
            }
            UnhandledException += App_UnhandledException;
            if (_ownsSingleInstanceMutex)
                foreach (string warning in AppDataPaths.MigrateKnownFiles(AppDataPaths.LocalBase))
                    WriteCrashLog(new IOException(warning), "AppData migration warning");
        }

        private static void App_UnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            WriteCrashLog(args.Exception, "Application.UnhandledException");
        }

        internal static void WriteCrashLog(Exception exception, string stage)
        {
            try
            {
                string directory = AppDataPaths.LocalRoot;
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    System.IO.Path.Combine(directory, "crash.log"),
                    $"[{DateTimeOffset.Now:O}] {stage}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Preserve the original exception even if crash logging is unavailable.
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            if (!_ownsSingleInstanceMutex)
            {
                MessageBoxW(
                    IntPtr.Zero,
                    "Naufal Tech's Windows Powertoys is already running",
                    "Naufal Tech's Windows Powertoys",
                    0x00000040U);
                Exit();
                return;
            }

            try
            {
                // Taskbar identity must be chosen before creating the first UI.
                ShellAppIdentity.ApplyToCurrentProcess();
                _window = new MainWindow();
                _window.Activate();
            }
            catch (Exception exception)
            {
                WriteCrashLog(exception, "App.OnLaunched");
                throw;
            }
        }

        internal void ReleaseSingleInstance()
        {
            if (_ownsSingleInstanceMutex && _singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // The process is already shutting down or no longer owns it.
                }
            }

            _ownsSingleInstanceMutex = false;
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBoxW(
            IntPtr hWnd,
            string lpText,
            string lpCaption,
            uint uType);
    }
}
