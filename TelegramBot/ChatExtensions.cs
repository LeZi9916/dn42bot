using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace dn42Bot.TelegramBot;

internal static class ChatExtensions
{
    extension(Chat @this)
    {
        public bool IsPrivateChat
        {
            get
            {
                return @this.Type == ChatType.Private;
            }
        }
        public bool IsChannel
        {
            get
            {
                return @this.Type == ChatType.Channel;
            }
        }
        public bool IsGroup
        {
            get
            {
                return @this.Type == ChatType.Group || @this.Type == ChatType.Supergroup;
            }
        }
    }
    public static async Task<Message> SendMessageAsync(this Chat chat, string text, 
        ITelegramBotClient client, 
        ParseMode parseMode = ParseMode.None, 
        CancellationToken token = default)
    {
        return await client.SendMessage(chat.Id, text, parseMode, cancellationToken: token);
    }
}
