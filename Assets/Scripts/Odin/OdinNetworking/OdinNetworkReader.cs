using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Odin.OdinNetworking.Messages;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// Flags used to compress transforms. Instead of always sending 1,1,1 or 0,0,0 for the position we just set one
    /// bit to indicate that the default rotation, scale or position should be used.
    /// </summary>
    [Flags]
    public enum OdinNetworkingFlags: byte
    {
        HasPosition = 1,
        HasRotation = 2,
        HasScale = 4
    }
    
    /// <summary>
    /// This class deserializes a byte stream by providing functions to read various primitive types from it. Messages
    /// are compiled byte streams that are as compact as possible to save bandwidth. This class is used to convert the
    /// byte stream back to a message object.
    /// The reader has an internal cursor counter that starts at 0 and advances with each primitive read requested. So,
    /// the first call to ReadByte will return the first byte, while the second call to ReadByte will be the second byte
    /// as the first ReadByte call will have set the cursor one byte further.
    /// </summary>
    /// <remarks>OdinNetworking uses Gzip compression to make messages as compact as possible. However, for smaller
    /// messages this does not bring any value, instead the package can be even larger than without compression.
    /// Our tests indicated that messages larger 100 bytes profit from compression. So the first byte of a message
    /// indicates if it's compressed or not.</remarks>
    public class OdinNetworkReader
    {
        private byte[] _bytes;
        private int _cursor = 0; // First byte is compression flag
        
        public OdinNetworkReader(byte[] bytes)
        {
            var firstByte = bytes[0];
            if (Convert.ToBoolean(firstByte))
            {
                // Compressed Data
                var compressedBytes = new byte[bytes.Length - 1];
                Buffer.BlockCopy(bytes, 1, compressedBytes, 0, compressedBytes.Length);
                _bytes = Decompress(compressedBytes);
            }
            else
            {
                _bytes = bytes;
                _cursor = 1;
            }
        }
        
        private byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        /// <summary>
        /// Read a byte at a specific position in the byte stream
        /// </summary>
        /// <param name="index">The index - make sure it's smaller than the size of the array</param>
        /// <returns>The byte at the index or 0 if out of bounds</returns>
        public byte ReadByteAt(int index)
        {
            if (index < 0 || index >= _bytes.Length)
            {
                return 0;
            }
            
            return _bytes[index];
        }

        /// <summary>
        /// Read a byte from the byte array
        /// </summary>
        /// <returns>The byte or 0 if out of bounds</returns>
        public byte ReadByte()
        {
            if (_cursor >= _bytes.Length)
            {
                return 0;
            }
            
            byte aByte = _bytes[_cursor];
            _cursor++;
            return aByte;
        }

        /// <summary>
        /// Read a short value from the byte stream
        /// </summary>
        /// <returns>The short value or 0 if out of bounds</returns>
        public ushort ReadUShort()
        {
            if (_cursor + sizeof(ushort) >= _bytes.Length)
            {
                return 0;
            }
            
            ushort value = BitConverter.ToUInt16(_bytes, _cursor);
            _cursor += sizeof(ushort);
            return value;
        }

        /// <summary>
        /// Read the type of the primitive. If different types of primitives can be stored in the message, i.e. a
        /// value of a sync var, this flag indicates which primitive comes next in the byte stream. This is important
        /// as different primitives have different length.
        /// </summary>
        /// <returns>The primitive type</returns>
        public OdinPrimitive ReadPrimitiveType()
        {
            return (OdinPrimitive)ReadByte();
        }
        
        /// <summary>
        /// Read a message type from the byte stream
        /// </summary>
        /// <returns>The decoded message type</returns>
        public OdinMessageType ReadMessageType()
        {
            return (OdinMessageType)ReadByte();
        }

        /// <summary>
        /// Read a transform. A transform is always position, rotation and scale. However, as scale often ist just 1,1,1
        /// it's packed. The first byte indicates which parts of the transform are available. If scale is default 1,1,1
        /// just a bit indicates that and the scale is not written to the stream. When decoding the default scale is then
        /// used.
        /// </summary>
        /// <returns>A truple of position, rotation and scale</returns>
        public (Vector3, Quaternion, Vector3) ReadTransform()
        {
            var localPosition = Vector3.zero;
            var localRotation = Quaternion.identity;
            var localScale = Vector3.one;
            
            var flags = (OdinNetworkingFlags)ReadByte();
            if ((flags & OdinNetworkingFlags.HasPosition) != 0)
            {
                localPosition = ReadVector3();    
            }
            if ((flags & OdinNetworkingFlags.HasRotation) != 0)
            {
                localRotation = ReadQuaternion();    
            }
            if ((flags & OdinNetworkingFlags.HasScale) != 0)
            {
                localScale = ReadVector3();    
            }

            return (localPosition, localRotation, localScale);
        }

        /// <summary>
        /// Read an integer from the stream
        /// </summary>
        /// <returns>The integer decoded at the current position</returns>
        public int ReadInt()
        {
            if (_cursor + sizeof(int) >= _bytes.Length)
            {
                return 0;
            }
            
            int value = BitConverter.ToInt32(_bytes, _cursor);
            _cursor += sizeof(int);
            return value;
        }
        
        /// <summary>
        /// Read a boolean from the stream.
        /// </summary>
        /// <returns>The boolean</returns>
        public bool ReadBoolean()
        {
            if (_cursor + sizeof(bool) >= _bytes.Length)
            {
                return false;
            }
            
            bool value = BitConverter.ToBoolean(_bytes, _cursor);
            _cursor += sizeof(bool);
            return value;
        }
        
        /// <summary>
        /// Read a float from the stream
        /// </summary>
        /// <returns>The float or 0.0 if out of bounds</returns>
        public float ReadFloat()
        {
            if (_cursor + sizeof(float) >= _bytes.Length)
            {
                return 0;
            }
            
            float value = BitConverter.ToSingle(_bytes, _cursor);
            _cursor += sizeof(float);
            return value;
        }
        
        /// <summary>
        /// Read a double value from the stream. Doubles are very large and should be used sparingly.
        /// </summary>
        /// <returns>The double decoded</returns>
        public double ReadDouble()
        {
            if (_cursor + sizeof(double) >= _bytes.Length)
            {
                return 0;
            }
            
            double value = BitConverter.ToDouble(_bytes, _cursor);
            _cursor += sizeof(double);
            return value;
        }

        /// <summary>
        /// Read a 2D vector
        /// </summary>
        /// <returns>The decoded vector.</returns>
        public Vector2 ReadVector2()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            return new Vector2(x, y);
        }
        
        /// <summary>
        /// Read a 3D vector from the stream
        /// </summary>
        /// <returns>The 3D vector</returns>
        public Vector3 ReadVector3()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            return new Vector3(x, y, z);
        }
        
        /// <summary>
        /// Read a 4D vector from the stream
        /// </summary>
        /// <returns>The vector read</returns>
        public Vector4 ReadVector4()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            float w = ReadFloat();
            return new Vector4(x, y, z, w);
        }
        
        /// <summary>
        /// Read a Quaternion used to represent rotations from the stream
        /// </summary>
        /// <returns>The decoded Quaternion</returns>
        public Quaternion ReadQuaternion()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            float w = ReadFloat();
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Read a string from the stream. The string is encoded in UTF8 format. The first two bytes are used to encode
        /// the length of the string
        /// </summary>
        /// <returns>The string decoded from the stream</returns>
        public string ReadString()
        {
            ushort length = ReadUShort();
            string aString = Encoding.UTF8.GetString(_bytes, _cursor, length);
            _cursor += length;
            return aString;
        }

        /// <summary>
        /// Read an object from the stream. An object is a placeholder that can hold many different types of objects.
        /// The first byte indicates which primitive is encoded next.
        /// </summary>
        /// <returns>An object type which can contain bools, vectors, etc.</returns>
        public object ReadObject()
        {
            OdinPrimitive primitive = (OdinPrimitive)ReadByte();
            if (primitive == OdinPrimitive.String)
            {
                return ReadString();
            }
            else if (primitive == OdinPrimitive.Bool)
            {
                return ReadBoolean();
            }            
            else if (primitive == OdinPrimitive.Byte)
            {
                return ReadByte();
            }                        
            else if (primitive == OdinPrimitive.Integer)
            {
                return ReadInt();
            }
            else if (primitive == OdinPrimitive.Short)
            {
                return ReadUShort();
            }
            else if (primitive == OdinPrimitive.Float)
            {
                return ReadFloat();
            }
            else if (primitive == OdinPrimitive.Vector2)
            {
                return ReadVector2();
            }
            else if (primitive == OdinPrimitive.Vector3)
            {
                return ReadVector3();
            }
            else if (primitive == OdinPrimitive.Vector4)
            {
                return ReadVector4();
            }
            else if (primitive == OdinPrimitive.Quaternion)
            {
                return ReadQuaternion();
            }
            
            return null;
        }
    }
}
