using System;
using System.Linq;

namespace EmpyrionModHost
{

    public static class CommandLineOptions

    {
        public static string GetOption(string aName, string aDefaultValue)
        {
            return Environment.GetCommandLineArgs().Any(A => string.Compare(A, aName, StringComparison.InvariantCultureIgnoreCase) != 0)
                ? Environment.GetCommandLineArgs().SkipWhile(A => string.Compare(A, aName, StringComparison.InvariantCultureIgnoreCase) != 0).Skip(1).FirstOrDefault()
                : aDefaultValue;
        }

    }

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
