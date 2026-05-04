using System;
using System.IO;
using System.Threading;

namespace EmpyrionModHost
{

    partial class Program
    {
        static bool WithDebugOutput = true;

        static void Main(string[] args)
        {
            try
            {
                if(WithDebugOutput) File.WriteAllText("EmpyrionModHost.log", $"EmpyrionModHost started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                Console.WriteLine("started");

                var Host = new ModHostDLL();
                Host.InitComunicationChannels();

                if (WithDebugOutput) File.WriteAllText("EmpyrionModHost.log", $"WaitForExit at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                Console.WriteLine("WaitForExit");

                var Exit = new AutoResetEvent(false);
                Host.Dispatcher.GameExit += (S, A) => Exit.Set();
                Exit.WaitOne();

                if (WithDebugOutput) File.WriteAllText("EmpyrionModHost.log", $"Exit:{Environment.ExitCode} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                Console.WriteLine($"Exit:{Environment.ExitCode}");

                Environment.Exit(Environment.ExitCode);
            }
            catch (Exception error)
            {
                if (WithDebugOutput) File.WriteAllText("EmpyrionModHost.log", $"Error:{error} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                Console.WriteLine($"Error:{error}");
            }
        }

    }
}
