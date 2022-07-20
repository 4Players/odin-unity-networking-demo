using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Odin.OdinNetworking.Messages;
using OdinNative.Odin;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This class implements a byte based serialization used to compress messages into byte streams that are
    /// sent over the network. OdinNetworkWriter implements Odins <see cref="OdinNative.Odin.IUserData"/> protocol.
    /// The maximum size of a message is 1500 bytes. The class has an internal counter that is incremented with
    /// each write process.
    /// </summary>
    public class OdinNetworkWriter: IUserData
    {
        /// <summary>
        /// The byte array that is used to store written data
        /// </summary>
        private byte[] _bytes = new byte[1500];
        
        /// <summary>
        /// The current position of the cursor. Each write process will advance the cursor forward.
        /// </summary>
        public int Cursor { get; private set; }

        /// <summary>
        /// Return a byte at a specific index in the byte array
        /// </summary>
        /// <param name="index">The index of the byte array where to read the byte</param>
        /// <returns>The byte read at index</returns>
        public byte GetByteAt(int index)
        {
            if (index >= _bytes.Length)
            {
                return 0;
            }

            return _bytes[index];
        }

        /// <summary>
        /// Write a byte to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(byte value)
        {
            _bytes[Cursor] = value;
            Cursor++;
        }

        /// <summary>
        /// Write an integer to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(int value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        /// <summary>
        /// Write a short to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(ushort value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }
        
        /// <summary>
        /// Write a long to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(ulong value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }
        
        /// <summary>
        /// Write a bool to the byte stream
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(bool value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        /// <summary>
        /// Write a string to the byte array. Encoding is UTF8 and the first two bytes indicate the length of the
        /// string.
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Write((ushort)bytes.Length);
            foreach (var aByte in bytes)
            {
                Write(aByte);
            }
        }
        
        /// <summary>
        /// Write a float to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(float value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        /// <summary>
        /// Write a 2D vector to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        /// <summary>
        /// Write a 3D vector
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        /// <summary>
        /// Write a Quaternion used to represent rotation to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        /// <summary>
        /// Write a 4D vector to the byte array
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }
        
        /// <summary>
        /// Write a primitive type to the stream. Sometimes, at a specific position in the stream, different types
        /// have to be stored, like sync vars that are of different types. Write the primitive type followed by the
        /// actual primitive value to encode variable types.
        /// </summary>
        /// <param name="value">The value that should be stored in the stream</param>
        public void Write(OdinPrimitive value)
        {
            Write((byte)value);
        }
        
        /// <summary>
        /// Encode a position, rotation and scale (transform). If identity versions are provided those are not encoded.
        /// </summary>
        /// <param name="position">The position to be stored</param>
        /// <param name="rotation">The rotation to be stored</param>
        /// <param name="scale">The scale to be stored</param>
        public void Write(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            byte flags = 0;
            if (!position.Equals(Vector3.zero))
            {
                flags |= (byte)OdinNetworkingFlags.HasPosition;
            } 
            if (!rotation.Equals(Quaternion.identity))
            {
                flags |= (byte)OdinNetworkingFlags.HasRotation;
            }
            if (!scale.Equals(Vector3.one))
            {
                flags |= (byte)OdinNetworkingFlags.HasScale;
            }

            Write(flags);
            
            if (!position.Equals(Vector3.zero))
            {
                Write(position);
            } 
            if (!rotation.Equals(Quaternion.identity))
            {
                Write(rotation);
            }
            if (!scale.Equals(Vector3.one))
            {
                Write(scale);
            }
        }

        /// <summary>
        /// Write a transform to the byte stream. Writes the position, scale and rotation of the transform to the byte
        /// stream.
        /// </summary>
        /// <param name="transform">The transform that should be written.</param>
        public void Write(Transform transform)
        {
            Write(transform.localPosition, transform.localRotation, transform.localScale);
        }

        /// <summary>
        /// Write the bytes of another network writer to this byte array. 
        /// </summary>
        /// <param name="writer">The writer whose bytes should be written to this stream</param>
        public void Write(OdinNetworkWriter writer)
        {
            for (int i = 0; i < writer.Cursor; i++)
            {
                Write(writer.GetByteAt(i));
            }
        }

        /// <summary>
        /// Sometimes the type of a primitive is variable, i.e. a sync var has a specific position in the stream, but
        /// it's type varies. This function writes the type in the first byte and serializes the object afterwards. As
        /// the type is coming firth, the deserializer knows which primitive it should be decode next.
        /// </summary>
        /// <remarks>Only primitive types defined in <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/> can be
        /// used as value</remarks>
        /// <param name="value">A value of a type defined in <see cref="Odin.OdinNetworking.Messages.OdinPrimitive"/></param>
        public void Write(object value)
        {
            if (value is string s)
            {
                Write((byte)OdinPrimitive.String);
                Write(s);
            }
            else if (value is byte bt)
            {
                Write((byte)OdinPrimitive.Byte);
                Write(bt);
            }            
            else if (value is int i)
            {
                Write((byte)OdinPrimitive.Integer);
                Write(i);
            }
            else if (value is ushort us)
            {
                Write((byte)OdinPrimitive.Short);
                Write(us);
            }           
            else if (value is bool b)
            {
                Write((byte)OdinPrimitive.Bool);
                Write(b);
            }                       
            else if (value is float f)
            {
                Write((byte)OdinPrimitive.Float);
                Write(f);
            }          
            else if (value is Vector2 v2)
            {
                Write((byte)OdinPrimitive.Vector2);
                Write(v2);
            }          
            else if (value is Vector3 v3)
            {
                Write((byte)OdinPrimitive.Vector3);
                Write(v3);
            }              
            else if (value is Vector4 v4)
            {
                Write((byte)OdinPrimitive.Vector4);
                Write(v4);
            }           
            else if (value is Quaternion q)
            {
                Write((byte)OdinPrimitive.Quaternion);
                Write(q);
            }                       
            else
            {
                Debug.LogWarning("Could not write object as the type is unknown");
            }
        }
        
        /// <summary>
        /// Compress the given byte array with GZip.
        /// </summary>
        /// <param name="data">The byte array that should be compressed</param>
        /// <returns>The compressed byte array</returns>
        private byte[] Compress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }

        /// <summary>
        /// Returns the byte array up to the current cursor position, i.e. the actual bytes used.
        /// </summary>
        /// <returns>The uncompressed serialized byte array</returns>
        private byte[] GetUncompressedBytes()
        {
            var finalBytes = new byte[Cursor];
            Buffer.BlockCopy(_bytes, 0, finalBytes, 0, Cursor);
            //Debug.Log($"Uncompressed data with size {finalBytes.Length}%");
            return finalBytes;
        }
        
        /// <summary>
        /// Returns the compressed byte array
        /// </summary>
        /// <returns>The compressed byte array</returns>
        private byte[] GetCompressedBytes()
        {
            var finalBytes = GetUncompressedBytes();
            var compressedBytes = Compress(finalBytes);
            //Debug.Log($"Compressed data {compressedBytes.Length} vs. uncompressed {finalBytes.Length}, {Mathf.Round(((float)compressedBytes.Length / (float)finalBytes.Length)*100f)}%");
            return compressedBytes;
        }

        /// <summary>
        /// Messages should be compressed to save bandwidth. However, small messages (like commands) are too small so that
        /// compression does not bring any benefit. Therefore, depending on the size of the message its either compressed
        /// or uncompressed. The first byte of a message indicates if data is compressed or uncompressed so that the
        /// deserializer knows how to handle it. This function returns a final byte array with either compressed data
        /// or uncompressed data and the first byte indicating it.
        /// </summary>
        /// <param name="bytes">The byte array to be packaged with the compression flag</param>
        /// <param name="compressed">true if the byte array has been compressed or false if uncompressed</param>
        /// <returns></returns>
        private byte[] PackageBytes(byte[] bytes, bool compressed)
        {
            var finalBytes = new byte[bytes.Length + 1];
            finalBytes[0] = Convert.ToByte(compressed);
            Buffer.BlockCopy(bytes, 0, finalBytes, 1, bytes.Length);
            return finalBytes;
        }
        
        /// <summary>
        /// Implementation of the IUserData interface method to return final bytes. It delivers compressed data if the
        /// first byte is true or uncompressed if the first byte is false. The function decides if data should be
        /// compressed or not.
        /// </summary>
        /// <returns></returns>
        public virtual byte[] ToBytes()
        {
            if (Cursor < 100)
            {
                var bytes = GetUncompressedBytes();
                return PackageBytes(bytes, false);
            }
            else
            {
                var bytes = GetCompressedBytes();
                return PackageBytes(bytes, true);
            }
        }

        /// <summary>
        /// Checks if two writers contain the same byte array. Once you send a message store the writer and compare it
        /// to the writer created later to check if anything has changed. Only if data has been changed send an update
        /// to user data.
        /// </summary>
        /// <param name="writer">The writer to compare to this data</param>
        /// <returns>true if data is the same or false if data is different to writer.</returns>
        public bool IsEqual(OdinNetworkWriter writer)
        {
            if (writer == null)
            {
                return false;
            }
            
            if (Cursor != writer.Cursor)
            {
                return false;
            }

            for (int i = 0; i < Cursor; i++)
            {
                if (GetByteAt(i) != writer.GetByteAt(i))
                {
                    return false;
                }
            }

            return true;
        }
        
        /// <summary>
        /// Returns true if the writer is untouched (cursor is still at 0)
        /// </summary>
        /// <returns>true if empty (nothing has been written so far)</returns>
        public bool IsEmpty()
        {
            return Cursor == 0;
        }

        /// <summary>
        /// Returns a string in hex format
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return BitConverter.ToString(ToBytes()).Replace("-","");
        }
    }
}
