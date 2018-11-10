using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

namespace ModExtenderCommunication
{
    public class ClientMessagePipe : IDisposable
    {
        Thread mCommThread;
        Queue<object> mSendCommands = new Queue<object>();
        NamedPipeClientStream mClientPipe;
        public Action<string> log { get; set; }

        public string PipeName { get; }
        public Action LoopPing { get; set; }
        public bool Exit { get; private set; }

        public ClientMessagePipe(string aPipeName)
        {
            PipeName = aPipeName;
            mCommThread = new Thread(CommunicationLoop);
            mCommThread.Start();
        }

        private void CommunicationLoop()
        {
            while (!Exit)
            {
                try
                {
                    if (mClientPipe == null || !mClientPipe.IsConnected)
                    {
                        do
                        {
                            try
                            {
                                log?.Invoke($"Try CommunicationLoop Connect {PipeName} Connected:{mClientPipe?.IsConnected}");
                                LoopPing?.Invoke();
                                if (mClientPipe != null) mClientPipe.Dispose();
                                mClientPipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                                mClientPipe.Connect(1000);
                            }
                            catch (IOException)         { Thread.Sleep(1000); }
                            catch (TimeoutException)    { Thread.Sleep(1000); }
                        } while ((mClientPipe == null || !mClientPipe.IsConnected) && !Exit);
                    }

                    var Formatter = new BinaryFormatter();

                    try
                    {
                        do
                        {
                            object SendMessage = null;

                            lock (mSendCommands)
                            {
                                if (mSendCommands.Count == 0) Monitor.Wait(mSendCommands);
                                if (mSendCommands.Count > 0) SendMessage = mSendCommands.Dequeue();
                            }

                            if (SendMessage != null && mClientPipe != null && mClientPipe.IsConnected && !Exit)
                            {
                                using (var MemBuffer = new MemoryStream())
                                {
                                    try
                                    {
                                        Formatter.Serialize(MemBuffer, SendMessage);

                                        var Size = MemBuffer.Length;
                                        mClientPipe.WriteByte((byte)(Size & 0xff));
                                        mClientPipe.WriteByte((byte)(Size >> 8  & 0xff));
                                        mClientPipe.WriteByte((byte)(Size >> 16 & 0xff));
                                        mClientPipe.WriteByte((byte)(Size >> 24 & 0xff));

                                        mClientPipe.Write(MemBuffer.ToArray(), 0, (int)MemBuffer.Length);
                                        mClientPipe.Flush();
                                    }
                                    catch (SerializationException Error)
                                    {
                                        log?.Invoke("Failed to serialize. Reason: " + Error.Message);
                                        mClientPipe.Close();
                                        mClientPipe = null;
                                    }
                                    catch (Exception Error)
                                    {
                                        log?.Invoke($"CommError {PipeName} Connected:{mClientPipe?.IsConnected} Reason: {Error.Message}");
                                        mClientPipe.Close();
                                        mClientPipe = null;
                                    }
                                }
                            }
                        } while (mClientPipe != null && mClientPipe.IsConnected);
                    }
                    catch (ThreadAbortException) { }
                    catch
                    {
                        if(!Exit) Thread.Sleep(10000);
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception Error)
                {
                    log?.Invoke($"MainError {PipeName} Connected:{mClientPipe?.IsConnected} Reason: {Error.Message}");
                    if(!Exit) Thread.Sleep(10000);
                }
            }

            if (mClientPipe != null && mClientPipe.IsConnected) mClientPipe.Dispose();
        }


        public void SendMessage(object aMessage)
        {
            if (mClientPipe == null || !mClientPipe.IsConnected) return;
            lock (mSendCommands)
            {
                mSendCommands.Enqueue(aMessage);
                Monitor.PulseAll(mSendCommands);
            }
        }

        public void Close()
        {
            try
            {
                Exit = true;
                lock (mSendCommands) Monitor.PulseAll(mSendCommands);
            }
            catch (Exception Error)
            {
                log?.Invoke($"CloseError:mCommThread {Error}");
            }
            try
            {
                lock (mSendCommands) Monitor.PulseAll(mSendCommands);
            }
            catch (Exception Error)
            {
                log?.Invoke($"CloseError:mSendCommands {Error}");
            }
            try
            {
                mClientPipe?.Close(); mClientPipe = null;
            }
            catch (Exception Error)
            {
                log?.Invoke($"CloseError:mClientPipe {Error}");
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}