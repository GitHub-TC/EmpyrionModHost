using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;

namespace ModExtenderCommunication
{
    public class ServerMessagePipe : IDisposable
    {
        byte[] mMessageBuffer = new byte[2048];
        NamedPipeServerStream mServerPipe;
        Thread mServerCommThread;

        public Action<string> log { get; set; }
        public string PipeName { get; }
        public Action<object> Callback { get; set; }
        public ServerMessagePipe(string aPipeName)
        {
            PipeName = aPipeName;
            mServerCommThread = new Thread(ServerCommunicationLoop);
            mServerCommThread.Start();
        }

        private void ServerCommunicationLoop()
        {
            var ShownErrors = new List<string>();
            while (mServerCommThread.ThreadState != ThreadState.AbortRequested)
            {
                try
                {
                    ExecServerCommunication();
                }
                catch (ThreadAbortException) { return; }
                catch (Exception Error)
                {
                    if (!ShownErrors.Contains(Error.Message))
                    {
                        ShownErrors.Add(Error.Message);
                        log?.Invoke($"Failed ExecServerCommunication. {PipeName} Reason: " + Error.Message);
                    }
                }
            }
        }

        private void ExecServerCommunication()
        {
            using (mServerPipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                mServerPipe.WaitForConnection();
                log?.Invoke($"ServerPipe: {PipeName} connected");
                while (mServerPipe.IsConnected && mServerCommThread.ThreadState != ThreadState.AbortRequested)
                {
                    var Message = ReadNextMessage();
                    Callback?.Invoke(Message);
                }
            }
        }

        private object ReadNextMessage()
        {
            using (var MemBuffer = new MemoryStream())
            {
                var MessageLength = 0;
                var Size = mServerPipe.ReadByte();
                Size += mServerPipe.ReadByte() << 8;
                Size += mServerPipe.ReadByte() << 16;
                Size += mServerPipe.ReadByte() << 24;

                if (mMessageBuffer.Length < Size) mMessageBuffer = new byte[Size];

                do
                {
                    var BytesRead = mServerPipe.Read(mMessageBuffer, 0, Size);
                    MessageLength += BytesRead;
                    MemBuffer.Write(mMessageBuffer, 0, BytesRead);
                }
                while (MessageLength < Size && mServerCommThread.ThreadState != ThreadState.AbortRequested);

                if (MessageLength == 0) return null;

                try
                {
                    MemBuffer.Seek(0, SeekOrigin.Begin);
                    return new BinaryFormatter().Deserialize(MemBuffer);
                }
                catch (Exception Error)
                {
                    log?.Invoke("Failed ReadNextMessage. Reason: " + Error.Message);
                    return null;
                }
            }
        }

        public void Close()
        {
            try
            {
                mServerCommThread?.Abort();

                new Thread(() => {
                    try
                    {
                        var ClientPipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                        ClientPipe.Connect(1);
                        ClientPipe.Close();
                    }
                    catch { }
                }).Start();

                mServerCommThread = null;
                mServerPipe?.Close();mServerPipe = null;
            }
            catch (Exception Error)
            {
                log?.Invoke($"CloseError {Error}");
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}