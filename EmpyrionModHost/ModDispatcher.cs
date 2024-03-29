﻿using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EmpyrionModHost
{
    /// <summary>
    /// Zu ladenen Mods werden über die Datei "DllNames.txt" im Mod-Verzeichnis vom ModLoader definiert.
    /// - pro Zeile ein Pfad zu einer DLL Moddatei
    /// - Leerzeilen sind erlaubt
    /// </summary>
    public class ModDispatcher : ModInterface
    {
        string mDllNamesFileName { get; set; }
        public ModGameAPI GameAPI { get; set; }
        string[] mAssemblyFileNames { get; set; }
        List<ModInterface> mModInstance { get; set; } = new List<ModInterface>();
        public string CurrentModFile { get; private set; }

        public static string ProgramPath { get; private set; } = GetDirWith(Directory.GetCurrentDirectory(), "BuildNumber.txt");

        public event EventHandler GameExit;

        private static string GetDirWith(string aTestDir, string aTestFile)
        {
            return File.Exists(Path.Combine(aTestDir, aTestFile))
                ? aTestDir
                : GetDirWith(Path.GetDirectoryName(aTestDir), aTestFile);
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            Parallel.ForEach(mModInstance, async M => await SafeApiCall(() => M.Game_Event(eventId, seqNr, data), M, $"CmdId:{eventId} seqNr:{seqNr} data:{data}"));
        }

        public void Game_Exit()
        {
            try
            {
                var timeoutForExitCall = new CancellationTokenSource(10000).Token;
                Parallel.ForEach(mModInstance, new ParallelOptions { CancellationToken = timeoutForExitCall }, async M => await SafeApiCall(() => M.Game_Exit(), M, "Game_Exit"));
            }
            catch (Exception error) { GameAPI.Console_Write($"Game_Exit(ExitCall): {error}"); }

            try
            {
                var timeoutForExitHandler = new CancellationTokenSource(10000).Token;
                Task.Run(() => GameExit(this, null), timeoutForExitHandler);
            }
            catch (Exception error) { 
                GameAPI.Console_Write($"Game_Exit(ExitHandler): {error}");
                Environment.Exit(Environment.ExitCode);
            }
        }

        public void Game_Start(ModGameAPI dediAPI)
        {
            mDllNamesFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), "DllNames.txt");
            GameAPI = dediAPI;

            SynchronizationContext.SetSynchronizationContext(new AsyncSynchronizationContext(GameAPI));

            try
            {
                string CurrentDirectory = Directory.GetCurrentDirectory();
                GameAPI.Console_Write($"ModDispatcher(start): {mDllNamesFileName} in {CurrentDirectory}");

                if (!File.Exists(mDllNamesFileName)) File.WriteAllText(mDllNamesFileName, @"#..\[PathToDLLFile]");

                mAssemblyFileNames = File.ReadAllLines(mDllNamesFileName)
                    .Select(L => L.Trim())
                    .Where(L => !string.IsNullOrEmpty(L) && !L.StartsWith("#"))
                    .ToArray();

                Array.ForEach(mAssemblyFileNames, LoadAssembly);

                Directory.SetCurrentDirectory(ProgramPath);

                try{ Parallel.ForEach(mModInstance, async M => await SafeApiCall(() => M.Game_Start(GameAPI), M, "Game_Start")); }
                finally{ Directory.SetCurrentDirectory(CurrentDirectory); }

                GameAPI.Console_Write($"ModDispatcher(finish:{mModInstance.Count}): {mDllNamesFileName}");
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"ModDispatcher: {mDllNamesFileName} -> {Error}");
            }
        }

        private void LoadAssembly(string aFileName)
        {
            CurrentModFile = aFileName;
            AppDomain.CurrentDomain.AssemblyResolve += ModResolveEventHandler;
            string CurrentDirectory = Directory.GetCurrentDirectory();

            try
            {
                var ModFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), aFileName);

                GameAPI.Console_Write($"ModDispatcher: load {ModFilename} CurrentDir{CurrentDirectory} ProgDir:{ProgramPath}");
                Assembly Mod = TryLoadAssembly(ModFilename);

                var ModType = Mod.GetTypes().Where(T => T.GetInterfaces().Contains(typeof(ModInterface))).FirstOrDefault();
                if (ModType != null)
                {
                    Directory.SetCurrentDirectory(ProgramPath);
                    var ModInstance = Activator.CreateInstance(ModType) as ModInterface;
                    mModInstance.Add(ModInstance);
                    GameAPI.Console_Write($"ModDispatcher: loaded {ModFilename}");
                }
                else
                {
                    ModType = Mod.GetTypes().Where(T => T.GetInterfaces().Any(I => I.FullName.Contains(nameof(ModInterface)))).FirstOrDefault();
                    if (ModType != null)
                    {
                        GameAPI.Console_Write($"ModDispatcher: no class implements: {typeof(ModInterface).AssemblyQualifiedName}\n" +
                                                ModType.GetInterfaces()?.Aggregate("", (S, I) => S + I.AssemblyQualifiedName + "\n") +
                                                ModType.GetMethods()?.Aggregate("", (S, M) => S + M.Name + "\n"));
                    }
                    else
                    {
                        GameAPI.Console_Write($"ModDispatcher: no ModInterface class found: " +
                        Mod.GetTypes().Aggregate("", (S, T) => S + T.FullName + ":" + T.GetInterfaces()?.Aggregate("", (SS, I) => SS + I.FullName) + "\n"));
                    }
                }
            }
            catch (ReflectionTypeLoadException Error)
            {
                GameAPI.Console_Write($"ModDispatcher: {aFileName} -> {Error}");
                Array.ForEach(Error.LoaderExceptions, E => GameAPI.Console_Write($"ModDispatcher: {aFileName} -> LE:{E}"));
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"ModDispatcher: {aFileName} -> {Error}");
            }
            finally
            {
                Directory.SetCurrentDirectory(CurrentDirectory);
            }

            AppDomain.CurrentDomain.AssemblyResolve -= ModResolveEventHandler;
        }

        private Assembly TryLoadAssembly(string aAssembly)
        {
            try{
                var Result = Assembly.LoadFile(aAssembly);
                if (Result == null) return Result;
            }
            catch {}

            string CurrentDir = null;
            try
            {
                CurrentDir = Directory.GetCurrentDirectory();
                GameAPI.Console_Write($"Try load within: {CurrentDir} -> {Path.GetDirectoryName(aAssembly)}");
                Directory.SetCurrentDirectory(Path.GetDirectoryName(aAssembly));
                return Assembly.LoadFile(aAssembly);
            }
            finally
            {
                Directory.SetCurrentDirectory(CurrentDir);
            }
        }

        private Assembly ModResolveEventHandler(object aSender, ResolveEventArgs aArgs)
        {
            var Delimitter = aArgs.Name.IndexOf(',');
            var ModPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location),
                Path.GetDirectoryName(CurrentModFile), 
                (Delimitter > 0 ? aArgs.Name.Substring(0, Delimitter) : aArgs.Name) + ".dll");
            GameAPI.Console_Write($"ModResolveEventHandler1: {ModPath}");
            if (File.Exists(ModPath)) return Assembly.LoadFrom(ModPath);

            ModPath = Path.Combine(ProgramPath, @"DedicatedServer\EmpyrionDedicated_Data\Managed", Path.GetFileName(ModPath));
            GameAPI.Console_Write($"ModResolveEventHandler2: {ModPath}");
            if (File.Exists(ModPath)) return Assembly.LoadFrom(ModPath);

            throw new FileNotFoundException("Assembly not found", ModPath);
        }

        public void Game_Update()
        {
            Parallel.ForEach(mModInstance, async M => await SafeApiCall(() => M.Game_Update(), M, "Game_Update"));
        }

        private async Task SafeApiCall(Action aCall, ModInterface aMod, string aErrorInfo)
        {
            try
            {
                void SafeCall()
                {
                    try
                    {
                        aCall();
                    }
                    catch (Exception error)
                    {
                        GameAPI.Console_Write($"Exception [{aMod}] {aErrorInfo} => {error}");
                    }
                }

                await Task.Run(() => SafeCall());
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"Exception [{aMod}] {aErrorInfo} => {Error}");
            }
        }
    }
}