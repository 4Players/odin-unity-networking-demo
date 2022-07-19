using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// This message encodes the current state of a player object to make sure every client sees the same world and
    /// avatars. The update message stores the position, rotation and scale, animation, managed objects and sync vars
    /// over the network.
    /// </summary>
    public class OdinUserDataUpdateMessage : OdinMessage
    {
        /// <summary>
        /// Defines if the transform is available
        /// </summary>
        public bool HasTransform = false;
        
        /// <summary>
        /// The transform of the object
        /// </summary>
        public OdinUserDataTransform Transform;

        /// <summary>
        /// Indicates if animation parameters are available or not
        /// </summary>
        public bool HasAnimationParameters = false;
        
        /// <summary>
        /// A list of animation parameters which represent the current state of the animation
        /// </summary>
        public List<OdinUserDataAnimationParam> AnimationParams = new List<OdinUserDataAnimationParam>();
        
        /// <summary>
        /// A list with sync vars
        /// </summary>
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        /// <summary>
        /// A list of the state of managed objects
        /// </summary>
        public List<OdinUserDataManagedObject> ManagedObjects = new List<OdinUserDataManagedObject>();

        /// <summary>
        /// Indicates if the sender of this message has been host when sending the message
        /// </summary>
        public bool IsHost = false;
        
        /// <summary>
        /// A list of world objects managed by this host
        /// </summary>
        public List<OdinUserDataManagedObject> ManagedWorldObjects = new List<OdinUserDataManagedObject>();
        
        /// <summary>
        /// A list of world sync vars managed by this host
        /// </summary>
        public List<OdinUserDataSyncVar> WorldSyncVars = new List<OdinUserDataSyncVar>();

        /// <summary>
        /// Create an instance of this struct
        /// </summary>
        public OdinUserDataUpdateMessage() : base(OdinMessageType.UserData)
        {
            
        }
        
        /// <summary>
        /// Deserialize data stored in the reader into the local messages properties
        /// </summary>
        /// <param name="reader">The reader from which to read from</param>
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
                WorldSyncVars = ReadSyncVars(reader);
                
                var numberOfManagedWorldObjects = reader.ReadByte();
                for (var i = 0; i < numberOfManagedWorldObjects; i++)
                {
                    OdinUserDataManagedObject managedObject = OdinUserDataManagedObject.FromReader(reader);
                    ManagedWorldObjects.Add(managedObject);
                }
            }
        }
        
        /// <summary>
        /// Serializes the state of this message into a writer
        /// </summary>
        /// <returns>Returns the writer with the byte stream created from the messages properties</returns>
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
                WriteSyncVars(WorldSyncVars, writer);
                
                writer.Write((byte)ManagedWorldObjects.Count);
                foreach(var managedObject in ManagedWorldObjects)
                {
                    managedObject.ToWriter(writer);
                }
            }

            return writer;
        }

        /// <summary>
        /// Helper function writing animation properties to the writer
        /// </summary>
        /// <param name="writer">Writer to write animation params</param>
        private void WriteAnimationParams(OdinNetworkWriter writer)
        {
            writer.Write(AnimationParams.Count);
            foreach (var animationParam in AnimationParams)
            {
                animationParam.ToWriter(writer);
            }
        }

        /// <summary>
        /// Helper function to read animation parameters
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private List<OdinUserDataAnimationParam> ReadAnimationParams(OdinNetworkReader reader)
        {
            var animationParams = new List<OdinUserDataAnimationParam>();
            var numberOfAnimationParams = reader.ReadInt();
            for (int i = 0; i < numberOfAnimationParams; i++)
            {
                OdinUserDataAnimationParam param = OdinUserDataAnimationParam.FromReader(reader);
                animationParams.Add(param);
            }

            return animationParams;
        }
    }
}