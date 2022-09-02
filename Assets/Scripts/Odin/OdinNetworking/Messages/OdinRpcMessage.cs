using System;

namespace Odin.OdinNetworking.Messages
{
    public class OdinRpcMessage: OdinMessage
    {
        public string MethodName;
        public object[] Parameters;
        
        public OdinRpcMessage(string methodName)
        {
            MethodName = methodName;
            Parameters = Array.Empty<object>();
        }

        public OdinRpcMessage(string methodName, object paramater1)
        {
            MethodName = methodName;
            Parameters = new object[] {paramater1};
        }
        
        public OdinRpcMessage(string methodName, object paramater1, object paramater2)
        {
            MethodName = methodName;
            Parameters = new object[] {paramater1, paramater2};
        }
        
        public OdinRpcMessage(string methodName, object paramater1, object paramater2, object paramater3)
        {
            MethodName = methodName;
            Parameters = new object[] {paramater1, paramater2, paramater3};
        }
        
        public OdinRpcMessage(string methodName, object paramater1, object paramater2, object paramater3, object paramater4)
        {
            MethodName = methodName;
            Parameters = new object[] {paramater1, paramater2, paramater3, paramater4};
        }

        public OdinRpcMessage(OdinNetworkReader reader) : base(reader)
        {
            MethodName = reader.ReadString();
            var numParams = reader.ReadByte();
            Parameters = new object[numParams];
            for (var i = 0; i < numParams; i++)
            {
                Parameters[i] = reader.ReadObject();
            }
        }

        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(MethodName);
            writer.Write((byte) Parameters.Length);
            foreach (var param in Parameters)
            {
                writer.Write(param);
            }
            return writer;
        }
    }
}