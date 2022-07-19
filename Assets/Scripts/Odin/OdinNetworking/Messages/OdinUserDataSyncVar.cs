namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// Serializes a sync var
    /// </summary>
    public struct OdinUserDataSyncVar
    {
        /// <summary>
        /// The name of the sync var
        /// </summary>
        public string Name;
        
        /// <summary>
        /// The value of the sync var. Parameters can be of any <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/> type.
        /// </summary>
        public object Value;

        /// <summary>
        /// Create an instance of this struct
        /// </summary>
        /// <param name="name">The name of the sync var</param>
        /// <param name="value">The value of any <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/> type.</param>
        public OdinUserDataSyncVar(string name, object value)
        {
            Name = name;
            Value = value;
        }
        
        /// <summary>
        /// Deserialize an instance of this message from the reader
        /// </summary>
        /// <param name="reader">The reader with data received from the network</param>
        /// <returns>An instance with property values serialized from the reader</returns>
        public static OdinUserDataSyncVar FromReader(OdinNetworkReader reader)
        {
            var syncVarName = reader.ReadString();
            var receivedValue = reader.ReadObject();
            return new OdinUserDataSyncVar(syncVarName, receivedValue);
        }

        /// <summary>
        /// Writes this struct to the writer.
        /// </summary>
        /// <param name="writer">The writer in which this struct should be written</param>
        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(Name);
            writer.Write(Value);
        }
    }
}