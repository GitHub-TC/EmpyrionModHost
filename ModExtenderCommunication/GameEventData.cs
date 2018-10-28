using Eleon.Modding;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace ModExtenderCommunication
{

    [Serializable]
    public class EmpyrionGameEventData
    {
        public CmdId eventId;
        public ushort seqNr;
        [NonSerialized]
        public object data;

        byte[] serializedData;
        Type serializedDataType;

        class ProtoBufCall<T>
        {
            public void Serialize(Stream aStream, T aData) { ProtoBuf.Serializer.Serialize<T>(aStream, (T)aData); }
            public T Deserialize(Stream aStream) { return ProtoBuf.Serializer.Deserialize<T>(aStream); }
        }

        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            if (data == null) return;

            using (var MemBuffer = new MemoryStream())
            {
                Type TypedProtoBufCall = typeof(ProtoBufCall<>);
                serializedDataType = data.GetType();
                TypedProtoBufCall = TypedProtoBufCall.MakeGenericType(new[] { serializedDataType });

                object ProtoBufCallInstance = Activator.CreateInstance(TypedProtoBufCall);
                MethodInfo MI = TypedProtoBufCall.GetMethod("Serialize");
                MI.Invoke(ProtoBufCallInstance, new[] { MemBuffer, data });

                MemBuffer.Seek(0, SeekOrigin.Begin);
                serializedData = MemBuffer.ToArray();
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (serializedData == null) return;

            using (var MemBuffer = new MemoryStream(serializedData))
            {
                Type TypedProtoBufCall = typeof(ProtoBufCall<>);
                TypedProtoBufCall = TypedProtoBufCall.MakeGenericType(new[] { serializedDataType });

                object ProtoBufCallInstance = Activator.CreateInstance(TypedProtoBufCall);
                MethodInfo MI = TypedProtoBufCall.GetMethod("Deserialize");
                data = MI.Invoke(ProtoBufCallInstance, new[] { MemBuffer });
            }
        }
    }
}