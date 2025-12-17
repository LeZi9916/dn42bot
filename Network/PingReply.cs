using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace dn42Bot.Network;

internal readonly struct PingReply
{
    public IPAddress Address { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
    public PingOptions? Options { get; init; }
    public TimeSpan RoundtripTime { get; init; }
    public IPStatus Status { get; init; }
}
