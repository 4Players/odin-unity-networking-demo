namespace Odin.OdinNetworking.Messages
{
    public class OdinUpdateWorldSyncVarMessage: OdinMessage
    {
        public string SyncVarName;
        public object SyncVarValue;
        
        public OdinUpdateWorldSyncVarMessage(string syncVarName, object syncVarValue)
        {
            SyncVarName = syncVarName;
            SyncVarValue = syncVarValue;
        }

        public OdinUpdateWorldSyncVarMessage(OdinNetworkReader reader) : base(reader)
        {
            SyncVarName = reader.ReadString();
            SyncVarValue = reader.ReadObject();
        }

        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(SyncVarName);
            writer.Write(SyncVarValue);
            return writer;
        }
    }
}