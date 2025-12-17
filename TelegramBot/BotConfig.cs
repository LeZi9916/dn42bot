using dn42bot.Text.JsonConverters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace dn42Bot.TelegramBot;

internal class BotConfig
{
    public string Token { get; init; } = string.Empty;
    public BotConfig_Network Network { get; init; } = new();
}
internal class BotConfig_Network
{
    public BotConfig_Network_DNS DNS { get; init; } = new();
    public BotConfig_Network_Whois Whois { get; init; } = new();
    public bool UseProxy { get; init; }
    public string Proxy { get; init; } = string.Empty;
}

internal class BotConfig_Network_DNS
{
    [JsonConverter(typeof(IPAddressConverter))]
    public IPAddress Address { get; init; } = IPAddress.Parse("172.20.0.53");
    public ushort Port { get; init; } = 53;
    public uint TimeoutMS { get; init; } = 500;
}
internal class BotConfig_Network_Whois
{
    [JsonConverter(typeof(IPAddressConverter))]
    public IPAddress Address { get; init; } = IPAddress.Parse("172.22.137.116");
    public ushort Port { get; init; } = 43;
}
