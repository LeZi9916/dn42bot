using dn42Bot.Buffers;
using dn42Bot.Network;
using dn42Bot.TelegramBot;
using dn42Bot.Utils;
using DnsClient;
using DnsClient.Protocol;
using Nito.AsyncEx;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using PingOptions = dn42Bot.Network.PingOptions;
using PingReply = dn42Bot.Network.PingReply;

namespace dn42Bot.Chatting.Controllers;

internal sealed class NetworkController: IController, ICallbackController, IControllerFactory
{
    public Guid RequestId
    {
        get
        {
            return _requestId;
        }
    }
    public DateTime CreateAt { get; private set; } = DateTime.Now;
    public DateTime LastActive { get; private set; } = DateTime.Now;
    public ControllerStatus State { get; private set; } = ControllerStatus.Created;
    public bool IsDisposed
    {
        get
        {
            return State == ControllerStatus.Disposed;
        }
    }

    string _cmdPrefix;
    string _cmdText;
    Message _userMessage;
    Message? _sentMessage;
    UserRequest _userRequest;

    CancellationTokenSource _cts = new();

    readonly Guid _requestId;
    readonly ChatRoom _chatRoom;
    readonly ITelegramBotClient _botClient;
    readonly LookupClient _dnsClient = new(_dnsLookupOptions);
    readonly AsyncLock _executeLock = new();
    readonly AsyncLock _callbackLock = new();

    readonly static LookupClientOptions _dnsLookupOptions;
    readonly static IPAddress DEFAULT_NAMESERVER_ADDRESS = Core.Config.Network.DNS.Address;
    const uint OUTPUT_TRACE_RTT_LEFT_PADDING_LENGTH = 48;

    static NetworkController()
    {
        _dnsLookupOptions = new(new NameServer(DEFAULT_NAMESERVER_ADDRESS, Core.Config.Network.DNS.Port))
        {
            Timeout = TimeSpan.FromMilliseconds(Core.Config.Network.DNS.TimeoutMS),
            ThrowDnsErrors = false
        };
    }
    NetworkController(UserRequest userRequest, ChatRoom chatRoom)
    {
        _chatRoom = chatRoom;
        _userRequest = userRequest;
        _cmdPrefix = userRequest.Command.Prefix;
        _botClient = userRequest.Client;
        _cmdText = userRequest.Command.Text;
        _userMessage = userRequest.Message!;
        _requestId = userRequest.RequestId;
    }
    public Task ExecuteAsync()
    {
        return InternalExecuteAsync(false);
    }
    public void OnCallback(CallbackQuery query)
    {
        OnCallbackAsync(query).AsTask().Wait();
    }
    public async ValueTask OnCallbackAsync(CallbackQuery query)
    {
        if (IsDisposed)
        {
            return;
        }
        using (await _callbackLock.LockAsync())
        {
            if(IsDisposed)
            {
                return;
            }
            var data = query.Data;
            switch(data)
            {
                case "cancel":
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                    break;
                case "retry":
                    if(!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                        _cts.Dispose();
                        _cts = new CancellationTokenSource();
                    }
                    _ = ExecuteAsync();
                    break;
            }
            await _botClient.AnswerCallbackQuery(query.Id, "Ok");
        }
    }
    async Task InternalExecuteAsync(bool accquireLock = false)
    {
        var @lock = default(IDisposable);
        if(!accquireLock)
        {
            @lock = await _executeLock.LockAsync();
        }
        try
        {
            if(IsDisposed)
            {
                return;
            }
            State = ControllerStatus.Running;
            switch (_cmdPrefix)
            {
                case "ping":
                    await PingAsync();
                    break;
                case "trace":
                    await TraceAsync();
                    break;
                case "nslookup":
                case "dig":
                    await NsLookupAsync();
                    break;
            }
        }
        catch(OperationCanceledException)
        {
            BotLogger.LogDebug($"Operation \"{RequestId}\" had been cancelled by user");
        }
        finally
        {
            @lock?.Dispose();
        }
    }
    async Task PingAsync()
    {
        var cancellationToken = _cts.Token;
        var isRegistedCallback = false;
        try
        {
            if (string.IsNullOrEmpty(_cmdText))
            {
                await PrintPingHelpTextAsync();
                return;
            }
            var inlineKeyboardWithCancel = new InlineKeyboardMarkup(new InlineKeyboardButton("Cancel", "cancel"));
            var inlineKeyboardWithRetry = new InlineKeyboardMarkup(new InlineKeyboardButton("Retry", "retry"));
            var timeoutMS = default(int?);
            var pingCount = default(int?);
            var size = default(int?);
            var isDFRequested = default(bool?);
            var isForceUseIPv4 = false;
            var isForceUseIPv6 = false;
            var hostAddress = default(IPAddress);
            var hostDomain = string.Empty;
            var intervalMS = default(int?);
            var lastParamName = ReadOnlySpan<char>.Empty;
            #region Arguments Parsing
            foreach (var range in _cmdText.AsSpan().Split(' '))
            {
                var text = _cmdText.AsSpan(range).Trim();
                if (text.IsEmpty)
                {
                    continue;
                }
                if (text[0] == '-')
                {
                    if (!lastParamName.IsEmpty)
                    {
                        _ = PrintPingHelpTextAsync($"Error: missing value for option \"-{lastParamName.ToString()}\"");
                        return;
                    }
                    lastParamName = text.Slice(1);
                    switch (lastParamName)
                    {
                        case "F":
                            if (isDFRequested is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-F\" specified multiple times", _botClient);
                                return;
                            }
                            isDFRequested = true;
                            lastParamName = string.Empty;
                            break;
                        case "c":
                            if (pingCount is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-c\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "s":
                            if (size is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-s\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "t":
                            if (timeoutMS is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-t\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "i":
                            if (intervalMS is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-i\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "4":
                            if (isForceUseIPv4)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-4\" specified multiple times", _botClient);
                                return;
                            }
                            else if (isForceUseIPv6)
                            {
                                _ = _userMessage.ReplyAsync("Error: options \"-4\" and \"-6\" are mutually exclusive", _botClient);
                                return;
                            }
                            isForceUseIPv4 = true;
                            lastParamName = string.Empty;
                            break;
                        case "6":
                            if (isForceUseIPv6)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-6\" specified multiple times", _botClient);
                                return;
                            }
                            else if (isForceUseIPv4)
                            {
                                _ = _userMessage.ReplyAsync("Error: options \"-4\" and \"-6\" are mutually exclusive", _botClient);
                                return;
                            }
                            isForceUseIPv6 = true;
                            lastParamName = string.Empty;
                            break;
                        default:
                            _ = PrintPingHelpTextAsync($"Error: unexpected option \"{text.ToString()}\"");
                            return;
                    }
                    continue;
                }
                else
                {
                    switch (lastParamName)
                    {
                        case "t":
                            if (int.TryParse(text, out var to) && to > 0)
                            {
                                timeoutMS = to;
                            }
                            else
                            {
                                _ = PrintPingHelpTextAsync($"Error: invalid timeout value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "i":
                            if (int.TryParse(text, out var i) && i >= 0)
                            {
                                intervalMS = i;
                            }
                            else
                            {
                                _ = PrintPingHelpTextAsync($"Error: invalid interval value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "c":
                            if (int.TryParse(text, out var c) && c > 0)
                            {
                                pingCount = c;
                            }
                            else
                            {
                                _ = PrintPingHelpTextAsync($"Error: invalid count value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "s":
                            if (int.TryParse(text, out var s) && s > 0)
                            {
                                size = s;
                            }
                            else
                            {
                                _ = PrintPingHelpTextAsync($"Error: invalid size value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        default:
                            if (IPAddress.TryParse(text, out var address))
                            {
                                if (hostAddress is not null || !string.IsNullOrEmpty(hostDomain))
                                {
                                    _ = PrintPingHelpTextAsync("Error: multiple destination specified");
                                    return;
                                }
                                else if (!IPNetworkHelper.IsInDN42AddressSpace(address))
                                {
                                    _ = PrintPingHelpTextAsync("Error: IP address out of range");
                                    return;
                                }
                                hostAddress = address;
                            }
                            else if (text.Length > 5 && text.Slice(text.Length - 5).Equals(".dn42", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrEmpty(hostDomain) || hostAddress is not null)
                                {
                                    _ = PrintPingHelpTextAsync("Error: multiple destination specified");
                                    return;
                                }
                                hostDomain = text.ToString();
                            }
                            else
                            {
                                _ = PrintPingHelpTextAsync($"Error: invalid destination \"{text.ToString()}\"");
                                return;
                            }
                            break;
                    }
                    lastParamName = string.Empty;
                }
            }
            if (string.IsNullOrEmpty(hostDomain) && hostAddress is null)
            {
                await PrintPingHelpTextAsync("Error: missing destination");
                return;
            }
            else if (hostAddress is null)
            {
                hostAddress = await GetIPAddressFromArgs(hostDomain, isForceUseIPv4, isForceUseIPv6);
                if (hostAddress is null)
                {
                    await _userMessage.ReplyAsync($"Error: cannot resolve domain \"{hostDomain}\"", _botClient);
                    return;
                }
            }
            size ??= 56;
            intervalMS ??= 500;
            isDFRequested ??= false;
            pingCount ??= 4;
            timeoutMS ??= 2000;
            #endregion
            var header = string.Empty;
            if (!string.IsNullOrEmpty(hostDomain))
            {
                header = $"PING {hostDomain} ({hostAddress}) with {size} bytes of data";
            }
            else
            {
                header = $"PING {hostAddress} with {size} bytes of data";
            }
            if (_sentMessage is null)
            {
                _sentMessage = await _userMessage.ReplyAsync(MessageEntityBulilder.Html.CodeBlock(header, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
                await _chatRoom.RegisterCallbackAsync(_sentMessage.Id, this);
                isRegistedCallback = true;
            }
            else
            {
                await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(header, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
            }

            #region Ping core
            var buffer = new byte[(int)size];
            var pingOptions = new PingOptions()
            {
                DontFragment = (bool)isDFRequested,
                Ttl = 128,
                Timeout = TimeSpan.FromMilliseconds((int)timeoutMS)
            };
            var resultStr = string.Empty;
            var packetLostCount = 0;
            var totalRTT = TimeSpan.Zero;
            var minRTT = TimeSpan.MaxValue;
            var maxRTT = TimeSpan.Zero;

            for (var i = 0; i < pingCount; i++)
            {
                var reply = default(PingReply);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    reply = await NetUtils.PingAsync(hostAddress!, buffer, pingOptions, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    BotLogger.LogError(e.ToString());
                    break;
                }
                BotLogger.LogDebug($"/ping|D:{hostAddress}|H:{reply.Address}|RSP:{reply.Status}|RTT:{reply.RoundtripTime}ms");
                switch (reply.Status)
                {
                    case IPStatus.Success:
                        resultStr += $"Reply from {reply.Address}: Seq={i + 1} TTL={((PingOptions)(reply.Options!)).Ttl} Time={reply.RoundtripTime.TotalMilliseconds:F2}ms\n";
                        totalRTT += reply.RoundtripTime;
                        minRTT = reply.RoundtripTime < minRTT ? reply.RoundtripTime : minRTT;
                        maxRTT = reply.RoundtripTime > maxRTT ? reply.RoundtripTime : maxRTT;
                        break;
                    case IPStatus.TimedOut:
                        resultStr += $"Request timed out\n";
                        totalRTT += reply.RoundtripTime;
                        maxRTT = reply.RoundtripTime > maxRTT ? reply.RoundtripTime : maxRTT;
                        packetLostCount++;
                        break;
                    case IPStatus.PacketTooBig:
                    case IPStatus.DestinationUnreachable:
                    case IPStatus.DestinationHostUnreachable:
                    case IPStatus.DestinationNetworkUnreachable:
                    case IPStatus.DestinationPortUnreachable:
                    case IPStatus.DestinationProhibited:
                        resultStr += $"Reply from {reply.Address}: {reply.Status}\n";
                        totalRTT += reply.RoundtripTime;
                        packetLostCount++;
                        minRTT = reply.RoundtripTime < minRTT ? reply.RoundtripTime : minRTT;
                        maxRTT = reply.RoundtripTime > maxRTT ? reply.RoundtripTime : maxRTT;
                        break;
                    default:
                        resultStr += $"Internal error\n";
                        totalRTT += reply.RoundtripTime;
                        packetLostCount++;
                        minRTT = reply.RoundtripTime < minRTT ? reply.RoundtripTime : minRTT;
                        maxRTT = reply.RoundtripTime > maxRTT ? reply.RoundtripTime : maxRTT;
                        break;
                }
                await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(
                    $"""
                    {header}
                    {resultStr}
                    """, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
                await Task.Delay((int)intervalMS);
            }
            #endregion
            var footer = string.Empty;
            if (!string.IsNullOrEmpty(hostDomain))
            {
                footer = $"--- {hostDomain} Ping statistics ---\n";
            }
            else
            {
                footer = $"--- {hostAddress} Ping statistics ---\n";
            }
            footer += $"""
                       {packetLostCount / (double)pingCount * 100:F2}% packet loss, time {totalRTT.TotalMilliseconds:F2}ms
                       RTT min/avg/max = {minRTT.TotalMilliseconds:F2}/{totalRTT.TotalMilliseconds / pingCount:F2}/{maxRTT.TotalMilliseconds:F2} ms
                       """;
            await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock($"""
                {header}
                {resultStr}
                {footer}
                """, "bash") + (cancellationToken.IsCancellationRequested ? "\nOperation had been cancelled by user" : ""),
                    _botClient, ParseMode.Html, inlineKeyboardWithRetry);
        }
        finally
        {
            if(isRegistedCallback)
            {
                State = ControllerStatus.StandBy;
            }
            else
            {
                State = ControllerStatus.Completed;
            }
        }
    }
    async Task TraceAsync()
    {
        var cancellationToken = _cts.Token;
        var isRegistedCallback = false;
        try
        {
            if (string.IsNullOrEmpty(_cmdText))
            {
                await PrintTraceHelpTextAsync();
                return;
            }
            var inlineKeyboardWithCancel = new InlineKeyboardMarkup(new InlineKeyboardButton("Cancel", "cancel"));
            var inlineKeyboardWithRetry = new InlineKeyboardMarkup(new InlineKeyboardButton("Retry", "retry"));
            var maxTTL = default(int?);
            var firstTTL = default(int?);
            var timeoutMS = default(int?);
            var queryCount = default(int?);
            var packetSize = default(int?);
            var isDFRequested = default(bool?);
            var intervalMS = default(int?);
            var isForceUseIPv4 = false;
            var isForceUseIPv6 = false;
            var hostAddress = default(IPAddress);
            var hostDomain = string.Empty;
            var lastParamName = ReadOnlySpan<char>.Empty;
            #region Arguments Parsing
            foreach (var range in _cmdText.AsSpan().Split(' '))
            {
                var text = _cmdText.AsSpan(range).Trim();
                if (text.IsEmpty)
                {
                    continue;
                }
                if (text[0] == '-')
                {
                    if (!lastParamName.IsEmpty)
                    {
                        _ = PrintTraceHelpTextAsync($"Error: missing value for option \"-{lastParamName.ToString()}\"");
                        return;
                    }
                    lastParamName = text.Slice(1);
                    switch (lastParamName)
                    {
                        case "F":
                            if (isDFRequested is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-F\" specified multiple times", _botClient);
                                return;
                            }
                            isDFRequested = true;
                            lastParamName = string.Empty;
                            break;
                        case "f":
                            if (firstTTL is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-f\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "m":
                            if (maxTTL is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-m\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "q":
                            if (queryCount is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-q\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "s":
                            if (packetSize is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-s\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "t":
                            if (timeoutMS is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-t\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "i":
                            if (intervalMS is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-i\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "4":
                            if (isForceUseIPv4)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-4\" specified multiple times", _botClient);
                                return;
                            }
                            else if (isForceUseIPv6)
                            {
                                _ = _userMessage.ReplyAsync("Error: options \"-4\" and \"-6\" are mutually exclusive", _botClient);
                                return;
                            }
                            isForceUseIPv4 = true;
                            lastParamName = string.Empty;
                            break;
                        case "6":
                            if (isForceUseIPv6)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-6\" specified multiple times", _botClient);
                                return;
                            }
                            else if (isForceUseIPv4)
                            {
                                _ = _userMessage.ReplyAsync("Error: options \"-4\" and \"-6\" are mutually exclusive", _botClient);
                                return;
                            }
                            isForceUseIPv6 = true;
                            lastParamName = string.Empty;
                            break;
                        default:
                            _ = PrintTraceHelpTextAsync($"Error: unexpected option \"{text.ToString()}\"");
                            return;
                    }
                    continue;
                }
                else
                {
                    switch (lastParamName)
                    {
                        case "t":
                            if (int.TryParse(text, out var to) && to > 0)
                            {
                                timeoutMS = to;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid timeout value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "f":
                            if (int.TryParse(text, out var f) && f >= 0)
                            {
                                firstTTL = f;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid firstTTL value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "m":
                            if (int.TryParse(text, out var m) && m >= 0)
                            {
                                maxTTL = m;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid maxTTL value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "i":
                            if (int.TryParse(text, out var i) && i >= 0)
                            {
                                intervalMS = i;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid interval value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "q":
                            if (int.TryParse(text, out var q) && q > 0)
                            {
                                queryCount = q;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid query count value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "s":
                            if (int.TryParse(text, out var s) && s >= 0)
                            {
                                packetSize = s;
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid size value \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        default:
                            if (IPAddress.TryParse(text, out var address))
                            {
                                if (hostAddress is not null || !string.IsNullOrEmpty(hostDomain))
                                {
                                    _ = _userMessage.ReplyAsync("Error: multiple destination specified", _botClient);
                                    return;
                                }
                                else if (!IPNetworkHelper.IsInDN42AddressSpace(address))
                                {
                                    _ = _userMessage.ReplyAsync("Error: IP address out of range", _botClient);
                                    return;
                                }
                                hostAddress = address;
                            }
                            else if (text.Length > 5 && text.Slice(text.Length - 5).Equals(".dn42", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrEmpty(hostDomain) || hostAddress is not null)
                                {
                                    _ = _userMessage.ReplyAsync("Error: multiple destination specified", _botClient);
                                    return;
                                }
                                hostDomain = text.ToString();
                            }
                            else
                            {
                                _ = PrintTraceHelpTextAsync($"Error: invalid destination \"{text.ToString()}\"");
                                return;
                            }
                            break;
                    }
                    lastParamName = string.Empty;
                }
            }
            if (string.IsNullOrEmpty(hostDomain) && hostAddress is null)
            {
                await PrintTraceHelpTextAsync("Error: missing destination");
                return;
            }
            else if(hostAddress is null)
            {
                hostAddress = await GetIPAddressFromArgs(hostDomain, isForceUseIPv4, isForceUseIPv6);
                if (hostAddress is null)
                {
                    await _userMessage.ReplyAsync($"Error: cannot resolve domain \"{hostDomain}\"", _botClient);
                    return;
                }
            }
            firstTTL ??= 1;
            maxTTL ??= 10;
            packetSize ??= 56;
            intervalMS ??= 50;
            isDFRequested ??= false;
            queryCount ??= 1;
            timeoutMS ??= 2000;
            #endregion
            var header = string.Empty;
            if (!string.IsNullOrEmpty(hostDomain))
            {
                header = $"traceroute to {hostDomain} ({hostAddress}), {maxTTL} hops max, with {packetSize} bytes of data";
            }
            else
            {
                header = $"traceroute to {hostAddress}, {maxTTL} hops max, with {packetSize} bytes of data";
            }
            if (_sentMessage is null)
            {
                _sentMessage = await _userMessage.ReplyAsync(MessageEntityBulilder.Html.CodeBlock(header, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
                await _chatRoom.RegisterCallbackAsync(_sentMessage.Id, this);
                isRegistedCallback = true;
            }
            else
            {
                await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(header, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
            }
            #region Trace core
            var pingSender = new Ping();
            var buffer = new byte[(int)packetSize];
            var pingOptions = new PingOptions()
            {
                DontFragment = (bool)isDFRequested,
                Timeout = TimeSpan.FromMilliseconds((int)timeoutMS)
            };
            var resultStr = string.Empty;
            var hopSb = new StringBuilder();
            var hopAddresses = new HashSet<IPAddress?>();
            var hopRTT = new string[(int)queryCount];
            for (var hop = (short)firstTTL; hop <= maxTTL; hop++)
            {
                pingOptions.Ttl = hop;
                var isReached = false;
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                for (var i = 0; i < queryCount; i++)
                {
                    var hopResultStr = string.Empty;
                    var reply = default(PingReply);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        reply = await NetUtils.PingAsync(hostAddress!,buffer, pingOptions, cancellationToken);
                    }
                    catch(OperationCanceledException)
                    {
                        break;
                    }
                    catch(Exception e)
                    {
                        BotLogger.LogError(e.ToString());
                        break;
                    }
                    BotLogger.LogDebug($"/trace|D:{hostAddress}|H:{reply.Address}|RSP:{reply.Status}|RTT:{reply.RoundtripTime.TotalMilliseconds}ms");
                    switch (reply.Status)
                    {
                        case IPStatus.Success:
                        case IPStatus.TtlExpired:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms";
                            hopAddresses.Add(reply.Address);
                            break;
                        case IPStatus.TimedOut:
                            hopRTT[i] = " * ";
                            hopAddresses.Add(null);
                            break;
                        case IPStatus.PacketTooBig:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !F";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        case IPStatus.DestinationUnreachable:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !U";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        case IPStatus.DestinationHostUnreachable:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !H";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        case IPStatus.DestinationNetworkUnreachable:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !N";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        case IPStatus.DestinationPortUnreachable:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !P";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        case IPStatus.DestinationProhibited:
                            hopRTT[i] = $"{reply.RoundtripTime.TotalMilliseconds:F2}ms !X";
                            hopAddresses.Add(reply.Address);
                            isReached = true;
                            break;
                        default:
                            hopRTT[i] = " E ";
                            hopAddresses.Add(null);
                            break;
                    }
                    if (reply.Address.Equals(hostAddress))
                    {
                        isReached = true;
                    }
                    hopSb.Clear();
                    var hopHeader = $"{hop}".PadRight(4);
                    if (!string.IsNullOrEmpty(resultStr))
                    {
                        hopSb.AppendLine(resultStr);
                    }
                    hopSb.Append(hopHeader);
                    var rttLeftPaddingLength = OUTPUT_TRACE_RTT_LEFT_PADDING_LENGTH;
                    var isHasHopAddress = hopAddresses.Any(x => x is not null);
                    var isFirst = true;
                    foreach (var (index, hopAddress) in hopAddresses.Index())
                    {
                        var isLast = index == hopAddresses.Count - 1;
                        if (hopAddress is null)
                        {
                            if (isHasHopAddress)
                            {
                                continue;
                            }
                            rttLeftPaddingLength = OUTPUT_TRACE_RTT_LEFT_PADDING_LENGTH;
                            hopSb.Append('*')
                                 .AppendLine();
                        }
                        else
                        {
                            if (!isFirst)
                            {
                                hopSb.Append("    ");
                                isFirst = false;
                            }
                            rttLeftPaddingLength = OUTPUT_TRACE_RTT_LEFT_PADDING_LENGTH;
                            var whoisResult = default(WhoisQueryResult<uint>);
                            var asn = "*";
                            var isDnsHasError = false;
                            var dnsQueryResult = default(IDnsQueryResponse);
                            try
                            {
                                var dnsReq = LookupClient.GetReverseQuestion(hopAddress);
                                var isSuccessfully = false;
                                (isSuccessfully, dnsQueryResult) = await DnsLookupAsync(dnsReq, token: cancellationToken);
                                whoisResult = await WhoisHelper.QueryASNFromIPAddressAsync(hopAddress, cancellationToken);
                                isDnsHasError = !isSuccessfully;
                            }
                            catch(OperationCanceledException)
                            {
                                break;
                            }
                            catch
                            {
                                isDnsHasError = true;
                            }

                            if (whoisResult.IsSuccessfully)
                            {
                                asn = $"AS{whoisResult.Result}";
                            }
                            if (isDnsHasError || (dnsQueryResult?.HasError ?? true))
                            {
                                var start = hopSb.Length;
                                hopSb.Append(hopAddress);
                                var end = hopSb.Length;
                                var len = end - start;
                                var padding = 44 - len;
                                for (var j = 0; j < padding; j++)
                                {
                                    hopSb.Append(' ');
                                }
                                hopSb.Append(asn)
                                     .AppendLine();
                            }
                            else
                            {
                                var hostname = dnsQueryResult!.Answers.PtrRecords()?.FirstOrDefault()?.PtrDomainName ?? hopAddress.ToString();
                                var start = hopSb.Length;
                                hopSb.Append(hostname);
                                var end = hopSb.Length;
                                var len = end - start;
                                var padding = 44 - len;
                                for (var j = 0; j < padding; j++)
                                {
                                    hopSb.Append(' ');
                                }
                                hopSb.Append(asn)
                                     .AppendLine();

                                hopSb.Append("    ");
                                start = hopSb.Length;
                                hopSb.Append('(')
                                     .Append(hopAddress)
                                     .Append(')');
                                if(isLast)
                                {
                                    end = hopSb.Length;
                                    len = end - start + 4;
                                    rttLeftPaddingLength -= (uint)len;
                                }
                                else
                                {
                                    hopSb.AppendLine();
                                }
                            }
                        }
                    }
                    for (var x = 0; x < rttLeftPaddingLength; x++)
                    {
                        hopSb.Append(' ');
                    }
                    hopSb.AppendJoin('/', hopRTT.AsSpan(0, i + 1));
                    hopResultStr = hopSb.ToString();
                    await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(
                        $"""
                        {header}
                        {hopResultStr}
                        """, "bash"), _botClient, ParseMode.Html, inlineKeyboardWithCancel);
                }
                resultStr = hopSb.ToString();
                hopSb.Clear();
                hopAddresses.Clear();
                if (isReached)
                {
                    break;
                }
            }
            await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(
                $"""
                {header}
                {resultStr}
                """, "bash") + (cancellationToken.IsCancellationRequested ? "\nOperation had been cancelled by user" : ""), 
                _botClient, ParseMode.Html, inlineKeyboardWithRetry);
            #endregion
        }
        finally
        {
            if (isRegistedCallback)
            {
                State = ControllerStatus.StandBy;
            }
            else
            {
                State = ControllerStatus.Completed;
            }
        }
    }
    async Task NsLookupAsync()
    {
        var cancellationToken = _cts.Token;
        var isRegistedCallback = false;
        try
        {
            if (string.IsNullOrEmpty(_cmdText))
            {
                await PrintNsLookupHelpTextAsync();
                return;
            }
            var domain = default(string);
            var nsAddr = default(IPAddress);
            var port = default(int?);
            var queryTypes = new HashSet<QueryType>();
            var lastParamName = ReadOnlySpan<char>.Empty;
            #region Arguments Parsing
            foreach (var range in _cmdText.AsSpan().Split(' '))
            {
                var text = _cmdText.AsSpan(range).Trim();
                if (text.IsEmpty)
                {
                    continue;
                }
                if (text[0] == '-')
                {
                    if (!lastParamName.IsEmpty)
                    {
                        _ = PrintNsLookupHelpTextAsync($"Error: missing value for option \"-{lastParamName.ToString()}\"");
                        return;
                    }
                    lastParamName = text.Slice(1);
                    switch (lastParamName)
                    {
                        case "s":
                            if (nsAddr is not null)
                            {
                                _ = _userMessage.ReplyAsync("Error: option \"-s\" specified multiple times", _botClient);
                                return;
                            }
                            break;
                        case "p":
                        case "t":
                            break;                        
                        default:
                            _ = PrintNsLookupHelpTextAsync($"Error: unexpected option \"{text.ToString()}\"");
                            return;
                    }
                    continue;
                }
                else
                {
                    switch (lastParamName)
                    {
                        case "s":
                            if(!IPAddress.TryParse(text, out nsAddr))
                            {
                                _ = PrintNsLookupHelpTextAsync($"Error: invalid ip address format \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "t":
                            if (Enum.TryParse<QueryType>(text, true, out var q))
                            {
                                queryTypes.Add(q);
                            }
                            else
                            {
                                _ = PrintNsLookupHelpTextAsync($"Error: invalid record type \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        case "p":
                            if (ushort.TryParse(text, out var p))
                            {
                                port = p;
                            }
                            else
                            {
                                _ = PrintNsLookupHelpTextAsync($"Error: invalid ns port \"{text.ToString()}\"");
                                return;
                            }
                            break;
                        default:
                            if (text.Length > 5 && text.Slice(text.Length - 5).Equals(".dn42", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrEmpty(domain) || domain is not null)
                                {
                                    _ = _userMessage.ReplyAsync("Error: multiple destination specified", _botClient);
                                    return;
                                }
                                domain = text.ToString();
                            }
                            else
                            {
                                _ = PrintNsLookupHelpTextAsync($"Error: invalid domain \"{text.ToString()}\"");
                                return;
                            }
                            break;
                    }
                    lastParamName = string.Empty;
                }
            }
            if (string.IsNullOrEmpty(domain))
            {
                await PrintNsLookupHelpTextAsync("Error: missing domain");
                return;
            }
            nsAddr ??= DEFAULT_NAMESERVER_ADDRESS;
            if(queryTypes.Count == 0)
            {
                queryTypes.Add(QueryType.A);
                queryTypes.Add(QueryType.AAAA);
            }
            port ??= 53;
            #endregion
            var dnsOptions = new LookupClientOptions(new NameServer(nsAddr, (int)port))
            {
                UseCache = false,
                Timeout = TimeSpan.FromSeconds(2)
            };
            var header = $"""
                Address: {nsAddr}#{port}
                """;
            var sb = new StringBuilder();
            var errSb = new StringBuilder();
            var isAnySuccessfully = false;
            using var dnsQueryResults = new RentedList<(bool IsSuccessfully, IDnsQueryResponse? Response)>();
            if(_sentMessage is null)
            {
                _sentMessage = await _userMessage.ReplyAsync(MessageEntityBulilder.Html.CodeBlock(header + "\n\nQuerying", "bash"), _botClient, ParseMode.Html);
            }
            else
            {
                await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock(header + "\n\nQuerying", "bash"), _botClient, ParseMode.Html);
            }

            try
            {
                foreach(var queryType in queryTypes)
                {
                    var (isSuccessfully, dnsQueryResult) = await DnsLookupAsync(domain, queryType, QueryClass.IN, dnsOptions, true, cancellationToken);
                    dnsQueryResults.Add((isSuccessfully, dnsQueryResult));
                }
                using var dnsRecords = new RentedList<DnsResourceRecord>();
                foreach(var (isSuccessfully, dnsQueryResult) in dnsQueryResults)
                {
                    isAnySuccessfully |= isSuccessfully;
                    if (!isSuccessfully)
                    {
                        if (dnsQueryResult is null)
                        {
                            errSb.AppendLine($"can't find {domain}");
                        }
                        else
                        {
                            errSb.AppendLine($"can't find {domain}: {dnsQueryResult.ErrorMessage}");
                        }
                        continue;
                    }
                    dnsRecords.AddRange(dnsQueryResult!.Answers);
                }
                var groupedResult = dnsRecords.GroupBy(x => x.RecordType);
                foreach (var resultGroup in groupedResult)
                {
                    var prefix = string.Empty;
                    switch (resultGroup.Key)
                    {
                        case ResourceRecordType.A:
                            prefix = "IP";
                            break;
                        case ResourceRecordType.AAAA:
                            prefix = "IP6";
                            break;
                        default:
                            prefix = resultGroup.Key.ToString();
                            break;
                    }
                    foreach (var result in resultGroup)
                    {
                        var value = string.Empty;
                        switch (resultGroup.Key)
                        {
                            case ResourceRecordType.A:
                                value = ((AddressRecord)result).Address.ToString();
                                break;
                            case ResourceRecordType.AAAA:
                                value = ((AddressRecord)result).Address.ToString();
                                break;
                            case ResourceRecordType.NS:
                                value = ((NsRecord)result).NSDName.Value;
                                break;
                            case ResourceRecordType.CNAME:
                                value = ((CNameRecord)result).CanonicalName.Value;
                                break;
                            case ResourceRecordType.MX:
                                var mx = (MxRecord)result;
                                value = $"{mx.Exchange.Value} (Priority={mx.Preference})";
                                break;
                            case ResourceRecordType.TXT:
                                value = string.Join(" ", ((TxtRecord)result).Text);
                                break;
                            case ResourceRecordType.PTR:
                                value = ((PtrRecord)result).PtrDomainName.Value;
                                break;
                            case ResourceRecordType.SRV:
                                var srv = (SrvRecord)result;
                                value = $"{srv.Target.Value}:{srv.Port} (Priority={srv.Priority}, Weight={srv.Weight})";
                                break;
                            case ResourceRecordType.HINFO:
                                var hinfo = (HInfoRecord)result;
                                value = $"CPU={hinfo.Cpu}, OS={hinfo.OS}";
                                break;
                            case ResourceRecordType.AFSDB:
                                var afsdb = (AfsDbRecord)result;
                                value = $"{afsdb.Hostname.Value} (Subtype={afsdb.SubType})";
                                break;
                            case ResourceRecordType.CAA:
                                var caa = (CaaRecord)result;
                                value = $"{caa.Tag} {caa.Value}";
                                break;
                            case ResourceRecordType.URI:
                                var uri = (UriRecord)result;
                                value = $"{uri.Target} (Priority={uri.Priority}, Weight={uri.Weight}, TTL={uri.TimeToLive})";
                                break;
                            default:
                                value = result.ToString();
                                break;
                        }
                        sb.Append(prefix)
                          .Append(':')
                          .AppendLine(value);
                    }
                }

                sb.AppendLine()
                  .AppendLine(errSb.ToString());

                await _sentMessage.EditAsync(MessageEntityBulilder.Html.CodeBlock($"""
                        {header}

                        Records:
                        {sb.ToString()}
                        """, "bash"), _botClient, ParseMode.Html);
            }
            catch (Exception e)
            {
                BotLogger.LogError(e);
            }
        }
        finally
        {
            if (isRegistedCallback)
            {
                State = ControllerStatus.StandBy;
            }
            else
            {
                State = ControllerStatus.Completed;
            }
        }
    }
    async Task PrintPingHelpTextAsync(string text = "")
    {
        var content = text + MessageEntityBulilder.Html.CodeBlock(
            """
            Usage:
                /ping [options] <dest>

            Options:
                -c <count>    number of echo requests to send (default: 4)
                -s <size>     use <size> as number of data bytes to be sent
                -F            do not fragment packets
                -t <timeout>  time to wait for response (default: 2000ms)
                -i <interval> ms between sending each packet (default: 500ms)
                -4            use IPv4
                -6            use IPv6
            """, "bash");
        if(_sentMessage is null)
        {
            _sentMessage = await _userMessage.ReplyAsync(content, _botClient, ParseMode.Html);
        }
        else
        {
            await _sentMessage.EditAsync(content, _botClient, ParseMode.Html);
        }
    }
    async Task PrintTraceHelpTextAsync(string text = "")
    {
        var content = text + MessageEntityBulilder.Html.CodeBlock(
            """
            Usage:
                /trace [options] <dest>

            Options:
                -f <firstTTL>       start from the <firstTTL> hop (default: 1)
                -m <maxTTL>         set the max number of hops (default: 10)
                -s <size>           use <size> as number of data bytes to be sent
                -t <timeout>        time to wait for response (default: 2000ms)
                -q <queryCount>     set the number of probes per each hop (default: 1)
                -i <interval>       ms between sending each packet (default: 50ms)
                -F                  do not fragment packets
                -M                  mtr mode
                -4                  use IPv4
                -6                  use IPv6
            """, "bash");
        if (_sentMessage is null)
        {
            _sentMessage = await _userMessage.ReplyAsync(content, _botClient, ParseMode.Html);
        }
        else
        {
            await _sentMessage.EditAsync(content, _botClient, ParseMode.Html);
        }
    }
    async Task PrintNsLookupHelpTextAsync(string text = "")
    {
        var content = text + MessageEntityBulilder.Html.CodeBlock(
            """
            Usage:
                /dig      [options] <domain>
                /nslookup [options] <domain>

            Options:
                -t <query type>        specify the DNS query type (e.g. A, AAAA, MX, NS, TXT)
                -s <IPv4|IPv6 addr>    specify the DNS server (IPv4 or IPv6 address) to query
                -p <port>              specify port number
            """, "bash");
        if (_sentMessage is null)
        {
            _sentMessage = await _userMessage.ReplyAsync(content, _botClient, ParseMode.Html);
        }
        else
        {
            await _sentMessage.EditAsync(content, _botClient, ParseMode.Html);
        }
    }
    public static IController FromRequest(UserRequest request, ChatRoom chatRoom)
    {
        ArgumentNullException.ThrowIfNull(chatRoom, nameof(request));
        return new NetworkController(request, chatRoom);
    }
    public void Dispose()
    {
        using (_executeLock.Lock())
        {
            if (State == ControllerStatus.Disposed)
            {
                return;
            }
            _cts.Cancel();
            State = ControllerStatus.Disposed;
        }
    }
    public async ValueTask DisposeAsync()
    {
        using (await _executeLock.LockAsync())
        {
            if (State == ControllerStatus.Disposed)
            {
                return;
            }
            try
            {
                if (!_cts.IsCancellationRequested)
                {
                    await _cts.CancelAsync();
                }
                if (_sentMessage is not null)
                {
                    await _chatRoom.UnregisterCallbackAsync(_sentMessage.Id);
                    await _sentMessage.DeleteAsync(_botClient);
                }
            }
            finally
            {
                State = ControllerStatus.Disposed;
            }
        }
    }
    public bool Equals(IController? other)
    {
        if(other is null)
        {
            return false;
        }
        return other.RequestId == _requestId;
    }
    async ValueTask<IPAddress?> GetIPAddressFromArgs(string hostname, bool forceIPv4, bool forceIPv6, CancellationToken token = default)
    {
        var queryType = QueryType.AAAA;
        var isForce = forceIPv4 || forceIPv6;
        if (forceIPv4)
        {
            queryType = QueryType.A;
        }
        else if (forceIPv6)
        {
            queryType = QueryType.AAAA;
        }
    RETRY:
        var (isSuccessfully, queryResult) = await DnsLookupAsync(hostname, queryType,token: token);
        if (!isSuccessfully || queryResult is null)
        {
            goto FALLBACK;
        }
        var answers = queryResult!.Answers;
        if (!queryResult.HasError)
        {
            if(answers.Count == 0)
            {
                goto FALLBACK;
            }
            else if(answers.Count != 0)
            {
                var record = answers.AaaaRecords().Concat(answers.AddressRecords()).FirstOrDefault();
                if (record is not null)
                {
                    return record.Address;
                }
            }
        }
        return null;
    FALLBACK:
        if (!isForce && queryType != QueryType.A)
        {
            queryType = QueryType.A;
            goto RETRY;
        }
        return null;
    }
    ValueTask<(bool IsSuccessfully, IDnsQueryResponse? Response)> DnsLookupAsync(string query,
        QueryType queryType = QueryType.A,
        QueryClass queryClass = QueryClass.IN,
        DnsQueryAndServerOptions? options = null,
        bool bypassCache = false,
        CancellationToken token = default)
    {
        return DnsLookupAsync(new DnsQuestion(query, queryType, queryClass),options, bypassCache, token);
    }
    async ValueTask<(bool IsSuccessfully,IDnsQueryResponse? Response)> DnsLookupAsync(DnsQuestion query, 
        DnsQueryAndServerOptions? options = null, 
        bool bypassCache = false,
        CancellationToken token = default)
    {
        var queryResult = default(IDnsQueryResponse);
        var isSuccessfully = false;
        if (!bypassCache)
        {
            queryResult = _dnsClient.QueryCache(query);
        }
        if (queryResult is null)
        {
            try
            {
                queryResult = await _dnsClient.QueryAsync(query, options ?? _dnsLookupOptions, token);
                isSuccessfully = !queryResult.HasError;
            }
            catch(OperationCanceledException)
            {
                return (isSuccessfully, queryResult);
            }
            catch (Exception e)
            {
                BotLogger.LogError(e);
            }
        }
        else
        {
            isSuccessfully = !queryResult.HasError;
        }
        return (isSuccessfully, queryResult);
    }
}
