using UnityEngine;

namespace Odin.OdinNetworking.Messages
{
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
}