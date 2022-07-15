using System.Collections.Generic;
using OdinNative.Odin;

namespace Odin.OdinNetworking.Messages
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
        
        public static void WriteSyncVars(List<OdinUserDataSyncVar> syncVars, OdinNetworkWriter writer)
        {
            writer.Write((byte)syncVars.Count);
            foreach (var syncVar in syncVars)
            {
                syncVar.ToWriter(writer);
            }
        }

        public static List<OdinUserDataSyncVar> ReadSyncVars(OdinNetworkReader reader)
        {
            List<OdinUserDataSyncVar> syncVars = new List<OdinUserDataSyncVar>();
            
            var numberOfSyncVars = reader.ReadByte();
            if (numberOfSyncVars > 0)
            {
                for (byte i = 0; i < numberOfSyncVars; i++)
                {
                    OdinUserDataSyncVar syncVar = OdinUserDataSyncVar.FromReader(reader);
                    syncVars.Add(syncVar);
                }
            }
                
            return syncVars;
        }

        
    }
}
