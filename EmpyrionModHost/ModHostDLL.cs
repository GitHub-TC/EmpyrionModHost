using Eleon.Modding;
using ModExtenderCommunication;
using System;
using System.Collections.Generic;

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
            ToEmpyrion   = new ClientMessagePipe(CommandLineOptions.GetOption("-ModToEmpyrionPipe", "ModToEmpyrionPipe")) { log = Console.WriteLine };
            FromEmpyrion = new ServerMessagePipe(CommandLineOptions.GetOption("-EmpyrionToModPipe", "EmpyrionToModPipe")) { log = Console.WriteLine };

            FromEmpyrion.Callback = Msg => { if (InServerMessageHandler.TryGetValue(Msg.GetType(), out Action<object> Handler)) Handler(Msg); };

            Dispatcher = new ModDispatcher();
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
            Console.WriteLine($"Game_Event:{aMsg.eventId}#{aMsg.seqNr} = {aMsg.data}");

            Dispatcher?.Game_Event(aMsg.eventId, aMsg.seqNr, aMsg.data);
            Game_Request(aMsg.eventId, aMsg.seqNr, aMsg.data);
        }

        public void Console_Write(string aMsg)
        {
            Console.WriteLine($"Console_Write:{aMsg}");
            ToEmpyrion.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Console_Write, Data = aMsg });
        }

        public ulong Game_GetTickTime()
        {
            return (ulong)DateTime.Now.Ticks;
        }

        public bool Game_Request(CmdId reqId, ushort seqNr, object data)
        {
            Console.WriteLine($"Game_Request:{reqId}#{seqNr} = {data}");
            ToEmpyrion.SendMessage(new EmpyrionGameEventData() { eventId = reqId, seqNr = seqNr, data = data });
            return true;
        }
    }
}
