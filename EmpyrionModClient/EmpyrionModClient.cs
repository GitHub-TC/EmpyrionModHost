﻿using Eleon.Modding;
using ModExtenderCommunication;
using System;
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
        public int AutostartModHostAfterNSeconds { get; set; } = 30;
        public bool AutoshutdownModHost { get; set; } = true;
        public string EmpyrionToModPipeName { get; set; } = "EmpyrionToModPipe{0}";
        public string ModToEmpyrionPipeName { get; set; } = "ModToEmpyrionPipe{0}";
        public int HostProcessId { get; set; }
        public bool WithShellWindow { get; set; }
    }

    public class EmpyrionModClient : ModInterface
    {
        public ModGameAPI GameAPI { get; private set; }
        public ClientMessagePipe OutServer { get; private set; }
        public ServerMessagePipe InServer { get; private set; }
        public Process mHostProcess { get; private set; }
        public DateTime? mHostProcessAlive { get; private set; }
        public static string ProgramPath { get; private set; } = Directory.GetCurrentDirectory();
        public bool WithinExit { get; private set; }
        public bool ExposeShutdownHost { get; private set; }

        Dictionary<Type, Action<object>> InServerMessageHandler;

        ConfigurationManager<Configuration> CurrentConfig;
        private Thread mGame_Update_CheckHostProcess;

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            if (OutServer == null) return;

            try
            {
                //GameAPI.Console_Write($"ModClientDll: eventId:{eventId} seqNr:{seqNr} data:{data}");
                OutServer.SendMessage(new EmpyrionGameEventData() { eventId = eventId, seqNr = seqNr, data = data });
                //GameAPI.Console_Write($"ModClientDll: send");
            }
            catch (System.Exception Error)
            {
                GameAPI.Console_Write($"ModClientDll: {Error.Message}");
            }
        }

        public void Game_Exit()
        {
            WithinExit = true;
            OutServer?.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Exit });

            if (!ExposeShutdownHost && CurrentConfig.Current.AutoshutdownModHost && mHostProcess != null)
            {
                try
                {
                    mHostProcess.CloseMainWindow();
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

            GameAPI.Console_Write($"ModClientDll (CurrentDir:{Directory.GetCurrentDirectory()}): Config:{CurrentConfig.ConfigFilename}");

            InServerMessageHandler = new Dictionary<Type, Action<object>> {
                { typeof(EmpyrionGameEventData), M => HandleGameEvent               ((EmpyrionGameEventData)M) },
                { typeof(ClientHostComData    ), M => HandleClientHostCommunication ((ClientHostComData)M) }
            };

            try
            {
                CurrentConfig.Current.EmpyrionToModPipeName = string.Format(CurrentConfig.Current.EmpyrionToModPipeName, Guid.NewGuid().ToString("N"));
                CurrentConfig.Current.ModToEmpyrionPipeName = string.Format(CurrentConfig.Current.ModToEmpyrionPipeName, Guid.NewGuid().ToString("N"));

                OutServer = new ClientMessagePipe(CurrentConfig.Current.EmpyrionToModPipeName) { log = GameAPI.Console_Write };
                OutServer.LoopPing = CheckHostProcess;

                StartModToEmpyrionPipe();
                StartHostProcess();

                CurrentConfig.Save();
                GameAPI.Console_Write($"ModClientDll: started");
            }
            catch (System.Exception Error)
            {
                GameAPI.Console_Write($"ModClientDll: {Error.Message}");
            }
        }

        private void StartHostProcess()
        {
            var HostFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), CurrentConfig.Current.PathToModHost);
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
                try
                {
                    mHostProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo(HostFilename)
                        {
                            UseShellExecute  = CurrentConfig.Current.WithShellWindow,
                            CreateNoWindow   = true,
                            WorkingDirectory = ProgramPath,
                            Arguments = Environment.GetCommandLineArgs().Aggregate(
                                $"-EmpyrionToModPipe {CurrentConfig.Current.EmpyrionToModPipeName} -ModToEmpyrionPipe {CurrentConfig.Current.ModToEmpyrionPipeName}",
                                (C, A) => C + " " + A),
                        }
                    };

                    mHostProcess.Start();
                    CurrentConfig.Current.HostProcessId = mHostProcess.Id;
                    GameAPI.Console_Write($"ModClientDll: host started '{HostFilename}/{mHostProcess.Id}'");
                }
                catch (Exception Error)
                {
                    GameAPI.Console_Write($"ModClientDll: host start error '{HostFilename} -> {Error}'");
                    mHostProcess = null;
                }
            }
        }

        void CheckHostProcess()
        {
            if (WithinExit || CurrentConfig.Current.AutostartModHostAfterNSeconds == 0 || !CurrentConfig.Current.AutostartModHost) return;
            try { if (mHostProcess != null && !mHostProcess.HasExited) return; } catch { }

            if (!mHostProcessAlive.HasValue) mHostProcessAlive = DateTime.Now;
            if ((DateTime.Now - mHostProcessAlive.Value).TotalSeconds <= CurrentConfig.Current.AutostartModHostAfterNSeconds) return;

            mHostProcessAlive = null;

            StartModToEmpyrionPipe();
            StartHostProcess();
        }

        private void StartModToEmpyrionPipe()
        {
            GameAPI.Console_Write($"StartModToEmpyrionPipe: start {CurrentConfig.Current.ModToEmpyrionPipeName}");
            InServer?.Close();

            InServer = new ServerMessagePipe(CurrentConfig.Current.ModToEmpyrionPipeName) { log = Console.WriteLine };
            InServer.Callback = Msg => {
                if (InServerMessageHandler.TryGetValue(Msg.GetType(), out Action<object> Handler)) Handler(Msg);
            };

            GameAPI.Console_Write($"StartModToEmpyrionPipe: startet {CurrentConfig.Current.ModToEmpyrionPipeName}");
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
            GameAPI.Game_Request(TypedMsg.eventId, TypedMsg.seqNr, TypedMsg.data);
        }

        public void Game_Update()
        {
            if (WithinExit) return;

            if (mGame_Update_CheckHostProcess == null || !mGame_Update_CheckHostProcess.IsAlive) { 
                mGame_Update_CheckHostProcess = new Thread(() => { Thread.Sleep(1000); CheckHostProcess(); });
                mGame_Update_CheckHostProcess.Start();
            }

            OutServer?.SendMessage(new ClientHostComData() { Command = ClientHostCommand.Game_Update });
        }
    }
}