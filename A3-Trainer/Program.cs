using AnstossTrainer.Shared;
using System;
using System.Windows.Forms;

namespace A3_Trainer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => Logger.Error("Unhandled UI thread exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => Logger.Error("Unhandled exception", e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
