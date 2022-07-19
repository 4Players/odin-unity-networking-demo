using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// A managed networked object that is serialized over the network. 
    /// </summary>
    public struct OdinUserDataManagedObject
    {
        /// <summary>
        /// The object id (unique to the owner)
        /// </summary>
        public byte ObjectId;
        
        /// <summary>
        /// The PrefabId is the index of the prefab defined in the spawnablePrefabs of the Network Manager.
        /// </summary>
        public byte PrefabId;
        
        /// <summary>
        /// The transform struct of the object which encodes position, scale and rotation.
        /// </summary>
        public OdinUserDataTransform Transform;
        
        /// <summary>
        /// The list of sync vars available of the managed object
        /// </summary>
        public List<OdinUserDataSyncVar> SyncVars;

        /// <summary>
        /// Create an instance of the struct.
        /// </summary>
        /// <param name="objectId">The object id (unique to the owner)</param>
        /// <param name="prefabId">The PrefabId is the index of the prefab defined in the spawnablePrefabs of the Network Manager.</param>
        /// <param name="transform">The transform struct of the object which encodes position, scale and rotation.</param>
        public OdinUserDataManagedObject(byte objectId, byte prefabId, OdinUserDataTransform transform)
        {
            ObjectId = objectId;
            PrefabId = prefabId;
            SyncVars = new List<OdinUserDataSyncVar>();
            Transform = transform;
        }
        
        /// <summary>
        /// Deserialize an instance of this message from the reader
        /// </summary>
        /// <param name="reader">The reader with data received from the network</param>
        /// <returns>An instance with property values serialized from the reader</returns>
        public static OdinUserDataManagedObject FromReader(OdinNetworkReader reader)
        {
            var objectId = reader.ReadByte();
            var prefabId = reader.ReadByte();
            var transform = OdinUserDataTransform.FromReader(reader);
            OdinUserDataManagedObject managedObject = new OdinUserDataManagedObject(objectId, prefabId, transform);
            var syncVars = OdinMessage.ReadSyncVars(reader);
            managedObject.SyncVars = syncVars;
            return managedObject;
        }
        
        /// <summary>
        /// Writes this struct to the writer.
        /// </summary>
        /// <param name="writer">The writer in which this struct should be written</param>
        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(ObjectId);
            writer.Write(PrefabId);
            Transform.ToWriter(writer);
            OdinMessage.WriteSyncVars(SyncVars, writer);
        }
    }
}