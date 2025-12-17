using dn42Bot.Chatting;
using dn42Bot.Chatting.Controllers;
using dn42Bot.Network;
using dn42Bot.TelegramBot;
using dn42Bot.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace dn42Bot
{
    internal class Core
    {
        public static BotInfo BotProfile
        { 
            get
            {
                return new();
            }
        }
        public static DateTime StartAt { get; } = DateTime.Now;

        static long _botId = 0;
        static string _botUsername = string.Empty;

        static BotConfig _botConfig = new();
        static Task _chatRoomCleanUpTask = Task.CompletedTask;
        static TelegramBotClient _botClient = null!;
        static HttpClient _botHttpClient = new HttpClient();

        readonly static string WORKING_DIRECTORY_PATH = Environment.CurrentDirectory;
        readonly static IReadOnlyDictionary<string, string> PRE_DEFINED_COMMAND_LIST = new Dictionary<string, string>()
        {
            { "/ping", "Ping DN42 network" },
            { "/trace", "Traceroute on DN42 network" },
            { "/nslookup", "DNS query" },
            { "/dig", "DNS query" },
        };

        static async Task Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            {
                BotLogger.LogError($"Unobserved exception: {eventArgs}");
                eventArgs.SetObserved();
            };
            BotLogger.LogInfo("Bot is starting...");
            var configPath = Path.Combine(WORKING_DIRECTORY_PATH, "config.json");
            var jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            if(File.Exists(configPath))
            {
                BotLogger.LogInfo($"Reading bot configuration from \"{configPath}\"");
                try
                {
                    var botConfig = await JsonSerializer.DeserializeAsync<BotConfig>(File.OpenRead(configPath), jsonOptions);
                    if(botConfig is null)
                    {
                        Fatal("Failed to parse the configuration file");
                    }
                    if(string.IsNullOrEmpty(botConfig.Token))
                    {
                        Fatal("API token not specified");
                    }
                    _botConfig = botConfig;
                    var useProxy = botConfig.Network.UseProxy;
                    var proxyUriStr = botConfig.Network.Proxy;
                    var proxy = default(IWebProxy);

                    if (string.IsNullOrEmpty(proxyUriStr))
                    {
                        proxy = WebRequest.GetSystemWebProxy();
                    }
                    else if (!Uri.TryCreate(proxyUriStr, UriKind.RelativeOrAbsolute, out var proxyUri))
                    {
                        Fatal("Proxy address format is incorrect");
                    }
                    else
                    {
                        if(proxyUri.Scheme is not ("http" or "https"))
                        {
                            Fatal("Unsupported proxy protocol");
                        }
                        proxy = new WebProxy(proxyUriStr);
                    }

                    _botHttpClient = new(new HttpClientHandler()
                    {
                        UseProxy = botConfig.Network.UseProxy,
                        Proxy = proxy
                    });
                }
                catch(Exception e)
                {
                    BotLogger.LogError(e);
                    BotLogger.LogError("Failed to parse the configuration file");
                    Thread.Sleep(1000);
                    Environment.Exit(-1);
                }
            }
            else
            {
                var json = JsonSerializer.Serialize(_botConfig, jsonOptions);
                File.WriteAllText(configPath, json);
                BotLogger.LogInfo("Configuration file has been generated");
                Thread.Sleep(1000);
                Environment.Exit(0);
            }
            _botClient = new TelegramBotClient(_botConfig.Token, _botHttpClient);
            BotLogger.LogInfo("Logging into telegran.org...");
            var isValid = await _botClient.TestApi();
            if (isValid)
            {
                BotLogger.LogInfo("Logged in successfully");

            }
            else
            {
                Fatal("Failed to log in. Check the API token", -1);
            }
            var botDetails = await _botClient.GetMe();
            BotLogger.LogInfo($"#######################   Bot Profile   #######################");
            BotLogger.LogInfo($"Id      : {botDetails.Id}");
            BotLogger.LogInfo($"Name    : {botDetails.FirstName} {botDetails.LastName}");
            BotLogger.LogInfo($"Username: {botDetails.Username}");
            BotLogger.LogInfo($"####################### Bot Profile End #######################");
            _botId = botDetails.Id;
            _botUsername = botDetails.Username ?? string.Empty;
            BotLogger.LogInfo("Updating bot command list...");
            await _botClient.SetMyCommands(PRE_DEFINED_COMMAND_LIST.Select(x =>
            {
                var (cmd, desc) = x;
                return new BotCommand(cmd, desc);
            }));
            BotLogger.LogInfo("Starting receiving updates...");
            _ = _botClient.ReceiveAsync(
                updateHandler: UpdateHandler,
                errorHandler: ErrorHandler
            );
            _chatRoomCleanUpTask = Task.Factory.StartNew(() =>
            {
                while(true)
                {
                    try
                    {
                        ChatSession.CleanUpAsync().AsTask().Wait();
                    }
                    catch(Exception e)
                    {
                        BotLogger.LogError($"Failed to clean up the chat room: {e.Message}");
                    }
                    Thread.Sleep(30000);
                }
            }, TaskCreationOptions.LongRunning);
            while(true)
            {
                Thread.Sleep(30000);
                try
                {
                    botDetails = await _botClient.GetMe();
                    if(botDetails.Username != _botUsername)
                    {
                        BotLogger.LogInfo($"New username: {botDetails.Username}");
                    }
                    _botUsername = botDetails.Username ?? string.Empty;
                }
                catch(Exception e)
                {
                    BotLogger.LogError($"Error while getting bot info: {e}");
                }
            }
        }
        static void UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token = default)
        {
            Task.Run(async () =>
            {
                try
                {
                    var updateType = update.Type;
                    switch (updateType)
                    {
                        case UpdateType.Message:
                            if(update.Message!.Date.ToUniversalTime() < StartAt.ToUniversalTime())
                            {
                                return;
                            }
                            break;
                        case UpdateType.EditedMessage:
                            if (update.EditedMessage!.Date.ToUniversalTime() < StartAt.ToUniversalTime())
                            {
                                return;
                            }
                            break;
                        case UpdateType.CallbackQuery:
                            var query = update.CallbackQuery!;
                            var message = query.Message;
                            if (message is null)
                            {
                                BotLogger.LogWarning("Received an invalid callback query");
                                return;
                            }
                            if (message.Date.ToUniversalTime() < StartAt.ToUniversalTime())
                            {
                                return;
                            }
                            var chatRoom = await ChatSession.GetOrCreateChatRoomAsync(message.Chat.Id);
                            await chatRoom.OnCallbackAsync(query);
                            return;
                        default:
                            return;
                    }
                    var userRequest = UserRequest.Parse(client, update);
                    var sender = userRequest.Sender;
                    var chat = userRequest.Chat;
                    var username = string.Empty;
                    var chatName = string.Empty;
                    if (string.IsNullOrEmpty(sender.LastName))
                    {
                        username = sender.FirstName;
                    }
                    else
                    {
                        username = $"{sender.FirstName} {sender.LastName}";
                    }
                    if (string.IsNullOrEmpty(chat.LastName))
                    {
                        chatName = chat.FirstName;
                    }
                    else
                    {
                        chatName = $"{chat.FirstName} {chat.LastName}";
                    }
                    BotLogger.LogInfo($"[Recv][{updateType}] U:{sender.Id}({username})|C:{chat.Id}({chatName})|Said: \"{userRequest.Text}\"");
                    if (userRequest.IsBotSpecified && userRequest.SpecifiedBotUsername != _botUsername)
                    {
                        return;
                    }
                    await BotCommandHandleAsync(userRequest);
                }
                catch(Exception e)
                {
                    BotLogger.LogError(e.ToString());
                }
            });
        }
        static void ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken token = default)
        {
            BotLogger.LogError($"Telegram Bot API Error: {exception}");
        }
        static async ValueTask BotCommandHandleAsync(UserRequest userRequest)
        {
            try
            {
                var cmdEntity = userRequest.Command;
                var chatRoom = await ChatSession.GetOrCreateChatRoomAsync(userRequest.Chat.Id);
                switch (cmdEntity.Prefix)
                {
                    case "trace":
                    case "ping":
                    case "nslookup":
                    case "dig":
                        var controller = NetworkController.FromRequest(userRequest, chatRoom);
                        chatRoom.AddSession(controller);
                        await controller.ExecuteAsync();
                        break;
                    default:
                        return;
                }
            }
            catch(Exception e)
            {
                BotLogger.LogError(e.ToString());
            }
        }
        [DoesNotReturn]
        static void Fatal(string message, int exitCode = -127)
        {
            BotLogger.LogError(message);
            Thread.Sleep(1000);
            Environment.Exit(exitCode);
        }
        [DoesNotReturn]
        static void ExitWithMessage(string message, int exitCode = -127)
        {
            BotLogger.LogInfo(message);
            Thread.Sleep(1000);
            Environment.Exit(exitCode);
        }
        public readonly struct BotInfo
        {
            public long Id => _botId;
            public string Username => _botUsername;
        }
    }
}
