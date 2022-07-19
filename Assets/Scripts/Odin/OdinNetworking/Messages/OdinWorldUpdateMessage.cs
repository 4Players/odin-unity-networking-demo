using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// A message containing the serialized (networked) world 
    /// </summary>
    public class OdinWorldUpdateMessage : OdinMessage
    {
        /// <summary>
        /// The sync vars of the world script
        /// </summary>
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        /// <summary>
        /// Managed objects handled by the world (i.e. static objects that are part of the world)
        /// </summary>
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();
        
        /// <summary>
        /// Create an instance of the message
        /// </summary>
        public OdinWorldUpdateMessage() : base(OdinMessageType.WorldUpdate)
        {
            
        }
        
        /// <summary>
        /// Deserialize the parameters from the reader
        /// </summary>
        /// <param name="reader">The reader from which to read the params</param>
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
        
        /// <summary>
        /// Serialize the message to the writer
        /// </summary>
        /// <returns>The writer to which the message has been written.</returns>
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