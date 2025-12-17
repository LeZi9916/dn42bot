using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

internal interface IInternetPacket
{
    ReadOnlyMemory<byte> Raw { get; }
}
