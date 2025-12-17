using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;

namespace dn42Bot.Chatting;

internal interface ICallbackController: IController
{
    void OnCallback(CallbackQuery query);
    ValueTask OnCallbackAsync(CallbackQuery query);
}
