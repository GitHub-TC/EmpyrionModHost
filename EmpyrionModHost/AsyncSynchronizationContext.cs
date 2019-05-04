using Eleon.Modding;
using System;
using System.Threading;

namespace EmpyrionModHost
{
    public class AsyncSynchronizationContext : SynchronizationContext
    {
        public ModGameAPI GameAPI { get; set; }

        public AsyncSynchronizationContext(ModGameAPI gameAPI)
        {
            GameAPI = gameAPI;
        }

        public override void Send(SendOrPostCallback action, object state)
        {
            try
            {
                action(state);
            }
            catch (Exception ex)
            {
                GameAPI?.Console_Write($"AsyncSynchronizationContext:Send:Exception {ex} {state}");
            }
        }

        public override void Post(SendOrPostCallback action, object state)
        {
            try
            {
                action(state);
            }
            catch (Exception ex)
            {
                GameAPI?.Console_Write($"AsyncSynchronizationContext:Post:Exception {ex} {state}");
            }
        }
    }
}
