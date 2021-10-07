using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ModExtenderCommunication
{
    [Serializable]
    public class ComData
    {
        public Guid SequenceId { get; set; }
        public object Content { get; set; }
    }


    public static class SendReceiveMessage
    {
        static private ConcurrentDictionary<Guid, object> PendingCalls { get; set; } = new ConcurrentDictionary<Guid, object>();

        static Tuple<Guid, Task<T>> GetNewTaskCompletionSource<T>()
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            var guid = Guid.NewGuid();
            PendingCalls.TryAdd(guid, taskCompletionSource);

            return new Tuple<Guid, Task<T>>(guid, taskCompletionSource.Task);
        }

        static bool TryHandleEvent(ComData data)
        {
            bool trackingIdFound = PendingCalls.TryRemove(data.SequenceId, out object taskCompletionSource);
            if (!trackingIdFound) return false;

            if (data.Content is Exception error)
            {
                System.Reflection.MethodInfo setException = taskCompletionSource.GetType().GetMethod("SetException", new Type[] { typeof(Exception) });
                setException.Invoke(taskCompletionSource, new[] { error });
            }
            else
            {
                System.Reflection.MethodInfo setResult = taskCompletionSource.GetType().GetMethod("SetResult");
                try { setResult.Invoke(taskCompletionSource, new[] { data.Content }); }
                catch (Exception setError)
                {
                    System.Reflection.MethodInfo setException = taskCompletionSource.GetType().GetMethod("SetException", new Type[] { typeof(Exception) });
                    setException.Invoke(taskCompletionSource, new[] { setError });
                }
            }

            return true;
        }

        public static Task<R> Call<T, R>(this ClientMessagePipe sender, T message)
        {
            var send = GetNewTaskCompletionSource<R>();
            sender.SendMessage(new ComData() { SequenceId = send.Item1, Content = message });
            return send.Item2;
        }

        public static void Receive(this ClientMessagePipe sender, ComData message)
        {
            sender.SendMessage(message);
        }

    }
}
