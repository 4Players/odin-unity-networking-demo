using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    public struct OdinUserDataManagedObject
    {
        public byte ObjectId;
        public byte PrefabId;
        public OdinUserDataTransform Transform;
        public List<OdinUserDataSyncVar> SyncVars;

        public OdinUserDataManagedObject(byte objectId, byte prefabId, OdinUserDataTransform transform)
        {
            ObjectId = objectId;
            PrefabId = prefabId;
            SyncVars = new List<OdinUserDataSyncVar>();
            Transform = transform;
        }
        
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
        
        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(ObjectId);
            writer.Write(PrefabId);
            Transform.ToWriter(writer);
            OdinMessage.WriteSyncVars(SyncVars, writer);
        }
    }
}