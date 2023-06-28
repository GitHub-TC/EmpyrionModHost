using System;
using System.Threading;

namespace EmpyrionModHost
{

    partial class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("started");

                var Host = new ModHostDLL();
                Host.InitComunicationChannels();

                Console.WriteLine("WaitForExit");

                var Exit = new AutoResetEvent(false);
                Host.Dispatcher.GameExit += (S, A) => Exit.Set();
                Exit.WaitOne();

                Console.WriteLine($"Exit:{Environment.ExitCode}");

                Environment.Exit(Environment.ExitCode);
            }
            catch (Exception error)
            {
                Console.WriteLine($"Error:{error}");
            }
        }

    }
}
