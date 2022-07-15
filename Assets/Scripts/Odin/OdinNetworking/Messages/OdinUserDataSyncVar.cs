namespace Odin.OdinNetworking.Messages
{
    public struct OdinUserDataSyncVar
    {
        public string Name;
        public object Value;

        public OdinUserDataSyncVar(string name, object value)
        {
            Name = name;
            Value = value;
        }
        
        public static OdinUserDataSyncVar FromReader(OdinNetworkReader reader)
        {
            var syncVarName = reader.ReadString();
            var receivedValue = reader.ReadObject();
            return new OdinUserDataSyncVar(syncVarName, receivedValue);
        }

        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(Name);
            writer.Write(Value);
        }
    }
}