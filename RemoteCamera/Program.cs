using System.Threading;

namespace RemoteCamera
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\RemoteCamera.SingleInstance";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            StartupRegistration.EnsureEnabled();
            Application.Run(new frmRemoteCamera());
        }
    }
}
