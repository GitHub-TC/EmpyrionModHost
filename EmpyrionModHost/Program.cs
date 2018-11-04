using System;
using System.Threading;

namespace EmpyrionModHost
{

    partial class Program
    {
        static void Main(string[] args)
        {
            var Exit = new AutoResetEvent(false);
            var Host = new ModHostDLL();
            Host.GameExit += (S, A) => Exit.Set();
            Host.InitComunicationChannels();
            Exit.WaitOne();
        }

    }
}
