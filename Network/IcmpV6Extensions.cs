using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace dn42Bot.Network;

internal static class IcmpV6Extensions
{
    public static IPStatus ToIPStatus(this IcmpV6Type source)
    {
        var raw = (ushort)source;
        var t = (byte)(raw >> 8);
        var c = (byte)(raw & 0xFF);

        switch (t)
        {
            // Error Messages (0–127)
            case 1: // Destination Unreachable
                if (c == 1)
                {
                    return IPStatus.DestinationProhibited;
                }
                return IPStatus.DestinationUnreachable;

            case 2: // Packet Too Big
                return IPStatus.PacketTooBig;

            case 3: // Time Exceeded
                return IPStatus.TtlExpired;

            case 4: // Parameter Problem
                return IPStatus.ParameterProblem;

            // Informational (Echo)
            case 128: // Echo Request
                return IPStatus.Unknown;
            case 129: // Echo Reply
                return IPStatus.Success;

            default:
                return IPStatus.Unknown;
        }
    }
}
