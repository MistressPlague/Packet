using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

public class Packet : IDisposable
{
    public enum PacketType
    {
        Message,
        Movement,
        CarMovement,
        Size,
        Doors,
        Unknown
    }

    public PacketType Type = PacketType.Unknown;

    /// <summary>
    /// All Raw Data - char is first letter of type, byte[] is all bytes in that type for aggregating. next char would act as separator
    /// </summary>
    public List<(char, byte[])> Buffer = new List<(char, byte[])>();

    /// <summary>
    /// nameof(T), Index -> This is only used when reading.
    /// </summary>
    public Dictionary<char, int> Indexes = new Dictionary<char, int>();

    public Packet(PacketType Type)
    {
        Write((int)Type);
    }

    public Packet(byte[] data)
    {
        Buffer = data.ToBuffer();

        Type = (PacketType)Convert.ToInt32(Buffer[0].Item2[0]);
        Indexes['i'] = (Indexes.ContainsKey('i') ? Indexes['i'] : 0) + 1;

        //Debug.Log($"{JsonConvert.SerializeObject(Buffer)}");
    }

    /// <summary>
    /// Make sure the fucking type is generic lol
    /// </summary>
    public T Read<T>()
    {
        var identifier = typeof(T).Name.ToLower()[0];

        var IndexToRead = GetNextIndex<T>();

        if (IndexToRead == -1)
        {
            Debug.LogError("No More Values Found Of That Type!");
            return default;
        }

        var CastedType = Buffer[IndexToRead].GetTrueTypeOfT();

        var result = (T)CastedType;

        Indexes[identifier] = IndexToRead + 1;

        return result;
    }

    /// <summary>
    /// Make sure the fucking type is generic lol
    /// </summary>
    public void Write<T>(T data)
    {
        if (data is bool b)
        {
            Buffer.Add((data.GetType().Name.ToLower()[0], BitConverter.GetBytes(b)));
        }
        else if (data is float s)
        {
            Buffer.Add((data.GetType().Name.ToLower()[0], BitConverter.GetBytes(s)));
        }
        else if (data is int i)
        {
            Buffer.Add((data.GetType().Name.ToLower()[0], BitConverter.GetBytes(i)));
        }
    }

    /// <summary>
    /// Gets the index of the next byte of this type to be read.
    /// </summary>
    /// <typeparam name="T">The Fucking Type</typeparam>
    private int GetNextIndex<T>()
    {
        var identifier = typeof(T).Name.ToLower()[0];

        var IndexToRead = (Indexes.ContainsKey(identifier) ? Indexes[identifier] : 0);

        var DidFind = false;

        if (IndexToRead >= Buffer.Count - 1)
        {
            Debug.Log("IndexToRead Was Too Large. Expect No More Values Error After This.");
            return IndexToRead;
        }

        //Debug.Log($"Scanning Beginning At Index: {IndexToRead} For Identifier: {identifier}");

        for (var i = IndexToRead; i < Buffer.Count; i++)
        {
            var CastedType = Buffer[i].GetTrueTypeOfT();

            //Debug.Log($"{i} is {CastedType}");

            if (CastedType is T)
            {
                IndexToRead = i;
                DidFind = true;
                //Debug.Log($"Found Index: {IndexToRead} For Identifier: {identifier}");
                break;
            }
        }

        return DidFind ? IndexToRead : -1;
    }

    // Yeet
    private bool disposed;

    protected virtual void Dispose(bool _disposing)
    {
        if (!disposed)
        {
            if (_disposing)
            {
                Buffer.Clear();
                Buffer = null;
                Indexes.Clear();
                Indexes = null;
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public static class PacketExtensions
{
    public static object GetTrueTypeOfT(this (char, byte[]) data)
    {
        object CastedType;

        switch (data.Item1)
        {
            case 'b':
                CastedType = BitConverter.ToBoolean(data.Item2, 0);
                break;
            case 's':
                CastedType = BitConverter.ToSingle(data.Item2, 0);
                break;
            case 'i':
                CastedType = BitConverter.ToInt32(data.Item2, 0);
                break;
            default:
                CastedType = -1;
                break;
        }

        return CastedType;
    }

    public static byte[] ToByteArray(this List<(char, byte[])> data)
    {
        var Temp = new List<byte>();

        foreach (var entry in data)
        {
            Temp.Add(Convert.ToByte(entry.Item1));
            Temp.AddRange(entry.Item2);
        }

        return Temp.ToArray();
    }

    public static List<(char, byte[])> ToBuffer(this byte[] data) // has a bug, help
    {
        var output = new List<(char, byte[])>();

        var TempBufferChar = '?';
        var TempBufferByteArray = new List<byte>();

        var IsInSegment = false;

        foreach (var entry in data)
        {
            ReCheck:
            if (!IsInSegment)
            {
                var c = Convert.ToChar(entry);

                // Check if this is a valid identifier, we do this non-blindly just in case. Technically, it isn't needed and can be assumed one.
                switch (c)
                {
                    case 'b':
                    case 's':
                    case 'i':
                        IsInSegment = true;
                        TempBufferChar = c;
                        break;
                }
            }
            else
            {
                // Check if this is the end of a segment, and the start of a new one. If it is a recognized identifier, set back to segment check mode, add the data so far to the buffer and check the new segment's data; being sure to clear the Temps so we don't have pain
                switch (Convert.ToChar(entry)) // b is bool, s is single (float), i is int32
                {
                    case 'b':
                    case 's':
                    case 'i':
                        output.Add((TempBufferChar, TempBufferByteArray.ToArray()));
                        TempBufferByteArray.Clear();
                        TempBufferChar = '?';
                        IsInSegment = false;
                        goto ReCheck;
                }

                //Append To Array
                TempBufferByteArray.Add(entry);
            }
        }

        if (TempBufferByteArray.Count > 0)
        {
            output.Add((TempBufferChar, TempBufferByteArray.ToArray())); // Lack of this was the bug
        }

        TempBufferByteArray.Clear();
        TempBufferByteArray = null;

        return output;
    }
}
