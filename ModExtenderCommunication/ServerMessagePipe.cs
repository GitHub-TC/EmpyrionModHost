using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

namespace ModExtenderCommunication
{
    public static class NamedPipeExtensions{
        public static void WaitForConnectionEx(this NamedPipeServerStream aStream)
        {
            Exception Error = null;
            AutoResetEvent aAutoResetEvent = new AutoResetEvent(false);
            aStream.BeginWaitForConnection(A =>
            {
                try { aStream.EndWaitForConnection(A); }
                catch (Exception WaitError) { Error = WaitError; }
                finally { aAutoResetEvent.Set(); }
            }, null);
            aAutoResetEvent.WaitOne();
            if (Error != null) throw Error; // rethrow exception
        }
    }

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
            while (mServerCommThread.ThreadState != ThreadState.AbortRequested)
            {
                try
                {
                    ExecServerCommunication();
                }
                catch (ThreadAbortException) { return; }
                catch (Exception Error)
                {
                    log?.Invoke($"Failed ExecServerCommunication. {PipeName} Reason: " + Error.Message);
                }
            }
        }

        private void ExecServerCommunication()
        {
            using (mServerPipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                mServerPipe.WaitForConnectionEx();
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
                while (MessageLength < Size);

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
                mServerCommThread?.Abort(); mServerCommThread = null;
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