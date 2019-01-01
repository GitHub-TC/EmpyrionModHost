using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public event EventHandler GameExit;

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            Parallel.ForEach(mModInstance, M => SaveApiCall(() => M.Game_Event(eventId, seqNr, data), M, $"CmdId:{eventId} seqNr:{seqNr} data:{data}"));
        }

        public void Game_Exit()
        {
            Parallel.ForEach(mModInstance, M => SaveApiCall(() => M.Game_Exit(), M, "Game_Exit"));
            GameExit(this, null);
        }

        public void Game_Start(ModGameAPI dediAPI)
        {
            mDllNamesFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), "DllNames.txt");
            GameAPI = dediAPI;
            try
            {
                GameAPI.Console_Write($"ModDispatcher(start): {mDllNamesFileName}");

                mAssemblyFileNames = File.ReadAllLines(mDllNamesFileName)
                    .Select(L => L.Trim())
                    .Where(L => !string.IsNullOrEmpty(L) && !L.StartsWith("#"))
                    .ToArray();

                Array.ForEach(mAssemblyFileNames, LoadAssembly);

                Parallel.ForEach(mModInstance, M => SaveApiCall(() => M.Game_Start(GameAPI), M, "Game_Start"));

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

            try
            {
                var ModFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), aFileName);

                GameAPI.Console_Write($"ModDispatcher: load {ModFilename}");
                Assembly Mod = TryLoadAssembly(ModFilename);

                var ModType = Mod.GetTypes().Where(T => T.GetInterfaces().Contains(typeof(ModInterface))).FirstOrDefault();
                if (ModType != null)
                {
                    var ModInstance = Activator.CreateInstance(ModType) as ModInterface;
                    mModInstance.Add(ModInstance);
                    GameAPI.Console_Write($"ModDispatcher: loaded {ModFilename}");
                }
                else GameAPI.Console_Write($"ModDispatcher: no ModInterface class found");
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
            GameAPI.Console_Write($"ModResolveEventHandler: {ModPath}");
            if (File.Exists(ModPath)) return Assembly.LoadFrom(ModPath);

            throw new FileNotFoundException("Assembly not found", ModPath);
        }

        public void Game_Update()
        {
            Parallel.ForEach(mModInstance, M => SaveApiCall(() => M.Game_Update(), M, "Game_Update"));
        }

        private void SaveApiCall(Action aCall, ModInterface aMod, string aErrorInfo)
        {
            try
            {
                aCall();
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"Exception [{aMod}] {aErrorInfo} => {Error}");
            }
        }
    }
}