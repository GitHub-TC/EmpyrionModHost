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
        public event EventHandler GameExit;

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            Parallel.ForEach(mModInstance, M => M.Game_Event(eventId, seqNr, data));
        }

        public void Game_Exit()
        {
            Parallel.ForEach(mModInstance, M => M.Game_Exit());
            GameExit?.Invoke(this, null);
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

                Parallel.ForEach(mModInstance, M => M.Game_Start(GameAPI));

                GameAPI.Console_Write($"ModDispatcher(finish:{mModInstance.Count}): {mDllNamesFileName}");
            }
            catch (Exception Error)
            {
                GameAPI.Console_Write($"ModDispatcher: {mDllNamesFileName} -> {Error}");
            }
        }

        private void LoadAssembly(string aFileName)
        {
            try
            {
                var ModFilename = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location), aFileName);

                GameAPI.Console_Write($"ModDispatcher: load {ModFilename}");
                var Mod = Assembly.LoadFile(ModFilename);
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
        }

        public void Game_Update()
        {
            Parallel.ForEach(mModInstance, M => M.Game_Update());
        }
    }
}