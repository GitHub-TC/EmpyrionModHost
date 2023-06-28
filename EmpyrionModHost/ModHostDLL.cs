using Eleon.Modding;
using ModExtenderCommunication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace EmpyrionModHost
{
    public class ModHostDLL : ModGameAPI
    {
        public ClientMessagePipe ToEmpyrion { get; private set; }
        public ServerMessagePipe FromEmpyrion { get; private set; }
        public Dictionary<Type, Action<object>> InServerMessageHandler { get; }
        public ModDispatcher Dispatcher { get; private set; }

        public ModHostDLL()
        {
            InServerMessageHandler = new Dictionary<Type, Action<object>> {
                { typeof(EmpyrionGameEventData), M => HandleGameEvent               ((EmpyrionGameEventData)M) },
                { typeof(ClientHostComData    ), M => HandleClientHostCommunication ((ClientHostComData)M) }
            };
        }

        public void InitComunicationChannels()
        {
            Dispatcher = new ModDispatcher();

            Console.WriteLine("ModHostDLL:start");

            ToEmpyrion   = new ClientMessagePipe(CommandLineOptions.GetOption("-ModToEmpyrionPipe", "ModToEmpyrionPipe")) { log = Console.WriteLine };
            FromEmpyrion = new ServerMessagePipe(CommandLineOptions.GetOption("-EmpyrionToModPipe", "EmpyrionToModPipe")) { log = Console.WriteLine };

            Console.WriteLine("ModHostDLL:Pipe initialized");

            FromEmpyrion.Callback = Msg => { if (InServerMessageHandler.TryGetValue(Msg.GetType(), out Action<object> Handler)) Handler(Msg); };

            for (int i = 20; i >= 0 && !FromEmpyrion.Connected; i--) Thread.Sleep(1000);

            Console.WriteLine("ModHostDLL:Dispatcher.Game_Start");
            Dispatcher.Game_Start(this);
        }

        private void HandleClientHostCommunication(ClientHostComData aMsg)
        {
            //Console.WriteLine($"{aMsg.Command} = {aMsg.Data}");
            switch (aMsg.Command)
            {
                case ClientHostCommand.Game_Exit  : Dispatcher?.Game_Exit();   break;
                case ClientHostCommand.Game_Update: Dispatcher?.Game_Update(); break;
            }
        }

        private void HandleGameEvent(EmpyrionGameEventData aMsg)
        {
            var msg = aMsg.GetEmpyrionObject();
            //Console.WriteLine($"Game_Event:{aMsg.eventId}#{aMsg.seqNr} = {msg}");
            Dispatcher?.Game_Event(aMsg.eventId, aMsg.seqNr, msg);
        }

        public void Console_Write(string aMsg)
        {
            Console.WriteLine(aMsg);
        }

        public ulong Game_GetTickTime()
        {
            return (ulong)DateTime.Now.Ticks;
        }

        public bool Game_Request(CmdId reqId, ushort seqNr, object data)
        {
            //Console.WriteLine($"Game_Request:{reqId}#{seqNr} = {data}");
            var msg = new EmpyrionGameEventData() { eventId = reqId, seqNr = seqNr};
            msg.SetEmpyrionObject(data);
            ToEmpyrion.SendMessage(msg);
            return true;
        }
    }
}
