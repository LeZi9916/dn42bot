using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace dn42Bot.Network;

internal static class IcmpTypeExtensions
{
    public static IPStatus ToIPStatus(this IcmpType source)
    {
        var t = (byte)((ushort)source >> 8);
        var c = (byte)((ushort)source & 0xFF);

        switch (t)
        {
            case 0: // Echo Reply
                return IPStatus.Success;

            case 3: // Destination Unreachable
                return c switch
                {
                    0 => IPStatus.DestinationNetworkUnreachable,
                    1 => IPStatus.DestinationHostUnreachable,
                    2 => IPStatus.DestinationProtocolUnreachable,
                    3 => IPStatus.DestinationPortUnreachable,
                    4 => IPStatus.PacketTooBig,  // Frag needed
                    _ => IPStatus.DestinationUnreachable
                };

            case 4: // Source Quench
                return IPStatus.SourceQuench;

            case 5: // Redirect
                return IPStatus.TtlExpired;

            case 11: // Time Exceeded
                return IPStatus.TtlExpired;

            case 12: // Parameter Problem
                return IPStatus.ParameterProblem;

            case 8: // Echo Request
                return IPStatus.Unknown;

            default:
                return IPStatus.Unknown;
        }
    }
}
