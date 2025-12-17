using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace dn42Bot.Network;

internal static class IPNetworkHelper
{
    readonly static IPNetwork DN42_ADDRESS_SPACE_V4 = new IPNetwork(IPAddress.Parse("172.20.0.0"), 14);
    readonly static IPNetwork OTHER_ADDRESS_SPACE_V4 = new IPNetwork(IPAddress.Parse("10.0.0.0"), 8);
    readonly static IPNetwork DN42_ADDRESS_SPACE_V6 = new IPNetwork(IPAddress.Parse("fd00::"), 8);

    public static bool IsInDN42AddressSpace(IPAddress ipAddress)
    {
        switch(ipAddress.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                return DN42_ADDRESS_SPACE_V4.Contains(ipAddress) || OTHER_ADDRESS_SPACE_V4.Contains(ipAddress);
            case AddressFamily.InterNetworkV6:
                return DN42_ADDRESS_SPACE_V6.Contains(ipAddress);
            default:
                return false;
        }
    }
}
