using dn42Bot.Network;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace dn42Bot.Utils;

internal static class NetUtils
{
    readonly static IPAddress EMPTY_ADDRESS = IPAddress.Parse("0.0.0.0");
    readonly static EndPoint ANY_ENDPOINT = new IPEndPoint(IPAddress.Any, 0);
    readonly static Stopwatch TIMER = new Stopwatch();
    readonly static PingOptions DEFAULT_PING_OPTIONS = new();
    static NetUtils()
    {
        TIMER.Start();
    }
    public static PingReply Ping(IPAddress host, ReadOnlySpan<byte> payload, PingOptions options = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(payload.Length);
        try
        {
            return PingAsync(host, buffer, options).Result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    public static Task<PingReply> PingAsync(IPAddress host, ReadOnlyMemory<byte> payload, CancellationToken token = default)
    {
        return PingAsync(host, payload, DEFAULT_PING_OPTIONS, token);
    }
    public static Task<PingReply> PingAsync(IPAddress host, ReadOnlyMemory<byte> payload, PingOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(host, nameof(host));
        switch (host.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                return InternalPing4Async(host, payload, options, token);
            case AddressFamily.InterNetworkV6:
                return InternalPing6Async(host, payload, options, token);
            default:
                throw new NotSupportedException();
        }
    }
    
    static async Task<PingReply> InternalPing6Async(IPAddress host, ReadOnlyMemory<byte> payload, PingOptions options = default, CancellationToken token = default)
    {
        const int ICMP_FIXED_LENGTH = 4;
        const int IP_HEADER_LENGTH = 0;
        const int TTL_POSITION = 7;

        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IcmpV6);
        using var cts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
        var packet = IcmpV6Packet.Create(IcmpV6Type.EchoRequest, payload.Span);
        var id = (ushort)Environment.ProcessId;
        var seqId = (ushort)(Random.Shared.NextInt64() >> (64 - 16));
        socket.Ttl = options.Ttl;
        var startAt = TIMER.Elapsed;
        await socket.SendToAsync(packet.Raw, new IPEndPoint(host, 0), token);
        var remoteEP = ANY_ENDPOINT;
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(((IP_HEADER_LENGTH + ICMP_FIXED_LENGTH) * 2) + payload.Length, 1500));
        var buffer = Array.Empty<byte>();
        var bytes = 0;
        try
        {
            var result = await socket.ReceiveFromAsync(rentedBuffer, remoteEP, linkedCts.Token);
            bytes = result.ReceivedBytes;
            remoteEP = result.RemoteEndPoint;
            buffer = new byte[bytes];
            rentedBuffer.AsSpan(0, bytes).CopyTo(buffer);
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                throw;
            }
            return new()
            {
                Address = EMPTY_ADDRESS,
                Payload = ReadOnlyMemory<byte>.Empty,
                Options = null,
                Status = System.Net.NetworkInformation.IPStatus.TimedOut,
            };
        }
        catch (Exception e)
        {
            BotLogger.LogError($"NetUtils.Ping6: {e}");
            return new()
            {
                Address = EMPTY_ADDRESS,
                Payload = ReadOnlyMemory<byte>.Empty,
                Options = null,
                Status = System.Net.NetworkInformation.IPStatus.Unknown,
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
        var endAt = TIMER.Elapsed;
        var rtt = endAt - startAt;
        var remote = (IPEndPoint)remoteEP;
        if (bytes < IP_HEADER_LENGTH + ICMP_FIXED_LENGTH)
        {
            BotLogger.LogWarning("NetUtils.Ping6: received an invalid icmp response packet");
            return new()
            {
                Address = remote.Address,
                Payload = ReadOnlyMemory<byte>.Empty,
                Options = null,
                Status = System.Net.NetworkInformation.IPStatus.Unknown,
                RoundtripTime = rtt
            };
        }
        var recvPayloadLen = bytes - IP_HEADER_LENGTH - ICMP_FIXED_LENGTH;
        var remainingTTL = (short)254;
        var df = false;
        var recvPacket = IcmpV6Packet.Parse(buffer.AsSpan(IP_HEADER_LENGTH, bytes - IP_HEADER_LENGTH));

        return new()
        {
            Address = remote.Address,
            Payload = buffer.AsMemory(IP_HEADER_LENGTH + ICMP_FIXED_LENGTH, recvPayloadLen),
            Options = new()
            {
                Ttl = remainingTTL,
                DontFragment = df
            },
            Status = recvPacket.Type.ToIPStatus(),
            RoundtripTime = rtt
        };
    }
    static async Task<PingReply> InternalPing4Async(IPAddress host, ReadOnlyMemory<byte> payload, PingOptions options = default, CancellationToken token = default)
    {
        const int ICMP_FIXED_LENGTH = 8;
        const int IP_HEADER_LENGTH = 20;
        const int TTL_POSITION = 8;

        using var socket = new Socket(host.AddressFamily, SocketType.Raw, ProtocolType.Icmp);
        var id = (ushort)Environment.ProcessId;
        var seqId = (ushort)(Random.Shared.NextInt64() >> (64 - 16));
        var packet = IcmpPacket.Create(IcmpType.EchoRequest, id, seqId, payload.Span);
        socket.Ttl = options.Ttl;
        socket.DontFragment = options.DontFragment;
        var startAt = TIMER.Elapsed;
        await socket.SendToAsync(packet.Raw, new IPEndPoint(host, 0), token);
        var remoteEP = (IPEndPoint)ANY_ENDPOINT;
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(((IP_HEADER_LENGTH + ICMP_FIXED_LENGTH) * 2) + payload.Length, 1500));
        var buffer = Array.Empty<byte>();
        var bytes = 0;
        var recvPacket = default(IcmpPacket);
        try
        {
            using var cts = new CancellationTokenSource(options.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
            var result = default(SocketReceiveFromResult);
            while(!token.IsCancellationRequested)
            {
                result = await socket.ReceiveFromAsync(rentedBuffer, remoteEP, linkedCts.Token);
                bytes = result.ReceivedBytes;
                if (bytes < IP_HEADER_LENGTH + ICMP_FIXED_LENGTH)
                {
                    BotLogger.LogWarning("NetUtils.Ping4: received an invalid icmp response packet");
                    continue;
                }
                remoteEP = (IPEndPoint)result.RemoteEndPoint!;
                buffer = new byte[bytes];
                rentedBuffer.AsSpan(0, bytes).CopyTo(buffer);
                recvPacket = IcmpPacket.Parse(buffer.AsSpan(IP_HEADER_LENGTH, bytes - IP_HEADER_LENGTH));
                var t = (ushort)recvPacket.Type >> 8;
                var originId = recvPacket.Identifier;
                var originSeq = recvPacket.SequenceNumber;
                if (t is (3 or 11 or 12))
                {
                    var idOffset = 20 + 4;
                    var seqOffset = 20 + 6;
                    var rPayload = recvPacket.Payload.Span;
                    if(rPayload.Length <= seqOffset + 1)
                    {
                        continue;
                    }
                    originId = (ushort)((rPayload[idOffset] << 8) | rPayload[idOffset + 1]);
                    originSeq = (ushort)((rPayload[seqOffset] << 8) | rPayload[seqOffset + 1]);
                }
                if(originId != id || originSeq != seqId)
                {
                    BotLogger.LogWarning("NetUtils.Ping4: id and seq mismatch");
                    continue;
                }
                else
                {
                    break;
                }
            }
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                throw;
            }
            return new()
            {
                Address = EMPTY_ADDRESS,
                Payload = ReadOnlyMemory<byte>.Empty,
                Options = null,
                Status = System.Net.NetworkInformation.IPStatus.TimedOut,
            };
        }
        catch (Exception e)
        {
            BotLogger.LogError($"NetUtils.Ping4: {e}");
            return new()
            {
                Address = EMPTY_ADDRESS,
                Payload = ReadOnlyMemory<byte>.Empty,
                Options = null,
                Status = System.Net.NetworkInformation.IPStatus.Unknown,
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
        var endAt = TIMER.Elapsed;
        var rtt = endAt - startAt;
        var recvPayloadLen = bytes - IP_HEADER_LENGTH - ICMP_FIXED_LENGTH;
        var remainingTTL = buffer[TTL_POSITION];
        var df = (((((ushort)((buffer[6] << 8) | buffer[7])) >> 13) & 0x7) & 0x2) != 0;
        return new()
        {
            Address = remoteEP.Address,
            Payload = buffer.AsMemory(IP_HEADER_LENGTH + ICMP_FIXED_LENGTH, recvPayloadLen),
            Options = new()
            {
                Ttl = remainingTTL,
                DontFragment = df
            },
            Status = recvPacket.Type.ToIPStatus(),
            RoundtripTime = rtt
        };
    }
}
