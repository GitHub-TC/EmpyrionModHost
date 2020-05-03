using System;

namespace ModExtenderCommunication
{
    public enum ModCommand
    {
        Shutdown,
        GetPathFor,
        PlayfieldDataReceived
    }

    [Serializable]
    public class ModComData
    {
        public ModCommand Command { get; set; }
        public Guid SequenceId { get; set; }
        public object Data { get; set; }
    }

    [Serializable]
    public class PlayfieldNetworkData
    {
        public string Sender { get; set; }
        public string PlayfieldName { get; set; }
        public byte[] Data { get; set; }
    }


}
