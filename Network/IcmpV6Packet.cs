using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

internal struct IcmpV6Packet : IInternetPacket
{
    public required IcmpV6Type Type { get; init; }
    public required ushort Checksum { get; init; }
    public required ReadOnlyMemory<byte> Payload { get; init; }
    public ReadOnlyMemory<byte> Raw { get; init; }

    public static IcmpV6Packet Create(IcmpV6Type type)
    {
        return Create(type, null);
    }
    public static IcmpV6Packet Create(IcmpV6Type type, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[8 + payload.Length];
        buffer[0] = (byte)((ushort)type >> 8);
        buffer[1] = (byte)((ushort)type & 0xFF);
        buffer[2] = 0;         // Checksum temporary
        buffer[3] = 0;

        payload.CopyTo(buffer.AsSpan(4));

        return new()
        {
            Type = type,
            Checksum = 0,
            Payload = buffer.AsMemory(4),
            Raw = buffer
        };
    }
    public static IcmpV6Packet Parse(ReadOnlyMemory<byte> rawPacket)
    {
        return Parse(rawPacket.Span);
    }
    public static IcmpV6Packet Parse(ReadOnlySpan<byte> rawPacket)
    {
        if (rawPacket.Length < 8)
        {
            throw new ArgumentException();
        }

        var buffer = new byte[rawPacket.Length];
        rawPacket.CopyTo(buffer);
        var type = (IcmpV6Type)((buffer[0] << 8) | buffer[1]);
        var checksum = (ushort)((buffer[2] << 8) | buffer[3]);
        return new()
        {
            Type = type,
            Checksum = checksum,
            Payload = buffer.AsMemory(4),
            Raw = buffer
        };
    }
}
