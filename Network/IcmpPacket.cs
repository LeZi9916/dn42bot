using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

internal readonly struct IcmpPacket: IInternetPacket
{
    public required IcmpType Type { get; init; }
    public required ushort Checksum { get; init; }
    public required ushort Identifier { get; init; }
    public required ushort SequenceNumber { get; init; }
    public required ReadOnlyMemory<byte> Payload { get; init; }
    public ReadOnlyMemory<byte> Raw { get; init; }

    public static IcmpPacket Create(IcmpType type, ushort id, ushort seq)
    {
        return Create(type, id, seq, ReadOnlySpan<byte>.Empty);
    }
    public static IcmpPacket Create(IcmpType type, ushort id, ushort seq, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[8 + payload.Length];
        buffer[0] = (byte)((ushort)type >> 8);
        buffer[1] = (byte)((ushort)type & 0xFF);
        buffer[2] = 0;         // Checksum temporary
        buffer[3] = 0;
        buffer[4] = (byte)(id >> 8);      // Identifier
        buffer[5] = (byte)(id & 0xFF);
        buffer[6] = (byte)(seq >> 8);         // Sequence
        buffer[7] = (byte)(seq & 0xFF);

        payload.CopyTo(buffer.AsSpan(8));

        var cs = CalcChecksum(buffer);
        buffer[2] = (byte)(cs >> 8);
        buffer[3] = (byte)(cs & 0xFF);

        return new ()
        {
            Type = type,
            Identifier = id,
            SequenceNumber = seq,
            Checksum = cs,
            Payload = buffer.AsMemory(8),
            Raw = buffer
        };
    }
    public static IcmpPacket Parse(ReadOnlyMemory<byte> rawPacket)
    {
        return Parse(rawPacket.Span);
    }
    public static IcmpPacket Parse(ReadOnlySpan<byte> rawPacket)
    {
        if (rawPacket.Length < 8)
        {
            throw new ArgumentException();
        }

        var buffer = new byte[rawPacket.Length];
        rawPacket.CopyTo(buffer);
        var type = (IcmpType)((buffer[0] << 8) | buffer[1]);
        var checksum = (ushort)((buffer[2] << 8) | buffer[3]);
        var id = (ushort)((buffer[4] << 8) | buffer[5]);
        var seq = (ushort)((buffer[6] << 8) | buffer[7]);
        return new()
        {
            Type = type,
            Checksum = checksum,
            Identifier = id,
            SequenceNumber = seq,
            Payload = buffer.AsMemory(8),
            Raw = buffer
        };
    }
    static ushort CalcChecksum(ReadOnlySpan<byte> data)
    {
        var sum = 0U;
        var i = 0;
        while (i + 1 < data.Length)
        {
            sum += (uint)((data[i] << 8) | data[i + 1]);
            i += 2;
        }
        if (i < data.Length)
        {
            sum += (uint)(data[i] << 8);
        }

        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return (ushort)(~sum);
    }
}
