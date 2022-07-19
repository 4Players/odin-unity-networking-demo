using UnityEngine;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// This message is used to spawn prefabs over the network (i.e. every client will spawn that prefab in the scene).
    /// </summary>
    class OdinSpawnPrefabMessage : OdinMessage
    {
        /// <summary>
        /// The PrefabId (i.e. the index in the spawnablePrefabs list of the OdinManager.
        /// </summary>
        public byte PrefabId;
        
        /// <summary>
        /// The objectid (unique to the owner)
        /// </summary>
        public byte ObjectId;
        
        /// <summary>
        /// The position where the prefab is located initially
        /// </summary>
        public Vector3 Position;
        
        /// <summary>
        /// The initial rotation of the prefab
        /// </summary>
        public Quaternion Rotation;
        
        /// <summary>
        /// Create an instance of the message
        /// </summary>
        /// <param name="prefabId">The PrefabId (i.e. the index in the spawnablePrefabs list of the OdinManager.</param>
        /// <param name="objectId">The objectid (unique to the owner)</param>
        /// <param name="position">The position where the prefab is located initially</param>
        /// <param name="rotation">The initial rotation of the prefab</param>
        public OdinSpawnPrefabMessage(byte prefabId, byte objectId, Vector3 position, Quaternion rotation): base(OdinMessageType.SpawnPrefab)
        {
            MessageType = OdinMessageType.SpawnPrefab;
            PrefabId = prefabId;
            ObjectId = objectId;
            Position = position;
            Rotation = rotation;
        }
        
        /// <summary>
        /// Deserialize the message from a reader
        /// </summary>
        /// <param name="reader">The reader with data from the network</param>
        public OdinSpawnPrefabMessage(OdinNetworkReader reader) : base(reader)
        {
            PrefabId = reader.ReadByte();
            ObjectId = reader.ReadByte();
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
        }
        
        /// <summary>
        /// Serialize the message to a writer
        /// </summary>
        /// <returns></returns>
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