using System.Xml;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
namespace dn42Bot.Chatting;

public readonly struct UserRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public required User Sender { get; init; }
    public required Chat Chat { get; init; }
    public Message? Message { get; init; }
    public CommandEntity Command { get; init; }
    public string SpecifiedBotUsername { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsEdited { get; init; } = false;
    public bool IsBotSpecified { get; init; } = false;
    public required ITelegramBotClient Client { get; init; }

    public UserRequest()
    {

    }
    public static UserRequest Parse(ITelegramBotClient client, Update botUpdate)
    {
        var updateType = botUpdate.Type;
        var message = default(Message);
        var sender = default(User);
        var chat = default(Chat);
        var isBotSpecified = false;
        var specifiedBotUsername = string.Empty;

        switch (updateType)
        {
            case UpdateType.Message:
                message = botUpdate.Message!;
                sender = message.From!;
                chat = message.Chat;
                break;
            case UpdateType.EditedMessage:
                message = botUpdate.EditedMessage!;
                sender = message.From!;
                chat = message.Chat;
                break;
            default:
                throw new NotSupportedException();
        }
        var origText = message.Text ?? string.Empty;
        var cmdEntity = default(CommandEntity);

        // Command parsing
        if(!string.IsNullOrEmpty(origText) && origText[0] == '/')
        {
            var cmdText = origText.AsSpan(1);
            if (!cmdText.IsEmpty)
            {
                var ranges = (stackalloc Range[2]);
                var count = cmdText.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries);
                var prefix = ReadOnlySpan<char>.Empty;
                var text = ReadOnlySpan<char>.Empty;
                if (count == 1)
                {
                    prefix = cmdText;
                }
                else
                {
                    prefix = cmdText[ranges[0]];
                    text = cmdText[ranges[1]];
                }
                var atIndex = prefix.IndexOf('@');
                if (atIndex != -1 && atIndex != prefix.Length - 1)
                {
                    specifiedBotUsername = prefix.Slice(atIndex + 1).ToString();
                    prefix = prefix.Slice(0, atIndex);
                    isBotSpecified = true;
                }
                cmdEntity = new()
                {
                    Prefix = prefix.ToString(),
                    Text = text.ToString()
                };
            }
        }

        return new()
        {
            Sender = sender,
            Chat = chat,
            Command = cmdEntity,
            Message = message,
            Text = origText,
            SpecifiedBotUsername = specifiedBotUsername,
            IsEdited = updateType == UpdateType.EditedMessage,
            IsBotSpecified = isBotSpecified,
            Client = client
        };
    }
    static ReadOnlySpan<char> GetBotUsernameFromPrefix(in ReadOnlySpan<char> prefix)
    {
        var atIndex = prefix.IndexOf('@');
        if (atIndex != -1)
        {
            return prefix.Slice(atIndex + 1);
        }
        return ReadOnlySpan<char>.Empty;
    }
}
