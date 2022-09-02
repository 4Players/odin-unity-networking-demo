using System;
using System.Collections.Generic;
using OdinNative.Odin;
using UnityEngine;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// Defines supported primitives of OdinNetworking. You can build more complex structures out of those primitives.
    /// </summary>
    /// <remarks>Maximum number of 255 items, as it's stored as a byte in the message stream!</remarks>
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
    
    /// <summary>
    /// This is the base class of a message. A message has a type and properties that are finally serialized into
    /// a <see cref="Odin.OdinNetworking.OdinNetworkWriter"/> which represents a byte array sent over the network.
    /// </summary>
    /// <remarks>The whole message system might seem as overkill, as items could serialize directly to a network
    /// writer instead of serializing into a message instance and then using that to write a byte array. As byte arrays
    /// need to be read in the exact same order as its been written, and its not really human readable, errors are very
    /// hard to find. Therefore it's easer to have reading and writing in the same class rather than distributed over
    /// various classes. It also allows for optimization at central points of the network like bit compression, etc.
    /// </remarks>
    public class OdinMessage: IUserData
    {
        /// <summary>
        /// A dictionary mapping a byte to a class type. This way, only a byte needs be sent over the network
        /// to identify a message type.
        /// </summary>
        private static Dictionary<byte, Type> _messageTypes = new Dictionary<byte, Type>();
        private static byte _messageTypeId = 1; 
        
        /// <summary>
        /// Register a new message type. This class will create a new entry based on the current message type id and
        /// will increment an internal message type id counter to the next value. This way, a dictionary is built mapping
        /// message types to a byte value (identifying the class instance that needs to be created when deserializing a message).
        /// </summary>
        /// <remarks>If you create a new message type class, make sure you call the RegisterMessageType before using it</remarks>
        /// <typeparam name="T">The type of the message class you want to register</typeparam>
        public static void RegisterMessageType<T>()
        {
            _messageTypes.Add(_messageTypeId, typeof(T));
            _messageTypeId++;
        }

        /// <summary>
        /// Register built-in message types
        /// </summary>
        static OdinMessage()
        {
            RegisterMessageType<OdinSpawnPrefabMessage>();
            RegisterMessageType<OdinUserDataUpdateMessage>();
            RegisterMessageType<OdinWorldUpdateMessage>();
            RegisterMessageType<OdinCustomMessage>();
            RegisterMessageType<OdinUpdateWorldSyncVarMessage>();
            RegisterMessageType<OdinRpcMessage>();
        }

        /// <summary>
        /// Get the message type for a message type id
        /// </summary>
        /// <param name="messageTypeId">The id of the message</param>
        /// <returns>The type if found or null if it does not exist (i.e. RegisterMessageType has not been called)</returns>
        private static Type GetMessageType(byte messageTypeId)
        {
            if (_messageTypes.ContainsKey(messageTypeId))
            {
                return _messageTypes[messageTypeId];    
            }

            return null;
        }

        /// <summary>
        /// Get the message type for a message type. This is the value that is sent over the network.
        /// </summary>
        /// <param name="messageType">The type of the message</param>
        /// <returns>0 if nothing has been found (i.e. RegisterMessageType has not been called for this type) or
        /// the message type id</returns>
        public static byte GetMessageTypeId(Type messageType)
        {
            foreach (var key in _messageTypes.Keys)
            {
                var type = GetMessageType(key);
                if (type == messageType)
                {
                    return key;
                }
            }

            return 0;
        }
        
        protected OdinMessage()
        {
            
        }
        
        /// <summary>
        /// Create an instance of a message based on a reader 
        /// </summary>
        /// <param name="reader">The reader containing the byte array of data received from the network</param>
        protected OdinMessage(OdinNetworkReader reader)
        {
            
        }

        /// <summary>
        /// A static function that reads the first byte of the data stream to identify the message type and then creates
        /// an instance of the corresponding subclass.
        /// </summary>
        /// <remarks>If you want to have custom messages you need to adjust this function right now.</remarks>
        /// <param name="reader">The reader containing data received from the network</param>
        /// <returns>An instance of a subclass based on the message type given in the first byte of the data stream.</returns>
        public static OdinMessage FromReader(OdinNetworkReader reader)
        {
            var messageTypeId = reader.ReadByte();
            var messageType = GetMessageType(messageTypeId);
            if (messageType == null)
            {
                Debug.LogWarning($"Unknown message type {messageTypeId}. You need to register the message type before with OdinMessage.RegisterMessageType.");
                return null;
            }

            return (OdinMessage)Activator.CreateInstance(messageType, new[] { reader });
        }
        
        /// <summary>
        /// Creates an instance of a messages subclass for the data received from the network.
        /// </summary>
        /// <param name="bytes">The bytes received from the network</param>
        /// <returns>The message instance</returns>
        public static OdinMessage FromBytes(byte[] bytes)
        {
            OdinNetworkReader reader = new OdinNetworkReader(bytes);
            return FromReader(reader);
        }

        /// <summary>
        /// Returns true if the message is empty.
        /// </summary>
        /// <returns>true if the message is empty (does not contain any customized data)</returns>
        public bool IsEmpty()
        {
            return ToBytes().Length <= 0;
        }

        /// <summary>
        /// Returns a writer object that will serialize the messages properties into a byte stream so that it can be sent
        /// over the network
        /// </summary>
        /// <remarks>A messages subclass must override this function and call its base class function</remarks>
        /// <returns></returns>
        public virtual OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = new OdinNetworkWriter();
            var messageTypeId = GetMessageTypeId(GetType());
            if (messageTypeId <= 0)
            {
                Debug.LogError("This message type has not been registered. Call RegisterMessageType in its static constructor (see built-in messages for a sample).");
                return null;
            }
            
            writer.Write(messageTypeId);
            return writer;
        }

        /// <summary>
        /// Return the bytes of the serialized message
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            OdinNetworkWriter writer = GetWriter();
            return writer.ToBytes();
        }
        
        /// <summary>
        /// A static function that you can use to write a standard sync vars list into the given writer object.
        /// </summary>
        /// <param name="syncVars">A list with sync vars</param>
        /// <param name="writer">The writer where the sync vars should be serialized</param>
        public static void WriteSyncVars(List<OdinUserDataSyncVar> syncVars, OdinNetworkWriter writer)
        {
            writer.Write((byte)syncVars.Count);
            foreach (var syncVar in syncVars)
            {
                syncVar.ToWriter(writer);
            }
        }

        /// <summary>
        /// A static function that deserialized sync vars previously serialized with WriteSyncVars.
        /// </summary>
        /// <param name="reader">The reader from which to deserialize the sync vars</param>
        /// <returns>A list with deserialized sync vars</returns>
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
