namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// A structure used to store animation parameters. It's used in the OdinUserDataUpdateMessage but you can use
    /// these specialized primitive in your own messages.
    /// </summary>
    public struct OdinUserDataAnimationParam
    {
        /// <summary>
        /// The primitive type that the animation parameter has. Unity supports bool, float and integers
        /// </summary>
        public OdinPrimitive Primitive;
        
        /// <summary>
        /// The value of the animation parameter
        /// </summary>
        public object Value;

        /// <summary>
        /// Create an instance of the animation parameter 
        /// </summary>
        /// <param name="primitive">The primitive type that the animation parameter has. Unity supports bool, float and integers</param>
        /// <param name="value">The value of the animation parameter</param>
        public OdinUserDataAnimationParam(OdinPrimitive primitive, object value)
        {
            Primitive = primitive;
            Value = value;
        }
        
        /// <summary>
        /// Deserialize an instance of this message from the reader
        /// </summary>
        /// <param name="reader">The reader with data received from the network</param>
        /// <returns>An instance with property values serialized from the reader</returns>
        public static OdinUserDataAnimationParam FromReader(OdinNetworkReader reader)
        {
            OdinPrimitive primitive = reader.ReadPrimitiveType();
            object value = null;
            if (primitive == OdinPrimitive.Bool)
            {
                value = reader.ReadBoolean();
            } 
            else if (primitive == OdinPrimitive.Float)
            {
                value = reader.ReadFloat();
            }
            else if (primitive == OdinPrimitive.Integer)
            {
                value = reader.ReadInt();
            }

            OdinUserDataAnimationParam param = new OdinUserDataAnimationParam(primitive, value);
            return param;
        }
        
        /// <summary>
        /// Writes this struct to the writer.
        /// </summary>
        /// <param name="writer">The writer in which this struct should be written</param>
        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(Primitive);
            if (Primitive == OdinPrimitive.Bool)
            {
                writer.Write((bool)Value);
            } 
            else if (Primitive == OdinPrimitive.Float)
            {
                writer.Write((float)Value);
            }
            else if (Primitive == OdinPrimitive.Integer)
            {
                writer.Write((int)Value);
            }   
        }
    }
}