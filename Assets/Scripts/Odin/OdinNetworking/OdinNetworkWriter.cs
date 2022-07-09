using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdinNative.Odin;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinNetworkWriter: IUserData
    {
        private byte[] _bytes = new byte[1500];
        public int Cursor { get; private set; }

        public byte GetByteAt(int index)
        {
            if (index >= _bytes.Length)
            {
                return 0;
            }

            return _bytes[index];
        }

        public void Write(byte value)
        {
            _bytes[Cursor] = value;
            Cursor++;
        }

        public void Write(int value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        public void Write(ushort value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }
        
        public void Write(ulong value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }
        
        public void Write(bool value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        public void Write(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Write((ushort)bytes.Length);
            foreach (var aByte in bytes)
            {
                Write(aByte);
            }
        }
        
        public void Write(float value)
        {
            foreach (var aByte in BitConverter.GetBytes(value))
            {
                Write(aByte);
            }
        }

        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }
        
        public void Write(OdinPrimitive value)
        {
            Write((byte)value);
        }
        
        public void Write(OdinMessageType value)
        {
            Write((byte)value);
        }

        public void Write(Transform transform)
        {
            byte flags = 0;
            if (!transform.localPosition.Equals(Vector3.zero))
            {
                flags |= (byte)OdinNetworkingFlags.HasPosition;
            } 
            if (!transform.localRotation.Equals(Quaternion.identity))
            {
                flags |= (byte)OdinNetworkingFlags.HasRotation;
            }
            if (!transform.localScale.Equals(Vector3.one))
            {
                flags |= (byte)OdinNetworkingFlags.HasScale;
            }

            Write(flags);
            
            if (!transform.localPosition.Equals(Vector3.zero))
            {
                Write(transform.localPosition);
            } 
            if (!transform.localRotation.Equals(Quaternion.identity))
            {
                Write(transform.localRotation);
            }
            if (!transform.localScale.Equals(Vector3.one))
            {
                Write(transform.localScale);
            }
        }

        public void Write(OdinNetworkWriter writer)
        {
            for (int i = 0; i < writer.Cursor; i++)
            {
                Write(writer.GetByteAt(i));
            }
        }

        public void Write(object value)
        {
            if (value is string s)
            {
                Write((byte)OdinPrimitive.String);
                Write(s);
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

        private byte[] GetUncompressedBytes()
        {
            var finalBytes = new byte[Cursor];
            Buffer.BlockCopy(_bytes, 0, finalBytes, 0, Cursor);
            return finalBytes;
        }
        
        private byte[] GetCompressedBytes()
        {
            var finalBytes = GetUncompressedBytes();
            var compressedBytes = Compress(finalBytes);
            Debug.Log($"Compressed data {compressedBytes.Length} vs. uncompressed {finalBytes.Length}, {Mathf.Round(((float)compressedBytes.Length / (float)finalBytes.Length)*100f)}%");
            return compressedBytes;
        }

        private byte[] PackageBytes(byte[] bytes, bool compressed)
        {
            var finalBytes = new byte[bytes.Length + 1];
            finalBytes[0] = Convert.ToByte(compressed);
            Buffer.BlockCopy(bytes, 0, finalBytes, 1, bytes.Length);
            return finalBytes;
        }
        
        public byte[] ToBytes()
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

        public override string ToString()
        {
            return BitConverter.ToString(ToBytes()).Replace("-","");
        }
    }
}
