using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    public class OdinUserDataUpdateMessage : OdinMessage
    {
        public bool HasTransform = false;
        public OdinUserDataTransform Transform;

        public bool HasAnimationParameters = false;
        public List<OdinUserDataAnimationParam> AnimationParams = new List<OdinUserDataAnimationParam>();
        
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();

        public bool IsHost = false;
        public List<OdinUserDataManagedObject> ManagedWorldObjects = new List<OdinUserDataManagedObject>();

        public OdinUserDataUpdateMessage() : base(OdinMessageType.UserData)
        {
            
        }
        
        public OdinUserDataUpdateMessage(OdinNetworkReader reader) : base(reader)
        {
            MessageType = OdinMessageType.UserData;
            
            HasTransform = reader.ReadBoolean();
            if (HasTransform)
            {
                Transform = OdinUserDataTransform.FromReader(reader);
            }
            
            // Read Animator
            HasAnimationParameters = reader.ReadBoolean();
            if (HasAnimationParameters)
            {
                AnimationParams = ReadAnimationParams(reader);
            }

            SyncVars = ReadSyncVars(reader);
            
            var numberOfNetworkedObjects = reader.ReadByte();
            for (var i = 0; i < numberOfNetworkedObjects; i++)
            {
                OdinUserDataManagedObject managedObject = OdinUserDataManagedObject.FromReader(reader);
                ManagedObjects.Add(managedObject);
            }

            IsHost = reader.ReadBoolean();
            if (IsHost)
            {
                var numberOfManagedWorldObjects = reader.ReadByte();
                for (var i = 0; i < numberOfManagedWorldObjects; i++)
                {
                    OdinUserDataManagedObject managedObject = OdinUserDataManagedObject.FromReader(reader);
                    ManagedWorldObjects.Add(managedObject);
                }
            }
        }
        
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();

            writer.Write(HasTransform);
            if (HasTransform)
            {
                Transform.ToWriter(writer);
            }

            writer.Write(HasAnimationParameters);
            if (HasAnimationParameters)
            {
                WriteAnimationParams(writer);
            }

            WriteSyncVars(SyncVars, writer);

            writer.Write((byte)ManagedObjects.Count);
            foreach(var managedObject in ManagedObjects)
            {
                managedObject.ToWriter(writer);
            }

            writer.Write(IsHost);
            if (IsHost)
            {
                writer.Write((byte)ManagedWorldObjects.Count);
                foreach(var managedObject in ManagedWorldObjects)
                {
                    managedObject.ToWriter(writer);
                }
            }

            return writer;
        }

        private void WriteAnimationParams(OdinNetworkWriter writer)
        {
            writer.Write(AnimationParams.Count);
            foreach (var animationParam in AnimationParams)
            {
                var primitive = animationParam.Primitive; 
                writer.Write(animationParam.Primitive);
                if (primitive == OdinPrimitive.Bool)
                {
                    writer.Write((bool)animationParam.Value);
                } 
                else if (primitive == OdinPrimitive.Float)
                {
                    writer.Write((float)animationParam.Value);
                }
                else if (primitive == OdinPrimitive.Integer)
                {
                    writer.Write((int)animationParam.Value);
                }                
            }
        }

        private List<OdinUserDataAnimationParam> ReadAnimationParams(OdinNetworkReader reader)
        {
            var animationParams = new List<OdinUserDataAnimationParam>();
            var numberOfAnimationParams = reader.ReadInt();
            for (int i = 0; i < numberOfAnimationParams; i++)
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
                animationParams.Add(param);
            }

            return animationParams;
        }
    }
}