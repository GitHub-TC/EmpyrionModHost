using Eleon.Modding;
using System;
using System.Collections.Generic;

namespace ClientTestPlatform
{
    class Program
    {
        static void Main(string[] args)
        {
            var Client = new EmpyrionModClient.EmpyrionModClient();
            var gameAPIMockup = new GameAPIMockup();

            Client.Game_Start(gameAPIMockup);
            Client.InServer.Callback = O => Console.WriteLine($"Receive{O}");

            while (Console.ReadKey().KeyChar == ' ')
            {
                Client.Game_Event(Eleon.Modding.CmdId.Event_Player_Info, 1, 
                    new PlayerInfo() { playerName = "abc" });
                Client.Game_Event(Eleon.Modding.CmdId.Event_AlliancesAll, 1, 
                    new Eleon.Modding.AlliancesTable() { alliances = new HashSet<int>(new[] { 1, 3, 4 } )  });

                var GSL = new Eleon.Modding.GlobalStructureList();
                var GS = GSL.globalStructures = new Dictionary<string, List<GlobalStructureInfo>>();
                GS.Add("a", new List<GlobalStructureInfo>(
                            new[] { new GlobalStructureInfo() { id = 1, name = "S1" } }
                            ));
                Client.Game_Event(Eleon.Modding.CmdId.Event_GlobalStructure_List, 1, GSL);
                Client.Game_Update();
            }

            Client.Game_Exit();
            Console.WriteLine("finish...");
        }
    }
}
