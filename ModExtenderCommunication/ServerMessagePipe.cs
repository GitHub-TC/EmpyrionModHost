﻿using System;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using SharedMemory;

namespace ModExtenderCommunication
{
    public class ServerMessagePipe : IDisposable
    {
        CircularBuffer mServer;
        Thread mServerCommThread;
        private byte[] mDataBuffer;
        DateTime? mLastPing;

        public Action<string> log { get; set; }
        public string PipeName { get; }
        public Action<object> Callback { get; set; }
        public bool Exit { get; private set; }
        public bool Connected { get; private set; }

        public ServerMessagePipe(string aPipeName)
        {
            PipeName = aPipeName;
            mServerCommThread = new Thread(ServerCommunicationLoop);
            mServerCommThread.Start();
        }

        private void ServerCommunicationLoop()
        {
            var ShownErrors = new List<string>();
            var tryCount = 1;
            while (!Exit)
            {
                try
                {
                    ExecServerCommunication();
                }
                catch (ThreadAbortException) { return; }
                catch (FileNotFoundException Error)
                {
                    if (tryCount++ % 60 == 0) log?.Invoke($"Try to connect ExecServerCommunication. {PipeName} Reason: " + Error);

                    if (!Exit) Thread.Sleep(1000);
                }
                catch (Exception Error)
                {
                    if (!ShownErrors.Contains(Error.Message))
                    {
                        ShownErrors.Add(Error.Message);
                        log?.Invoke($"Failed ExecServerCommunication. {PipeName} Reason: " + Error);
                    }

                    if (!Exit) Thread.Sleep(1000);
                }
            }
        }

        private void ExecServerCommunication()
        {
            mLastPing = null;
            using (mServer = new CircularBuffer(PipeName))
            {
                long bufferSize = mServer.NodeBufferSize;

                log?.Invoke($"ServerPipe: {PipeName} connected {bufferSize}");
                if (Exit) return;
                if(bufferSize == 0)
                {
                    Thread.Sleep(1000);
                    return;
                }

                mDataBuffer = new byte[bufferSize];
                Connected = true;

                while (!Exit)
                {
                    var Message = ReadNextMessage();
                    if (!mLastPing.HasValue || Message != null) mLastPing = DateTime.Now;
                    if (Message != null) Callback?.Invoke(Message);

                    if ((DateTime.Now - mLastPing.Value).TotalSeconds > 5) return;
                }

                Connected = false;
            }
        }

        private object ReadNextMessage()
        {
            try
            {
                using (var memStream = new MemoryStream())
                {
                    var dataLength = mServer.Read(mDataBuffer);
                    if(dataLength == 0) return null;

                    memStream.Write(mDataBuffer, 0, dataLength);
                    memStream.Seek(0, SeekOrigin.Begin);
                    return new BinaryFormatter().Deserialize(memStream);
                }
            }
            catch (Exception Error)
            {
                log?.Invoke("Failed ReadNextMessage. Reason: " + Error.Message);
                return null;
            }
        }

        public void Close()
        {
            try
            {
                Exit = true;
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