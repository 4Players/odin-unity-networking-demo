using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    public class OdinWorldUpdateMessage : OdinMessage
    {
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();
        
        public OdinWorldUpdateMessage() : base(OdinMessageType.WorldUpdate)
        {
            
        }
        
        public OdinWorldUpdateMessage(OdinNetworkReader reader) : base(reader)
        {
            MessageType = OdinMessageType.WorldUpdate;
            SyncVars = ReadSyncVars(reader);
            
            var numberOfNetworkedObjects = reader.ReadByte();
            for (var i = 0; i < numberOfNetworkedObjects; i++)
            {
                OdinUserDataManagedObject managedObject = OdinUserDataManagedObject.FromReader(reader);
                ManagedObjects.Add(managedObject);
            }
        }
        
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            
            WriteSyncVars(SyncVars, writer);
            
            writer.Write((byte)ManagedObjects.Count);
            foreach(var managedObject in ManagedObjects)
            {
                writer.Write(managedObject.ObjectId);
                writer.Write(managedObject.PrefabId);
                managedObject.Transform.ToWriter(writer);
                WriteSyncVars(managedObject.SyncVars, writer);
            }

            return writer;
        }
    }
}