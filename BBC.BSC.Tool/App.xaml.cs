using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CrashReporterDotNET;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Application.Current.DispatcherUnhandledException += DispatcherOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            SendReport(unobservedTaskExceptionEventArgs.Exception);
        }

        private void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs dispatcherUnhandledExceptionEventArgs)
        {
            SendReport(dispatcherUnhandledExceptionEventArgs.Exception);
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            SendReport((Exception)unhandledExceptionEventArgs.ExceptionObject);
        }

        public static void SendReport(Exception exception, string developerMessage = "", bool silent = false)
        {
            var reportCrash = new ReportCrash("kristan.webb@bbc.co.uk")
            {
                DeveloperMessage = developerMessage,
                AnalyzeWithDoctorDump = false,
                FromEmail = "bsctool@bbc.co.uk",
                SmtpHost = "smtp.national.core.bbc.co.uk"
            };
            reportCrash.Silent = silent;
            reportCrash.Send(exception);
        }
    }
}
