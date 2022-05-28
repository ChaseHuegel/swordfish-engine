using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Swordfish.Library.Extensions;
using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Library.Networking
{
    public class Packet
    {
        /// <returns>instance of <typeparamref name="Packet"/> </returns>
        public static Packet Create() => new Packet();

        public int Length => data.Count;
        public int UnreadBytes => data.Count - readIndex;

        private int readIndex = 0;
        private List<byte> data = new List<byte>();

        /// <summary>
        /// Creates a <typeparamref name="Packet"/> from a <typeparamref name="byte"/> array
        /// </summary>
        /// <param name="data"></param>
        public Packet(byte[] data) => Append(data);

        public Packet() { }

        public override string ToString() => Encoding.ASCII.GetString(GetBytes());

        //  Casting to/from byte array
        public static implicit operator Packet(byte[] data) => new Packet(data);
        public static implicit operator byte[](Packet packet) => packet.GetBytes();

        /// <returns>new <typeparamref name="byte"/> array from the data in <paramref name="this"/></returns>
        public byte[] GetBytes() => data.ToArray();

        /// <returns>new <typeparamref name="byte"/> array with size <paramref name="length"/>
        ///     from the data in <paramref name="this"/> starting at <paramref name="index"/></returns>
        public byte[] GetBytes(int index, int length) => data.GetRange(index, length).ToArray();

        /// <returns>new <typeparamref name="Packet"/> with size <paramref name="length"/>
        ///     from the data in <paramref name="this"/> starting at <paramref name="index"/></returns>
        public Packet Grab(int index, int length) => new Packet(GetBytes(index, length));

        /// <summary>
        /// Removes the first 4 bytes at the start of the packet to undo a Pack() call
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet Unpack() { data.RemoveRange(0, 4); return this; }

        /// <summary>
        /// Resets the reading index to the start of this Packet
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet ResetReader() { readIndex = 0; return this; }

        /// <summary>
        /// Pushes the reading index by a set amount
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet PushReader(int amount) { readIndex += amount; return this; }

        /// <summary>
        /// Delete all data and reset the reader
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet Reset() { ResetReader(); data.Clear(); return this; }

        /// <summary>
        /// Append a <typeparamref name="byte"/> array to the end of the packet
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet Append(byte[] bytes) { data.AddRange(bytes); return this; }

        /// <summary>
        /// Sets the data of the packet to a <typeparamref name="byte"/> array
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        public Packet Assign(byte[] bytes) { Reset(); Append(bytes); return this; }

        /// <summary>
        /// Inserts the <typeparamref name="Packet"/> length at the start of the packet
        /// </summary>
        /// <returns>new <typeparamref name="byte"/> array from the data in this</returns>
        public byte[] Pack()
        {
            //  Write the packet size to the beginning
            data.InsertRange(0, BitConverter.GetBytes(data.Count));

            return GetBytes();
        }

        /// <summary>
        /// Writes a variable to the Packet
        /// <para/> Supported types are
        /// <see cref="string"/>
        /// <see cref="int"/>
        /// <see cref="float"/>
        /// <see cref="bool"/>
        /// </summary>
        /// <returns>builder for <see cref="Packet"/></returns>
        /// <exception cref="System.ArgumentException"></exception>
        public Packet Write(object value)
        {
            if      (value is string)       WriteString(value);
            else if (value is int)          WriteInt(value);
            else if (value is uint)         WriteUInt(value);
            else if (value is float)        WriteFloat(value);
            else if (value is bool)         WriteBool(value);
            else if (value is MultiBool)    WriteMultiBool(value);
            else if (value is string[])     WriteStringArray(value);
            else if (value == null)         Append(BitConverter.GetBytes(0));
            else Console.WriteLine($"Unsupported type [{value?.GetType()}] passed to Packet.Write()");

            return this;
        }

        public Packet Serialize(object obj)
        {
            foreach (FieldInfo field in obj.GetType().GetFields())
                Write(field.GetValue(obj) ?? field.FieldType.GetDefault());
            
            return this;
        }

        /// <summary>
        /// Deserializes a packet into an object of type T.
        /// </summary>
        /// <typeparam name="T">the type to deserialize as</typeparam>
        /// <returns>the deserialzied object</returns>
        public T Deserialize<T>() => (T)Deserialize(typeof(T));

        /// <summary>
        /// Deserializes a packet into an object of the specified type.
        /// </summary>
        /// <param name="type">the type to deserialize as</param>
        /// <returns>the deserialized object</returns>
        public object Deserialize(Type type)
        {
            object deserializedPacket = Activator.CreateInstance(type);
            
            foreach (FieldInfo field in type.GetFields())
                field.SetValue(deserializedPacket, Read(field.FieldType));

            return deserializedPacket;
        }

        private void WriteInt(object value) => Append( BitConverter.GetBytes((int)value) );
        private void WriteUInt(object value) => Append( BitConverter.GetBytes((uint)value) );
        private void WriteFloat(object value) => Append( BitConverter.GetBytes((float)value) );
        private void WriteBool(object value) => Append( BitConverter.GetBytes((bool)value) );
        private void WriteMultiBool(object value) => Append( ByteConverter.GetBytes((MultiBool)value) );

        private void WriteString(object value)
        {
            string s = (string)value ?? string.Empty;

            //  Write an int noting the length of the string in bytes
            byte[] bytes = BitConverter.GetBytes(Encoding.Default.GetByteCount(s));
            Append(bytes);

            //  Write the string
            bytes = Encoding.Default.GetBytes(s);
            Append(bytes);
        }

        private void WriteStringArray(object value)
        {
            string[] strings = (string[])value ?? Array.Empty<string>();

            //  Write an int noting the length of the array in bytes
            byte[] bytes = BitConverter.GetBytes(strings.Length);
            Append(bytes);

            //  Write the strings
            foreach (string s in strings)
                WriteString(s);
        }

        public object Read(Type type)
        {
            if      (type == typeof(string))    return ReadString();
            else if (type == typeof(int))       return ReadInt();
            else if (type == typeof(uint))      return ReadUInt();
            else if (type == typeof(float))     return ReadFloat();
            else if (type == typeof(bool))      return ReadBool();
            else if (type == typeof(MultiBool)) return ReadMultiBool();
            else if (type == typeof(string[]))  return ReadStringArray();

            return type.GetDefault();
            throw new ArgumentOutOfRangeException($"{type} is unsupported by Packet.Read!");
        }

        public byte[] ReadBytes(int count)
        {
            byte[] bytes = data.GetRange(readIndex, count).ToArray();
            readIndex += count;

            return bytes;
        }

        public string ReadString()
        {
            int length = BitConverter.ToInt32( GetBytes(readIndex, 4), 0 );
            string s = Encoding.Default.GetString( GetBytes(), readIndex+4, length );
            readIndex += length + 4;

            return s;
        }

        public int ReadInt()
        {
            readIndex += 4;
            return BitConverter.ToInt32( GetBytes(readIndex-4, 4), 0 );
        }

        public uint ReadUInt()
        {
            readIndex += 4;
            return BitConverter.ToUInt32( GetBytes(readIndex-4, 4), 0 );
        }

        public float ReadFloat()
        {
            readIndex += 4;
            return BitConverter.ToSingle( GetBytes(readIndex-4, 4), 0 );
        }

        public bool ReadBool()
        {
            readIndex += 1;
            return BitConverter.ToBoolean( GetBytes(readIndex-1, 1), 0 );
        }

        public MultiBool ReadMultiBool()
        {
            readIndex += 1;
            return ByteConverter.ToMultiBool( GetBytes(readIndex-1, 1), 0 );
        }

        public string[] ReadStringArray()
        {
            int length = BitConverter.ToInt32( GetBytes(readIndex, 4), 0 );
            readIndex += 4;
            
            string[] strings = new string[length];
            for (int i = 0; i < length; i++)
                strings[i] = ReadString();

            return strings;
        }
    }
}
