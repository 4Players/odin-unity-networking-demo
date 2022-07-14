using System.Collections.Generic;
using OdinNative.Odin;
using UnityEngine;

namespace Odin.OdinNetworking
{
    // Maximum number of 255 items, as it's stored as a byte in the message stream!
    public enum OdinMessageType: byte
    {
        SpawnPrefab,
        JoinServer,
        UserData,
        WorldUpdate,
        Command
    }

    // Maximum number of 255 items, as it's stored as a byte in the message stream!
    public enum OdinPrimitive
    {
        Bool,
        Short,
        Byte,
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
    public class OdinMessage: IUserData
    {
        public OdinMessageType MessageType;

        public OdinMessage(OdinMessageType type)
        {
            MessageType = type;
        }

        public OdinMessage(OdinNetworkReader reader)
        {
            
        }
        
        public OdinMessage(byte[] bytes)
        {
            
        }

        public static OdinMessage FromReader(OdinNetworkReader reader)
        {
            var messageType = reader.ReadMessageType();
            if (messageType == OdinMessageType.UserData)
            {
                return new OdinUserDataUpdateMessage(reader);
            }
            else if (messageType == OdinMessageType.SpawnPrefab)
            {
                return new OdinSpawnPrefabMessage(reader);
            } 
            else if (messageType == OdinMessageType.WorldUpdate)
            {
                return new OdinWorldUpdateMessage(reader);
            }
            else if (messageType == OdinMessageType.Command)
            {
                return new OdinCommandMessage(reader);
            }

            return null;
        }
        
        public static OdinMessage FromBytes(byte[] bytes)
        {
            OdinNetworkReader reader = new OdinNetworkReader(bytes);
            return FromReader(reader);
        }

        public bool IsEmpty()
        {
            return ToBytes().Length <= 0;
        }

        public virtual OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = new OdinNetworkWriter();
            writer.Write(MessageType);
            return writer;
        }

        public byte[] ToBytes()
        {
            OdinNetworkWriter writer = GetWriter();
            return writer.ToBytes();
        }

        protected void WriteSyncVars(List<OdinUserDataSyncVar> syncVars, OdinNetworkWriter writer)
        {
            writer.Write((byte)syncVars.Count);
            foreach (var syncVar in syncVars)
            {
                writer.Write(syncVar.Name);
                writer.Write(syncVar.Value);
            }
        }

        protected List<OdinUserDataSyncVar> ReadSyncVars(OdinNetworkReader reader)
        {
            List<OdinUserDataSyncVar> syncVars = new List<OdinUserDataSyncVar>();
            
            var numberOfSyncVars = reader.ReadByte();
            if (numberOfSyncVars > 0)
            {
                for (byte i = 0; i < numberOfSyncVars; i++)
                {
                    var syncVarName = reader.ReadString();
                    var receivedValue = reader.ReadObject();

                    OdinUserDataSyncVar syncVar = new OdinUserDataSyncVar(syncVarName, receivedValue);
                    syncVars.Add(syncVar);
                }
            }
                
            return syncVars;
        }
    }

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

    class OdinSpawnPrefabMessage : OdinMessage
    {
        public byte PrefabId;
        public byte ObjectId;
        public Vector3 Position;
        public Quaternion Rotation;
        
        public OdinSpawnPrefabMessage(byte prefabId, byte objectId, Vector3 position, Quaternion rotation): base(OdinMessageType.SpawnPrefab)
        {
            MessageType = OdinMessageType.SpawnPrefab;
            PrefabId = prefabId;
            ObjectId = objectId;
            Position = position;
            Rotation = rotation;
        }
        
        public OdinSpawnPrefabMessage(OdinNetworkReader reader) : base(reader)
        {
            PrefabId = reader.ReadByte();
            ObjectId = reader.ReadByte();
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
        }
        
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(PrefabId);
            writer.Write(ObjectId);
            writer.Write(Position);
            writer.Write(Rotation);
            return writer;
        }
    }

    public struct OdinUserDataManagedObject
    {
        public byte ObjectId;
        public byte PrefabId;
        public OdinUserDataTransform Transform;
        public List<OdinUserDataSyncVar> SyncVars;

        public OdinUserDataManagedObject(byte objectId, byte prefabId, OdinUserDataTransform transform)
        {
            ObjectId = objectId;
            PrefabId = prefabId;
            SyncVars = new List<OdinUserDataSyncVar>();
            Transform = transform;
        }
    }

    public struct OdinUserDataSyncVar
    {
        public string Name;
        public object Value;

        public OdinUserDataSyncVar(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    public struct OdinUserDataTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public OdinUserDataTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public static OdinUserDataTransform FromReader(OdinNetworkReader reader)
        {
            var (position, rotation, scale) = reader.ReadTransform();
            return new OdinUserDataTransform(position, rotation, scale);
        }

        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(Position, Rotation, Scale);
        }
    }

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

    public class OdinWorldUpdateMessage : OdinMessage
    {
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();
        
        public OdinWorldUpdateMessage() : base(OdinMessageType.WorldUpdate)
        {
            
        }
        
        public OdinWorldUpdateMessage(OdinNetworkReader reader) : base(reader)
        {
            MessageType = OdinMessageType.WorldUpdate;
            SyncVars = ReadSyncVars(reader);
            
            var numberOfNetworkedObjects = reader.ReadByte();
            for (var i = 0; i < numberOfNetworkedObjects; i++)
            {
                var (objectId, prefabId) = OdinNetworkedObject.DeserializeHeader(reader);
                var transform = OdinUserDataTransform.FromReader(reader);
                OdinUserDataManagedObject managedObject = new OdinUserDataManagedObject(objectId, prefabId, transform);
                var syncVars = ReadSyncVars(reader);
                managedObject.SyncVars = syncVars;
                ManagedObjects.Add(managedObject);
            }
        }
        
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

    public class OdinUserDataUpdateMessage : OdinMessage
    {
        public bool HasTransform = false;
        public OdinUserDataTransform Transform;

        public bool HasAnimationParameters = false;
        public List<OdinUserDataAnimationParam> AnimationParams = new List<OdinUserDataAnimationParam>();
        
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();

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
                var (objectId, prefabId) = OdinNetworkedObject.DeserializeHeader(reader);
                var transform = OdinUserDataTransform.FromReader(reader);
                OdinUserDataManagedObject managedObject = new OdinUserDataManagedObject(objectId, prefabId, transform);
                var syncVars = ReadSyncVars(reader);
                managedObject.SyncVars = syncVars;
                ManagedObjects.Add(managedObject);
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
                writer.Write(managedObject.ObjectId);
                writer.Write(managedObject.PrefabId);
                managedObject.Transform.ToWriter(writer);
                WriteSyncVars(managedObject.SyncVars, writer);
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
