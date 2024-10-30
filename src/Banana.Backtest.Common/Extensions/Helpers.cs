using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Banana.Backtest.Common.Extensions;

public static class Helpers
{
    private static long _orderId = Stopwatch.GetTimestamp();

    public static DateTime AsDateTime(this long unixTimeMilliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds).UtcDateTime;
    }

    public static long ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }

    public static TimeOnly Time(this DateTime dateTime)
    {
        return new TimeOnly(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string DecodeString(ulong value)
    {
        var valuePtr = (byte*)&value;
        Span<char> buffer= stackalloc char[sizeof(ulong)];
        Encoding.ASCII.GetChars(new Span<byte>(valuePtr, sizeof(ulong)), buffer);
        var slicer = buffer.IndexOf('\0');
        fixed (char* bufferPtr = buffer)
        {
            return new string(bufferPtr, 0, slicer > 0 ? slicer : buffer.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong EncodeString(ReadOnlySpan<char> input)
    {
        if (input.Length > sizeof(ulong))
            throw new ArgumentException("String is too long");
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        fixed (char* inputPtr = input)
        {
            fixed (byte* bufferPtr = buffer)
            {
                var bytesCount = Encoding.ASCII.GetBytes(inputPtr, input.Length, bufferPtr, sizeof(ulong));
                return EncodeString(buffer[..bytesCount]);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong EncodeString(Span<byte> asciiString)
    {
        
        if (asciiString.Length > sizeof(ulong))
            throw new ArgumentException("String is too long");
        
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        fixed (byte* inputPtr = asciiString)
        {
            fixed (byte* bufferPtr = buffer)
            {
                Buffer.MemoryCopy(inputPtr, bufferPtr, sizeof(ulong), asciiString.Length);
                return *(ulong*)bufferPtr;
            }
        }
    }
    
    public static int ReadSafe(this Stream stream, Span<byte> buffer)
    {
        if (stream.Read(buffer) is var read && read != 0)
        {
            if (read != buffer.Length) // Stream не готов в данный момент времени выдать данные в полном объеме
            {
                while (stream.Read(buffer[read..]) is var reread && reread != 0) // дочитываем
                {
                    read += reread;
                    if (read == buffer.Length)
                        break;
                }
            }

            if (read != buffer.Length)
                throw new InvalidOperationException("Stream ain't got enough bytes");
        }

        return read;
    }

    public static long NextId => Interlocked.Increment(ref _orderId);
    
    public static long Timestamp => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}