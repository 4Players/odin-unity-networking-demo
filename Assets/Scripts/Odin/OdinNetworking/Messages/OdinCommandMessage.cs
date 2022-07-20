using System.Collections.Generic;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// Command messages are special as they are flexible and are sent by clients to the host that processes the commands
    /// and updates the world afterwards. Commands have a name and a list of parameters that you can use to customize the
    /// commands. Parameters can be of any <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/> type.
    /// <remarks>The host received commands in the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnCommandReceived"/></remarks>
    /// callback where they can handle it.
    /// </summary>
    public class OdinCommandMessage : OdinMessage
    {
        /// <summary>
        /// The parameters set in this command. Use SetValue to set a parameter value by name
        /// </summary>
        public List<OdinUserDataSyncVar> SyncVars = new List<OdinUserDataSyncVar>();
        
        /// <summary>
        /// The name of the command
        /// </summary>
        public string Name;

        
        /// <summary>
        /// Create a command and give it a name.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        public OdinCommandMessage(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Sets a commands value by name. You can use up to 256 parameters with any name. The objects value must be of
        /// an <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/> type.
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <param name="value">The value of the parameter. Any type defined in <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/>
        /// is allowed</param>
        public void SetValue(string name, object value)
        {
            SyncVars.Add(new OdinUserDataSyncVar(name, value));
        }

        /// <summary>
        /// Get a parameters value by name
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <returns>The value of the parameter. Any type defined in <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/>
        /// is supported.</returns>
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
        
        /// <summary>
        /// Deserialize a command from a reader
        /// </summary>
        /// <param name="reader">The reader from which to read the properties</param>
        public OdinCommandMessage(OdinNetworkReader reader)
        {
            Name = reader.ReadString();
            SyncVars = ReadSyncVars(reader);
        }
        
        /// <summary>
        /// Returns a writer with data serialized provided in the command message
        /// </summary>
        /// <returns>The writer with the serialized byte array</returns>
        public override OdinNetworkWriter GetWriter()
        {
            OdinNetworkWriter writer = base.GetWriter();
            writer.Write(Name);
            WriteSyncVars(SyncVars, writer);
            return writer;
        }
    }
}