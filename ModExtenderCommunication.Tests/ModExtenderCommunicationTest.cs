using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModExtenderCommunication.Tests
{
    [TestClass]
    public class ModExtenderCommunicationTest
    {
        [TestMethod]
        public void ClientServerCommunication()
        {
            var c = new ClientMessagePipe("a");
            var s = new ServerMessagePipe("a");

            var e = new AutoResetEvent(false);

            s.Callback = O =>
            {
                Console.WriteLine(O);
                e.Set();
            };

            Thread.Sleep(2000);

            c.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
            e.WaitOne();
        }

        [TestMethod]
        public void ReconnectToNewClient()
        {
            var c = new ClientMessagePipe("a");
            var s = new ServerMessagePipe("a");

            var e = new AutoResetEvent(false);

            s.Callback = O =>
            {
                Console.WriteLine(O);
                e.Set();
            };

            Thread.Sleep(2000);

            c.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
            e.WaitOne();

            c.Dispose();
            Thread.Sleep(2000);
            e.Reset();

            c = new ClientMessagePipe("a");
            Thread.Sleep(2000);

            c.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
            e.WaitOne();
        }

        [TestMethod]
        public void ReconnectToNewServer()
        {
            var c = new ClientMessagePipe("a");
            var s = new ServerMessagePipe("a");

            var e = new AutoResetEvent(false);

            s.Callback = O =>
            {
                Console.WriteLine(O);
                e.Set();
            };

            Thread.Sleep(2000);

            c.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
            e.WaitOne();

            s.Dispose();
            Thread.Sleep(2000);
            e.Reset();

            s = new ServerMessagePipe("a");
            s.Callback = O =>
            {
                Console.WriteLine(O);
                e.Set();
            };
            Thread.Sleep(2000);

            c.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
            e.WaitOne();
        }
    }

}
