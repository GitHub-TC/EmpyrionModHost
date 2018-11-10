using System;
using System.Threading;

namespace EmpyrionModHost
{

    partial class Program
    {
        static void Main(string[] args)
        {
            var Host = new ModHostDLL();
            Host.InitComunicationChannels();

            var Exit = new AutoResetEvent(false);
            Host.Dispatcher.GameExit += (S, A) => Exit.Set();
            Exit.WaitOne();

            Environment.Exit(Environment.ExitCode);
        }

    }
}
