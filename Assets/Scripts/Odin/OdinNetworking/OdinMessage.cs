namespace Odin.OdinNetworking
{
    // Maximum number of 255 items, as it's stored as a byte in the message stream!
    public enum OdinMessageType
    {
        UpdateSyncVar,
        SpawnPrefab,
        UpdateNetworkedObject
    }

    // Maximum number of 255 items, as it's stored as a byte in the message stream!
    public enum OdinPrimitive
    {
        Bool,
        Short,
        String,
        Integer,
        Float,
        Double,
        Vector2,
        Vector3,
        Vector4,
        Quaternion
    }
    
    /*
    abstract class OdinMessage
    {
        public OdinMessageType OdinMessageType;
    }
    
    abstract class OdinUpdateSyncVarMessage : OdinMessage
    {
        public OdinMessageType OdinMessageType = OdinMessageType.UpdateSyncVar;
        public string Name;
        public OdinPrimitive OdinPrimitive;
        public object Value;
    }
*/
    public class OdinMessage : OdinNetworkWriter
    {
        public OdinMessage(OdinMessageType type)
        {
            Write(type);          
        }
    }

    class OdinUpdateSyncVarMessage : OdinMessage
    {
        public OdinUpdateSyncVarMessage(string name, object value) : base(OdinMessageType.UpdateSyncVar)
        {
            Write(name);
            Write(value);
        }
    }
    
}
