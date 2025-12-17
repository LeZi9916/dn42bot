using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace dn42Bot.TelegramBot;

internal static class MessageExtensions
{
    public static async Task<Message> ReplyAsync(this Message origin,string text, ITelegramBotClient client, 
        ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = default,
        CancellationToken token = default)
    {
        var chatId = origin.Chat.Id;
        return await client.SendMessage(chatId, 
            text, 
            parseMode,
            replyParameters: new()
            {
                ChatId = chatId,
                MessageId = origin.MessageId,
                AllowSendingWithoutReply = true
            },
            replyMarkup: replyMarkup,
            cancellationToken: token);
    }
    public static async Task<Message> EditAsync(this Message origin, string text, ITelegramBotClient client,
        ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = default,
        CancellationToken token = default)
    {
        return await client.EditMessageText(origin.Chat.Id, 
            origin.MessageId, 
            text, 
            parseMode, 
            replyMarkup: replyMarkup,
            cancellationToken: token);
    }
    public static async Task DeleteAsync(this Message origin, ITelegramBotClient client, CancellationToken token = default)
    {
        await client.DeleteMessage(origin.Chat.Id, origin.MessageId, cancellationToken: token);
    }
}
