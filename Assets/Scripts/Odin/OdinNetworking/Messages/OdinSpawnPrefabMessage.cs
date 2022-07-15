using UnityEngine;

namespace Odin.OdinNetworking.Messages
{
    class OdinSpawnPrefabMessage : OdinMessage
    {
        public byte PrefabId;
        public byte ObjectId;
        public Vector3 Position;
        public Quaternion Rotation;
        
        public OdinSpawnPrefabMessage(byte prefabId, byte objectId, Vector3 position, Quaternion rotation): base(OdinMessageType.SpawnPrefab)
        {
            MessageType = OdinMessageType.SpawnPrefab;
            PrefabId = prefabId;
            ObjectId = objectId;
            Position = position;
            Rotation = rotation;
        }
        
        public OdinSpawnPrefabMessage(OdinNetworkReader reader) : base(reader)
        {
            PrefabId = reader.ReadByte();
            ObjectId = reader.ReadByte();
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
        }
        
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(PrefabId);
            writer.Write(ObjectId);
            writer.Write(Position);
            writer.Write(Rotation);
            return writer;
        }
    }
}