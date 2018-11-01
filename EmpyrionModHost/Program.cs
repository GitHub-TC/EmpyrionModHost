using System;

namespace EmpyrionModHost
{

    partial class Program
    {
        static void Main(string[] args)
        {
            var Host = new ModHostDLL();
            Host.InitComunicationChannels();
            Console.ReadLine();
        }

    }
}
