//#define     CONSOLE_LOG
//#define     UNITY_LOG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
#if UNITY_LOG
using UnityEngine;
#endif

namespace NetBase
{
    public struct PacketHeader
    {
        public ushort Size;
        public ushort Type;

        public void Serialize(PacketBase packet)
        {
            packet.Write(Size);
            packet.Write(Type);
        }

        public void Deserialize(PacketBase packet)
        {
            Size = packet.Read<ushort>();
            Type = packet.Read<ushort>();
        }

        public const int HeaderSize = 4; // 2 bytes for PacketSize, 2 bytes for PacketType

        public static PacketHeader FromBytes(byte[] buffer)
        {
            PacketHeader header = new PacketHeader
            {
                Size = BitConverter.ToUInt16(buffer, 0),
                Type = BitConverter.ToUInt16(buffer, 2)
            };
            return header;
        }

        public byte[] ToBytes()
        {
            byte[] buffer = new byte[HeaderSize];
            BitConverter.GetBytes(Size).CopyTo(buffer, 0);
            BitConverter.GetBytes(Type).CopyTo(buffer, 2);
            return buffer;
        }
    }

    public interface IPacketSerializable
    {
        void Serialize(PacketBase packet);
        void Deserialize(PacketBase packet);
    }

    public class PacketBase
    {
        private MemoryStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;

        public PacketBase()
        {
            stream = new MemoryStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        private string GetTypeName(object value)
        {
            if (value == null) return "null";
            return value.GetType().Name;
        }

        private string GetTypeName(Type type)
        {
            if (type == null) return "null";
            return type.Name;
        }

        public void Write(object value,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (value == null) return;

            if (value is int)
            {
                writer.Write((int)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((int){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((int){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is bool)
            {
                writer.Write((bool)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((bool){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((bool){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is char)
            {
                writer.Write((char)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((char){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((char){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is float)
            {
                writer.Write((float)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((float){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((float){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is long)
            {
                writer.Write((long)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((long){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((long){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is ushort)
            {
                writer.Write((ushort)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((ushort){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((ushort){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is Enum)
            {
                writer.Write(Convert.ToInt32(value));
#if CONSOLE_LOG
               Console.WriteLine("Write((Enum){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((Enum){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is string)
            {
                writer.Write((string)value);
#if CONSOLE_LOG
               Console.WriteLine("Write((string){0} {1} {2}/{3}", value.ToString(), GetCurrentBufferSize(), sourceFilePath, sourceLineNumber);
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((string){value}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is char[])
            {
                char[] charArray = (char[])value;
                writer.Write((Int32)charArray.Length);
                writer.Write(charArray);
#if CONSOLE_LOG
               Console.WriteLine($"Write((char[]){new string(charArray)}) {GetCurrentBufferSize()} {sourceFilePath}/{sourceLineNumber}");
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} ((char[]){new string(charArray)}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is int[])
            {
                int[] intArray = (int[])value;
                writer.Write((Int32)intArray.Length);
                for (int i = 0; i < intArray.Length; i++)
                {
                    writer.Write(intArray[i]);
                }
#if CONSOLE_LOG
                Console.WriteLine($"Write((int[])[{string.Join(", ", intArray)}]) {GetCurrentBufferSize()} {sourceFilePath}/{sourceLineNumber}");
#endif
#if UNITY_LOG
                Debug.Log($"Write Type:{GetTypeName(value)} ((int[])[{string.Join(", ", intArray)}]) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is float[])
            {
                float[] floatArray = (float[])value;
                writer.Write((Int32)floatArray.Length);
                for (int i = 0; i < floatArray.Length; i++)
                {
                    writer.Write(floatArray[i]);
                }
#if CONSOLE_LOG
                Console.WriteLine($"Write((float[])[{string.Join(", ", floatArray)}]) {GetCurrentBufferSize()} {sourceFilePath}/{sourceLineNumber}");
#endif
#if UNITY_LOG
                Debug.Log($"Write Type:{GetTypeName(value)} ((float[])[{string.Join(", ", floatArray)}]) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is short[])
            {
                short[] shortArray = (short[])value;
                writer.Write((Int32)shortArray.Length);
                for (int i = 0; i < shortArray.Length; i++)
                {
                    writer.Write(shortArray[i]);
                }
#if CONSOLE_LOG
                Console.WriteLine($"Write((short[])[{string.Join(", ", shortArray)}]) {GetCurrentBufferSize()} {sourceFilePath}/{sourceLineNumber}");
#endif
#if UNITY_LOG
                Debug.Log($"Write Type:{GetTypeName(value)} ((short[])[{string.Join(", ", shortArray)}]) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value is IPacketSerializable serializable)
            {
                serializable.Serialize(this);
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} (IPacketSerializable) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
            }
            else if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)value;
                Int32 count = list.Count;
                writer.Write(count);
#if CONSOLE_LOG
               Console.WriteLine($"Write((int){list.Count}) // List count {GetCurrentBufferSize()} {sourceFilePath}, {sourceLineNumber}");
#endif
#if UNITY_LOG
               string elementType = list.Count > 0 ? GetTypeName(list[0]) : "unknown";
               Debug.Log($"Write Type:{GetTypeName(value)}<{elementType}> (Count:{list.Count}) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
                foreach (var item in list)
                {
                    Write(item);
                }
            }
            else if (value is ValueType)
            {
#if CONSOLE_LOG
               Console.WriteLine($"Write((value){value.ToString()}) // Value type {GetCurrentBufferSize()} {sourceFilePath}, {sourceLineNumber}");
#endif
#if UNITY_LOG
               Debug.Log($"Write Type:{GetTypeName(value)} (ValueType) Size:{GetCurrentBufferSize()} at {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}");
#endif
                var fields = value.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    Write(field.GetValue(value));
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported type: {GetTypeName(value)}");
            }
        }

        public T Read<T>()
        {
            return (T)ReadObject(typeof(T));
        }

        private object ReadObject(Type type)
        {
            if (type == typeof(int))
            {
                var obj = reader.ReadInt32();
#if CONSOLE_LOG
               Console.WriteLine("Read(({0}){1} {2}", obj.GetType().ToString(), obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((int){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(bool))
            {
                var obj = reader.ReadBoolean();
#if CONSOLE_LOG
               Console.WriteLine("Read((bool){0} {1}", obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((bool){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(char))
            {
                var obj = reader.ReadChar();
#if CONSOLE_LOG
               Console.WriteLine("Read((char){0} {1}", obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((char){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(float))
            {
                var obj = reader.ReadSingle();
#if CONSOLE_LOG
               Console.WriteLine("Read(({0}){1} {2}", obj.GetType().ToString(), obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((float){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(long))
            {
                var obj = reader.ReadInt64();
#if CONSOLE_LOG
               Console.WriteLine("Read(({0}){1} {2}", obj.GetType().ToString(), obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((long){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(ushort))
            {
                var obj = reader.ReadUInt16();
#if CONSOLE_LOG
               Console.WriteLine("Read(({0}){1} {2}", obj.GetType().ToString(), obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((ushort){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type.IsEnum)
            {
                var obj = Enum.ToObject(type, reader.ReadInt32());
#if CONSOLE_LOG
               Console.WriteLine("Read(({0}){1} {2}", obj.GetType().ToString(), obj.ToString(), GetCurrentBufferSize());
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((Enum){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(string))
            {
                var obj = reader.ReadString();
#if CONSOLE_LOG
               Console.WriteLine($"Read((string){obj}) {GetCurrentBufferSize()}");
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((string){obj}) Size:{GetCurrentBufferSize()}");
#endif
                return obj;
            }
            else if (type == typeof(char[]))
            {
                int length = reader.ReadInt32();
                var charArray = reader.ReadChars(length);
#if CONSOLE_LOG
               Console.WriteLine($"Read((char[]){new string(charArray)}) {GetCurrentBufferSize()}");
#endif
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} ((char[]){new string(charArray)}) Size:{GetCurrentBufferSize()}");
#endif
                return charArray;
            }
            else if (type == typeof(int[]))
            {
                int length = reader.ReadInt32();
                var intArray = new int[length];
                for (int i = 0; i < length; i++)
                {
                    intArray[i] = reader.ReadInt32();
                }
#if CONSOLE_LOG
    Console.WriteLine($"Read((int[])[{string.Join(", ", intArray)}]) {GetCurrentBufferSize()}");
#endif
#if UNITY_LOG
    Debug.Log($"Read Type:{GetTypeName(type)} ((int[])[{string.Join(", ", intArray)}]) Size:{GetCurrentBufferSize()}");
#endif
                return intArray;
            }
            else if (type == typeof(float[]))
            {
                int length = reader.ReadInt32();
                var floatArray = new float[length];
                for (int i = 0; i < length; i++)
                {
                    floatArray[i] = reader.ReadSingle();
                }
#if CONSOLE_LOG
    Console.WriteLine($"Read((float[])[{string.Join(", ", floatArray)}]) {GetCurrentBufferSize()}");
#endif
#if UNITY_LOG
    Debug.Log($"Read Type:{GetTypeName(type)} ((float[])[{string.Join(", ", floatArray)}]) Size:{GetCurrentBufferSize()}");
#endif
                return floatArray;
            }
            else if (type == typeof(short[]))
            {
                int length = reader.ReadInt32();
                var shortArray = new short[length];
                for (int i = 0; i < length; i++)
                {
                    shortArray[i] = reader.ReadInt16();
                }
#if CONSOLE_LOG
    Console.WriteLine($"Read((short[])[{string.Join(", ", shortArray)}]) {GetCurrentBufferSize()}");
#endif
#if UNITY_LOG
    Debug.Log($"Read Type:{GetTypeName(type)} ((short[])[{string.Join(", ", shortArray)}]) Size:{GetCurrentBufferSize()}");
#endif
                return shortArray;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                int count = reader.ReadInt32();
#if CONSOLE_LOG
               Console.WriteLine("Read(List Count){0} {1}", count, GetCurrentBufferSize());
#endif
#if UNITY_LOG
               string elementTypeName = type.GetGenericArguments()[0].Name;
               Debug.Log($"Read Type:List<{elementTypeName}> (Count:{count}) Size:{GetCurrentBufferSize()}");
#endif
                var list = (IList)Activator.CreateInstance(type);
                var elementType = type.GetGenericArguments()[0];

                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadObject(elementType));
                }

                return list;
            }
            else if (typeof(IPacketSerializable).IsAssignableFrom(type))
            {
                var value = Activator.CreateInstance(type);
                ((IPacketSerializable)value).Deserialize(this);
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} (IPacketSerializable) Size:{GetCurrentBufferSize()}");
#endif
                return value;
            }
            else if (type.IsValueType)
            {
                var value = Activator.CreateInstance(type);
#if UNITY_LOG
               Debug.Log($"Read Type:{GetTypeName(type)} (ValueType) Size:{GetCurrentBufferSize()}");
#endif
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    field.SetValue(value, ReadObject(field.FieldType));
                }
                return value;
            }
            else
            {
                throw new NotSupportedException($"Cannot read type {GetTypeName(type)}");
            }
        }

        public byte[] GetPacketData()
        {
            return stream.ToArray();
        }

        public void SetPacketData(byte[] data)
        {
            stream = new MemoryStream(data);
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public void ResetStreamPosition()
        {
            stream.Position = 0;
        }

        public int GetCurrentBufferSize()
        {
            return (int)stream.Position;
        }
    }
}
