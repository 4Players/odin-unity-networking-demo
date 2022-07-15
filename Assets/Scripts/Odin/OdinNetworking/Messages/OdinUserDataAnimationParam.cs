namespace Odin.OdinNetworking.Messages
{
    public struct OdinUserDataAnimationParam
    {
        public OdinPrimitive Primitive;
        public object Value;

        public OdinUserDataAnimationParam(OdinPrimitive primitive, object value)
        {
            Primitive = primitive;
            Value = value;
        }
    }
}