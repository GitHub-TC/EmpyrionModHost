using EgsModClientDbTools;
using Eleon.Modding;
using ModExtenderCommunication;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace EmpyrionModClient
{
    public class Configuration
    {
        public string PathToModHost { get; set; } = @"..\Host\EmpyrionModHost.exe";
        public bool AutostartModHost { get; set; } = true;
        public int AutostartModHostAfterNSeconds { get; set; } = 10;
        public bool AutoshutdownModHost { get; set; } = true;
        public string EmpyrionToModPipeName { get; set; } = "EmpyrionToModPipe{0}";
        public string ModToEmpyrionPipeName { get; set; } = "ModToEmpyrionPipe{0}";
        public int HostProcessId { get; set; }
        public bool WithShellWindow { get; set; }
        public int GlobalStructureListUpdateIntervallInSeconds { get; set; } = 30;
    }

    public class EmpyrionModClient : ModInterface, IMod
    {
        public ModGameAPI GameAPI { get; private set; }
        public ClientMessagePipe OutServer { get; private set; }
        public ServerMessagePipe InServer { get; private set; }
        public Process mHostProcess { get; private set; }
        public DateTime? mHostProcessAlive { get; private set; }
        public static string ProgramPath { get; private set; } = GetDirWith(Directory.GetCurrentDirectory(), "BuildNumber.txt");
        public bool Exit { get; private set; }
        public bool ExposeShutdownHost { get; private set; }
        public IModApi ModAPI { get; private set; }

        Dictionary<Type, Action<object>> InServerMessageHandler;

        ConfigurationManager<Configuration> CurrentConfig;
        public AutoResetEvent GetGlobalStructureList { get; set; } = new AutoResetEvent(false);
        public ConcurrentQueue<EmpyrionGameEventData> GetGlobalStructureListEvents { get; set; } = new ConcurrentQueue<EmpyrionGameEventData>();
        public GlobalStructureListAccess GSL { get; private set; }

        private static string GetDirWith(string aTestDir, string aTestFile)
        {
            return File.Exists(Path.Combine(aTestDir, aTestFile))
                ? aTestDir
                : GetDirWith(Path.GetDirectoryName(aTestDir), aTestFile);
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            if (OutServer == null) return;

            try
            {
                var msg = new EmpyrionGameEventData() { eventId = eventId, seqNr = seqNr};
                msg.SetEmpyrionObject(data);
                OutServer.SendMessage(msg);
            }
            catch (System.Exception Error)
            {
                GameAPI.Console_Write($"ModClientDll: {Error.Message}");
            }
        }

        public void Game_Exit()
        {
            Exit = true;
            GameAPI.Console_Write($"ModClientDll: Game_Exit {CurrentConfig.Current.ModToEmpyrionPipeName}");
            OutServer?.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Exit });

            if (!ExposeShutdownHost && CurrentConfig.Current.AutoshutdownModHost && mHostProcess != null)
            {
                try
                {
                    try { mHostProcess.CloseMainWindow(); } catch { }
                    CurrentConfig.Current.HostProcessId = 0;
                    CurrentConfig.Save();

                    Thread.Sleep(1000);
                }
                catch (Exception Error)
                {
                    GameAPI.Console_Write($"ModClientDll: Game_Exit {Error}");
                }
            }

            InServer?.Close();
            OutServer?.Close();
        }

        public void Game_Start(ModGameAPI dediAPI)
        {
            GameAPI = dediAPI;
            GameAPI.Console_Write($"ModClientDll: start");
            
            CurrentConfig = new ConfigurationManager<Configuration>()
            {
                ConfigFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), "Configuration.xml")
            };
            CurrentConfig.Load();
            CurrentConfig.Current.EmpyrionToModPipeName = string.Format(CurrentConfig.Current.EmpyrionToModPipeName, Guid.NewGuid().ToString("N"));
            CurrentConfig.Current.ModToEmpyrionPipeName = string.Format(CurrentConfig.Current.ModToEmpyrionPipeName, Guid.NewGuid().ToString("N"));
            CurrentConfig.Save();

            GameAPI.Console_Write($"ModClientDll (CurrentDir:{Directory.GetCurrentDirectory()}): Config:{CurrentConfig.ConfigFilename}");

            InServerMessageHandler = new Dictionary<Type, Action<object>> {
                { typeof(EmpyrionGameEventData), M => HandleGameEvent               ((EmpyrionGameEventData)M) },
                { typeof(ClientHostComData    ), M => HandleClientHostCommunication ((ClientHostComData)M) },
                { typeof(ModComData           ), M => HandleModCommunication        ((ModComData)M) }
            };

            OutServer = new ClientMessagePipe(CurrentConfig.Current.EmpyrionToModPipeName) { log = GameAPI.Console_Write };
            InServer  = new ServerMessagePipe(CurrentConfig.Current.ModToEmpyrionPipeName) { log = GameAPI.Console_Write };
            InServer.Callback = Msg => {
                if (InServerMessageHandler.TryGetValue(Msg.GetType(), out Action<object> Handler)) Handler(Msg);
            };

            new Thread(() => { while (!Exit) { Thread.Sleep(1000); CheckHostProcess(); }}) { IsBackground = true }.Start();
            new Thread(() => ReadGlobalStructureInfoForEvent())                            { IsBackground = true }.Start();

            GameAPI.Console_Write($"ModClientDll: started");
        }

        private void ReadGlobalStructureInfoForEvent()
        {
            GSL = new EgsModClientDbTools.GlobalStructureListAccess();
            while (!Exit)
            {
                if (GetGlobalStructureList.WaitOne(1000))
                {
                    if (GetGlobalStructureListEvents.TryDequeue(out var TypedMsg))
                    {
                        GSL.UpdateIntervallInSeconds = CurrentConfig.Current.GlobalStructureListUpdateIntervallInSeconds;
                        GSL.GlobalDbPath = Path.Combine(ModAPI.Application.GetPathFor(AppFolder.SaveGame), "global.db");

                        switch (TypedMsg.eventId)
                        {
                            case CmdId.Request_GlobalStructure_List:                            Game_Event(TypedMsg.eventId, TypedMsg.seqNr, GSL.CurrentList); break;
                            case CmdId.Request_GlobalStructure_Update: GSL.UpdateNow = true;    Game_Event(TypedMsg.eventId, TypedMsg.seqNr, true); break;
                        }
                    }
                    GetGlobalStructureList.Reset();
                }
            }
        }

        private void StartHostProcess()
        {
            var HostFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), CurrentConfig.Current.PathToModHost);

            if (!File.Exists(HostFilename) || File.GetLastWriteTime(HostFilename) < File.GetLastWriteTime(HostFilename + ".bin"))
            {
                GameAPI.Console_Write($"Update/Create: '{HostFilename}'");
                File.Copy(HostFilename + ".bin", HostFilename, true);
            }

            GameAPI.Console_Write($"ModClientDll: start host '{HostFilename}'");
            mHostProcessAlive = null;

            if (CurrentConfig.Current.HostProcessId != 0)
            {
                try
                {
                    mHostProcess = Process.GetProcessById(CurrentConfig.Current.HostProcessId);
                    if (mHostProcess.MainWindowTitle != HostFilename) mHostProcess = null;
                }
                catch (Exception)
                {
                    mHostProcess = null;
                }
            }

            if (mHostProcess == null && CurrentConfig.Current.AutostartModHost && !string.IsNullOrEmpty(CurrentConfig.Current.PathToModHost))
            {
                if (!ExistsStopFile()) CreateHostProcess(HostFilename);
            }
        }

        private bool ExistsStopFile()
        {
            return File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), "stop.txt"));
        }

        private void CreateHostProcess(string HostFilename)
        {
            try
            {
                mHostProcess = new Process
                {
                    StartInfo = new ProcessStartInfo(HostFilename)
                    {
                        UseShellExecute = CurrentConfig.Current.WithShellWindow,
                        CreateNoWindow = true,
                        WorkingDirectory = ProgramPath,
                        Arguments = Environment.GetCommandLineArgs().Aggregate(
                            $"-EmpyrionToModPipe {CurrentConfig.Current.EmpyrionToModPipeName} -ModToEmpyrionPipe {CurrentConfig.Current.ModToEmpyrionPipeName}",
                            (C, A) => C + " " + A),
                    }
                };

                mHostProcess.Start();
                CurrentConfig.Current.HostProcessId = mHostProcess.Id;
                GameAPI.Console_Write($"ModClientDll: host started '{HostFilename} {mHostProcess?.StartInfo?.Arguments}' -> {mHostProcess.Id}");
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"ModClientDll: host start error '{HostFilename} -> {mHostProcess?.StartInfo?.Arguments} -> {Error}'");
                mHostProcess = null;
            }
        }

        void CheckHostProcess()
        {
            if (Exit) return;

            if (ExistsStopFile())
            {
                try
                {
                    if (mHostProcess != null && !mHostProcess.HasExited)
                    {
                        GameAPI.Console_Write($"ModClientDll: stop.txt found");

                        OutServer?.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Exit });
                        Thread.Sleep(1000);

                        GameAPI.Console_Write($"ModClientDll: stopped");
                    }
                }
                catch { }

                return;
            }

            if (CurrentConfig.Current.AutostartModHostAfterNSeconds == 0 || !CurrentConfig.Current.AutostartModHost) return;
            try { if (mHostProcess != null && !mHostProcess.HasExited) return; } catch { }

            if (!mHostProcessAlive.HasValue) mHostProcessAlive = DateTime.Now;
            if ((DateTime.Now - mHostProcessAlive.Value).TotalSeconds <= CurrentConfig.Current.AutostartModHostAfterNSeconds) return;

            mHostProcessAlive = null;

            StartHostProcess();
        }

        private void HandleModCommunication(ModComData msg)
        {
            if (OutServer == null) return;

            try
            {
                switch (msg.Command)
                {
                    case ModCommand.GetPathFor: OutServer.SendMessage(new ModComData() { Command = msg.Command, SequenceId = msg.SequenceId, Data = ModAPI.Application.GetPathFor((AppFolder)msg.Data) }); break;
                    case ModCommand.PlayfieldDataReceived:
                        {
                            var data = msg.Data as PlayfieldNetworkData;
                            ModAPI.Network.SendToPlayfieldServer(data.Sender, data.PlayfieldName, data.Data);
                            break;
                        }
                }
            }
            catch (System.Exception Error)
            {
                GameAPI.Console_Write($"ModClientDll: {Error.Message}");
            }
        }

        private void HandleClientHostCommunication(ClientHostComData aMsg)
        {
            switch (aMsg.Command)
            {
                case ClientHostCommand.RestartHost          : break;
                case ClientHostCommand.ExposeShutdownHost   : ExposeShutdownHost = true; break;
                case ClientHostCommand.Console_Write        : GameAPI.Console_Write(aMsg.Data as string); break;
            }
        }

        private void HandleGameEvent(EmpyrionGameEventData TypedMsg)
        {
            if (TypedMsg.eventId == CmdId.Request_GlobalStructure_List || TypedMsg.eventId == CmdId.Request_GlobalStructure_Update)
            {
                GetGlobalStructureListEvents.Enqueue(TypedMsg);
                GetGlobalStructureList.Set();
            }
            else if (TypedMsg.eventId == CmdId.Request_GlobalStructure_List + 100)
            {
                try
                {
                    GSL.GlobalDbPath = Path.Combine(ModAPI.Application.GetPathFor(AppFolder.SaveGame), "global.db");

                    var id = TypedMsg.GetEmpyrionObject() as Id;
                    Game_Event(TypedMsg.eventId, TypedMsg.seqNr, GSL.ReadGlobalStructureInfo(id));
                }
                catch (Exception error)
                {
                    GameAPI.Console_Write($"ModClientDll:Request_GlobalStructure_Info: {error}");
                }
            }
            else if (TypedMsg.eventId == CmdId.Event_ChatMessage + 100)
            {
                object receiveObject = null;
                try
                {
                    receiveObject = TypedMsg.GetEmpyrionObject<Eleon.MessageData>();
                    ModAPI.Application.SendChatMessage(receiveObject as Eleon.MessageData);
                    Game_Event(TypedMsg.eventId, TypedMsg.seqNr, true);
                }
                catch (Exception error)
                {
                    ModAPI.LogError($"SendChatMessage:[{receiveObject}] {error}");
                }
            }
            else GameAPI.Game_Request(TypedMsg.eventId, TypedMsg.seqNr, TypedMsg.GetEmpyrionObject());
        }

        public void Game_Update()
        {
            if (Exit) return;
            OutServer?.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
        }

        public void Init(IModApi modAPI)
        {
            ModAPI = modAPI;

            ModAPI.Network.RegisterReceiverForPlayfieldPackets(PlayfieldDataReceived);
        }

        private void PlayfieldDataReceived(string sender, string playfieldName, byte[] data)
        {
            OutServer?.SendMessage(new ModComData()
            {
                Command = ModCommand.PlayfieldDataReceived,
                Data = new PlayfieldNetworkData() { Sender = sender, PlayfieldName = playfieldName, Data = data }
            });
        }

        public void Shutdown()
        {
            OutServer?.SendMessage(new ModComData() { Command = ModCommand.Shutdown });
        }
    }
}