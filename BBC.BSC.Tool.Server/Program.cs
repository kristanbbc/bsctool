using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace BBC.BSC.Tool.Server
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(String[] args)
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new svcServer()
            };
            // In interactive and debug mode ?
            if (Environment.UserInteractive)
            {
                if (System.Diagnostics.Debugger.IsAttached || HasCommand(args, "debug"))
                {
                    // Simulate the services execution
                    RunInteractiveServices(ServicesToRun);
                }
                else
                {
                    try
                    {
                        bool hasCommands = false;

                        // Having install command ?
                        if (HasCommand(args, "install"))
                        {
                            ManagedInstallerClass.InstallHelper(new String[] { typeof(Program).Assembly.Location });
                            hasCommands = true;
                        }

                        // Having a start command ?
                        if (HasCommand(args, "start"))
                        {
                            foreach (var service in ServicesToRun)
                            {
                                ServiceController sc = new ServiceController(service.ServiceName);
                                sc.Start();
                                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                            }
                            hasCommands = true;
                        }

                        // Having a stop command ?
                        if (HasCommand(args, "stop"))
                        {
                            foreach (var service in ServicesToRun)
                            {
                                ServiceController sc = new ServiceController(service.ServiceName);
                                sc.Stop();
                                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                            }
                            hasCommands = true;
                        }
                        // Having uninstall command ?
                        if (HasCommand(args, "uninstall"))
                        {
                            ManagedInstallerClass.InstallHelper(new String[] { "/u", typeof(Program).Assembly.Location });
                            hasCommands = true;
                        }

                        // If we don't have commands print useage message
                        if (!hasCommands)
                        {
                            Console.WriteLine("Useage : {0} [command] [command ...]", Environment.GetCommandLineArgs());
                            Console.WriteLine("Commands :");
                            Console.WriteLine(" - install : Install the services");
                            Console.WriteLine(" - uninstall : Uninstall the services");
                            Console.WriteLine(" - start : Starts the installed services");
                            Console.WriteLine(" - stop : Stops the installed services");
                            // TODO ensure that your service outputs to the console - possibly via nLog
                            Console.WriteLine(" - debug : Runs the services on the command line for debugging.");
                        }
                    }
                    catch (Exception ex)
                    {
                        var oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error : {0}", ex.GetBaseException().Message);
                        Console.ForegroundColor = oldColor;
                    }
                }
            }
            else
            {


                // Normal service execution
                ServiceBase.Run(ServicesToRun);
            }
        }

        /// <summary>
        /// Run services in interactive mode
        /// </summary>
        /// <param name="servicesToRun"></param>
        static void RunInteractiveServices(ServiceBase[] servicesToRun)
        {
            Console.WriteLine();
            Console.WriteLine("Start the services in interactive mode.");
            Console.WriteLine();

            // Get the method to invoke on each service to start it
            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart", BindingFlags.Instance | BindingFlags.NonPublic);

            // Start services loop
            foreach (ServiceBase service in servicesToRun)
            {
                Console.Write("Starting {0} ...", service.ServiceName);
                onStartMethod.Invoke(service, new object[] { new string[] { } });
                Console.WriteLine("Started");
            }

            // Waiting the end
            Console.WriteLine();
            Console.WriteLine("Press a key to stop the services and finish process...");
            Console.ReadKey();
            Console.WriteLine();

            // Get the method to invoke on each service to stop it
            MethodInfo onStopMethod = typeof(ServiceBase).GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic);

            // Stop loop
            foreach (ServiceBase service in servicesToRun)
            {
                Console.WriteLine("Stopping {0}... ", service.ServiceName);
                onStopMethod.Invoke(service, null);
                Console.WriteLine("Stopped");
            }

            Console.WriteLine();
            Console.WriteLine("All services are stopped.");

            // Waiting a key press to not return to VS directly
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine();
                Console.Write("=== Press a key to quit ===");
                Console.ReadKey();
            }

        }

        /// <summary>
        /// Helper to check if we have a command in the command line
        /// </summary>
        /// <param name="args"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        static bool HasCommand(String[] args, String command)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(command)) return false;
            return args.Any(a => String.Equals(a, command, StringComparison.OrdinalIgnoreCase));
        }

    }
}
