using UnityEngine;

namespace Odin.OdinNetworking.Messages
{
    /// <summary>
    /// Stores a transform (i.e. position, rotation and scale). It's up to you, the context or use case if local transform
    /// or world coordinates are stored in this struct.
    /// </summary>
    public struct OdinUserDataTransform
    {
        /// <summary>
        /// The position of the transform
        /// </summary>
        public Vector3 Position;
        
        /// <summary>
        /// The rotation of the transform
        /// </summary>
        public Quaternion Rotation;
        
        /// <summary>
        /// The scale of the transform
        /// </summary>
        public Vector3 Scale;

        /// <summary>
        /// Create an instance of this struct
        /// </summary>
        /// <param name="position">The position</param>
        /// <param name="rotation">Rotation</param>
        /// <param name="scale">The scale</param>
        public OdinUserDataTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// Deserialize an instance of this message from the reader
        /// </summary>
        /// <param name="reader">The reader with data received from the network</param>
        /// <returns>An instance with property values serialized from the reader</returns>
        public static OdinUserDataTransform FromReader(OdinNetworkReader reader)
        {
            var (position, rotation, scale) = reader.ReadTransform();
            return new OdinUserDataTransform(position, rotation, scale);
        }

        /// <summary>
        /// Writes this struct to the writer.
        /// </summary>
        /// <param name="writer">The writer in which this struct should be written</param>
        public void ToWriter(OdinNetworkWriter writer)
        {
            writer.Write(Position, Rotation, Scale);
        }
    }
}