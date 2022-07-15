using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    public class OdinCommandMessage : OdinMessage
    {
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        public string Name;
        
        public OdinCommandMessage() : base(OdinMessageType.Command)
        {
        }
        
        public OdinCommandMessage(string name) : base(OdinMessageType.Command)
        {
            MessageType = OdinMessageType.Command;
            Name = name;
        }

        public OdinCommandMessage(OdinNetworkReader reader) : base(reader)
        {
            MessageType = OdinMessageType.Command;
            Name = reader.ReadString();
            SyncVars = ReadSyncVars(reader);
        }

        public void SetValue(string name, object value)
        {
            SyncVars.Add(new OdinUserDataSyncVar(name, value));
        }

        public object GetValue(string name)
        {
            foreach (var syncVar in SyncVars)
            {
                if (syncVar.Name == name)
                {
                    return syncVar.Value;
                }
            }

            return null;
        }
        
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(Name);
            WriteSyncVars(SyncVars, writer);
            return writer;
        }
    }
}