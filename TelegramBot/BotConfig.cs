using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.TelegramBot;

internal class BotConfig
{
    public string Token { get; init; } = string.Empty;
    public BotConfig_Network Network { get; init; } = new();
}
internal class BotConfig_Network
{
    public bool UseProxy { get; init; }
    public string Proxy { get; init; } = string.Empty;
}
